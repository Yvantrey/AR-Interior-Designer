using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A placed (or preview) piece of furniture. It owns three things:
///  1. The user's chosen colour. This is the single source of truth and is
///     applied through MaterialPropertyBlocks, so no material copies are made
///     and the texture still shows through (URP Lit multiplies _BaseMap by _BaseColor).
///  2. Its bounds in its own local space, used to rest it exactly on the floor,
///     to rotate/scale it around its footprint centre, and to measure it.
///  3. The selection highlight: a ring on the floor. The highlight never
///     touches material colours, so it can't overwrite or lose the user's colour.
/// </summary>
public class SelectableFurniture : MonoBehaviour
{
    [Header("Selection Highlight")]
    [Tooltip("Colour of the ring drawn on the floor under the selected piece (Figma cyan).")]
    public Color selectionRingColor = new Color(0.184f, 0.839f, 0.902f, 1f); // #2FD6E6

    [Tooltip("Ring line width in metres.")]
    [Min(0.001f)] public float selectionRingWidth = 0.012f;

    [Tooltip("Gap between the furniture's footprint and the ring, in metres.")]
    [Min(0f)] public float selectionRingMargin = 0.05f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private const int RingCornerSegments = 8;

    private static MaterialPropertyBlock sharedBlock;
    private static Material ringMaterial;

    /// <summary>One material slot whose colour we can tint.</summary>
    private struct ColorSlot
    {
        public Renderer renderer;
        public int materialIndex;
        public int propertyId;
        public Color originalColor;
    }

    private readonly List<ColorSlot> colorSlots = new List<ColorSlot>();
    private LineRenderer ring;
    private bool isSelected;

    /// <summary>
    /// Combined mesh bounds in this object's local space, before this object's
    /// own scale is applied. Unaffected by rotation, so it is the right basis
    /// for measuring real dimensions.
    /// </summary>
    public Bounds LocalBounds { get; private set; }

    /// <summary>The prefab's authored localScale (the "100%" size).</summary>
    public Vector3 OriginalScale { get; private set; } = Vector3.one;

    /// <summary>Current uniform size multiplier relative to <see cref="OriginalScale"/> (1 = 100%).</summary>
    public float ScaleFactor { get; private set; } = 1f;

    public bool IsSelected => isSelected;

    public bool HasCustomColor { get; private set; }
    public Color32 CustomColor { get; private set; } = new Color32(255, 255, 255, 255);

    /// <summary>Base colour of the first tintable material as authored (usually white for textured furniture).</summary>
    public Color32 OriginalColor { get; private set; } = new Color32(255, 255, 255, 255);

    /// <summary>The colour the user currently sees applied: their choice, or the original.</summary>
    public Color32 CurrentColor => HasCustomColor ? CustomColor : OriginalColor;

    /// <summary>World-space point at the centre of the furniture's footprint, on its underside.</summary>
    public Vector3 FootprintCenter
    {
        get
        {
            Bounds b = LocalBounds;
            return transform.TransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
        }
    }

    /// <summary>Rotation around the vertical axis, in degrees.</summary>
    public float Yaw => transform.eulerAngles.y;

    private void Awake()
    {
        OriginalScale = transform.localScale;
        CacheColorSlots();
        LocalBounds = ComputeLocalBounds();
        EnsureSelectionCollider();

        if (gameObject.CompareTag("Untagged"))
            TrySetFurnitureTag();
    }

    private void TrySetFurnitureTag()
    {
        try { gameObject.tag = "Furniture"; }
        catch { /* Tag not created yet - not required for selection to work */ }
    }

    //=========================================================
    // Pose: everything is expressed as "footprint centre on the floor + yaw + size"
    //=========================================================

    /// <summary>
    /// Places the piece so the centre of its underside sits exactly on
    /// <paramref name="footprintWorld"/>, facing <paramref name="yaw"/> degrees,
    /// at <paramref name="scaleFactor"/> times its original size. Rotating or
    /// scaling through this keeps the footprint centre fixed and the bottom on
    /// the floor, even for prefabs whose pivot is not centred.
    /// </summary>
    public void ApplyFootprintPose(Vector3 footprintWorld, float yaw, float scaleFactor)
    {
        ScaleFactor = scaleFactor;

        Vector3 localScale = OriginalScale * scaleFactor;
        transform.localScale = localScale;

        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        Vector3 worldScale = Vector3.Scale(localScale, parentScale);

        Bounds b = LocalBounds;
        Vector3 localBottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

        transform.SetPositionAndRotation(
            footprintWorld - rotation * Vector3.Scale(localBottomCenter, worldScale),
            rotation);
    }

    //=========================================================
    // Colour
    //=========================================================

    /// <summary>
    /// Tints every material on the piece with <paramref name="color"/>. Safe to
    /// call every frame while a slider is dragged: no allocations, no new materials.
    /// </summary>
    public void SetCustomColor(Color32 color)
    {
        color.a = 255;
        HasCustomColor = true;
        CustomColor = color;

        if (sharedBlock == null)
            sharedBlock = new MaterialPropertyBlock();

        for (int i = 0; i < colorSlots.Count; i++)
        {
            ColorSlot slot = colorSlots[i];
            if (slot.renderer == null)
                continue;

            // Keep each material's own alpha so transparent parts stay transparent.
            Color tint = color;
            tint.a = slot.originalColor.a;

            slot.renderer.GetPropertyBlock(sharedBlock, slot.materialIndex);
            sharedBlock.SetColor(slot.propertyId, tint);
            slot.renderer.SetPropertyBlock(sharedBlock, slot.materialIndex);
        }
    }

    /// <summary>Removes the user's colour and shows the materials exactly as authored.</summary>
    public void ResetColor()
    {
        HasCustomColor = false;
        CustomColor = OriginalColor;

        if (sharedBlock == null)
            sharedBlock = new MaterialPropertyBlock();

        sharedBlock.Clear();
        for (int i = 0; i < colorSlots.Count; i++)
        {
            ColorSlot slot = colorSlots[i];
            if (slot.renderer != null)
                slot.renderer.SetPropertyBlock(sharedBlock, slot.materialIndex);
        }
    }

    private void CacheColorSlots()
    {
        colorSlots.Clear();
        bool originalCaptured = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                continue;

            // sharedMaterials: read-only look at the assets, never creates copies.
            Material[] materials = r.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                Material mat = materials[m];
                if (mat == null)
                    continue;

                int propertyId;
                if (mat.HasProperty(BaseColorId)) propertyId = BaseColorId;      // URP Lit / Simple Lit
                else if (mat.HasProperty(LegacyColorId)) propertyId = LegacyColorId; // Built-in / legacy shaders
                else continue;

                Color original = mat.GetColor(propertyId);
                colorSlots.Add(new ColorSlot
                {
                    renderer = r,
                    materialIndex = m,
                    propertyId = propertyId,
                    originalColor = original
                });

                if (!originalCaptured)
                {
                    originalCaptured = true;
                    OriginalColor = original;
                }
            }
        }

        CustomColor = OriginalColor;
    }

    //=========================================================
    // Bounds and collider
    //=========================================================

    /// <summary>
    /// Combines the bounds of every mesh in the hierarchy, expressed in this
    /// object's local space. Uses the meshes' own bounds rather than
    /// Renderer.bounds, which are world-axis-aligned and grow when rotated.
    /// </summary>
    private Bounds ComputeLocalBounds()
    {
        Matrix4x4 worldToRoot = transform.worldToLocalMatrix;
        bool hasBounds = false;
        Bounds result = new Bounds(Vector3.zero, Vector3.zero);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            Mesh mesh = null;
            if (r is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }
            else if (r is MeshRenderer)
            {
                MeshFilter filter = r.GetComponent<MeshFilter>();
                if (filter != null)
                    mesh = filter.sharedMesh;
            }

            if (mesh == null)
                continue;

            Matrix4x4 meshToRoot = worldToRoot * r.transform.localToWorldMatrix;
            Bounds mb = mesh.bounds;
            Vector3 min = mb.min;
            Vector3 max = mb.max;

            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);
                p = meshToRoot.MultiplyPoint3x4(p);

                if (!hasBounds)
                {
                    result = new Bounds(p, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    result.Encapsulate(p);
                }
            }
        }

        // No mesh at all: fall back to a 10 cm cube so the maths stays valid.
        return hasBounds ? result : new Bounds(Vector3.zero, Vector3.one * 0.1f);
    }

    private void EnsureSelectionCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = LocalBounds.center;
        box.size = LocalBounds.size;
    }

    //=========================================================
    // Selection highlight (floor ring)
    //=========================================================

    public void Select()
    {
        isSelected = true;
        EnsureRing();
        ring.enabled = true;
    }

    public void Deselect()
    {
        isSelected = false;
        if (ring != null)
            ring.enabled = false;
    }

    /// <summary>Shows or hides the ring without changing selection (used when capturing a screenshot).</summary>
    public void SetHighlightVisible(bool visible)
    {
        if (ring != null)
            ring.enabled = visible && isSelected;
    }

    private void Update()
    {
        if (!isSelected || ring == null || !ring.enabled)
            return;

        // Gentle pulse so the selection reads as "live" without being distracting.
        float pulse = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * 4f));
        Color c = selectionRingColor;
        c.a *= pulse;
        ring.startColor = c;
        ring.endColor = c;
    }

    private void EnsureRing()
    {
        if (ring != null)
            return;

        if (ringMaterial == null)
            ringMaterial = new Material(Shader.Find("Sprites/Default"));

        GameObject ringObject = new GameObject("Selection Ring");
        ringObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        ringObject.transform.SetParent(transform, false);

        ring = ringObject.AddComponent<LineRenderer>();
        ring.useWorldSpace = false; // follows the furniture as it moves, rotates and scales
        ring.loop = true;
        ring.widthMultiplier = selectionRingWidth;
        ring.numCapVertices = 2;
        ring.numCornerVertices = 2;
        ring.alignment = LineAlignment.TransformZ; // lie flat on the floor instead of facing the camera
        ring.sharedMaterial = ringMaterial;
        ring.startColor = selectionRingColor;
        ring.endColor = selectionRingColor;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;

        // TransformZ alignment makes the line face the ring's local Z axis, so
        // tip the ring object to make its Z point up.
        ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        BuildRingShape();
    }

    /// <summary>A rounded rectangle hugging the footprint, slightly above the floor.</summary>
    private void BuildRingShape()
    {
        Bounds b = LocalBounds;
        float scaleX = Mathf.Max(Mathf.Abs(OriginalScale.x), 0.0001f);
        float scaleY = Mathf.Max(Mathf.Abs(OriginalScale.y), 0.0001f);
        float scaleZ = Mathf.Max(Mathf.Abs(OriginalScale.z), 0.0001f);

        float halfX = b.extents.x + selectionRingMargin / scaleX;
        float halfZ = b.extents.z + selectionRingMargin / scaleZ;
        float radius = Mathf.Min(halfX, halfZ) * 0.5f;
        float lift = b.min.y + 0.004f / scaleY; // 4 mm above the floor to avoid z-fighting

        int count = RingCornerSegments * 4;
        ring.positionCount = count;

        // The ring object is rotated +90 degrees about X, so a ring-local point
        // (x, y, 0) lands at furniture-local (x, 0, y). Position the ring object
        // at floor height and draw the shape in its XY plane.
        ring.transform.localPosition = new Vector3(b.center.x, lift, b.center.z);

        int index = 0;
        for (int corner = 0; corner < 4; corner++)
        {
            float cx = (corner == 0 || corner == 3) ? halfX - radius : -(halfX - radius);
            float cz = (corner < 2) ? halfZ - radius : -(halfZ - radius);
            float startAngle = corner * 90f;

            for (int s = 0; s < RingCornerSegments; s++)
            {
                float angle = (startAngle + 90f * s / (RingCornerSegments - 1)) * Mathf.Deg2Rad;
                float x = cx + Mathf.Cos(angle) * radius;
                float z = cz + Mathf.Sin(angle) * radius;
                ring.SetPosition(index++, new Vector3(x, z, 0f));
            }
        }
    }
}

using UnityEngine;


public class SelectableFurniture : MonoBehaviour
{
    [Header("Selection Highlight")]
    public Color selectedTintColor = new Color(1f, 0.85f, 0.3f); // soft gold

    private Renderer[] renderers;
    private Color[] originalColors;
    private string[] colorPropertyNames;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        originalColors = new Color[renderers.Length];
        colorPropertyNames = new string[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            // .material (not .sharedMaterial) makes a per-instance copy,
            // so tinting one sofa never affects other sofas using the same material.
            Material mat = renderers[i].material;
            string propName = GetColorPropertyName(mat);
            colorPropertyNames[i] = propName;
            if (propName != null)
                originalColors[i] = mat.GetColor(propName);
        }

        EnsureSelectionCollider();

        // Needed so the raycast in TouchInteractionManager can find it if you
        // ever raycast by tag instead of by component (kept for safety/future use)
        if (gameObject.tag == "Untagged")
        {
            // Only set this if you have already created the "Furniture" tag
            // (Part 9 of the guide). If not, this line is safely skipped.
            TrySetFurnitureTag();
        }
    }

    private void TrySetFurnitureTag()
    {
        try { gameObject.tag = "Furniture"; }
        catch { /* Tag not created yet - not required for selection to work */ }
    }

    private void EnsureSelectionCollider()
    {
        if (GetComponentInChildren<Collider>() != null || renderers.Length == 0)
            return;

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            worldBounds.Encapsulate(renderers[i].bounds);

        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = transform.InverseTransformPoint(worldBounds.center);
        collider.size = new Vector3(
            worldBounds.size.x / transform.lossyScale.x,
            worldBounds.size.y / transform.lossyScale.y,
            worldBounds.size.z / transform.lossyScale.z);
    }

       private string GetColorPropertyName(Material mat)
    {
        if (mat.HasProperty("_BaseColor")) return "_BaseColor";
        if (mat.HasProperty("_Color")) return "_Color";
        return null;
    }

    public void Select() => Tint(selectedTintColor);

    public void Deselect() => RestoreOriginalColors();

    private void Tint(Color color)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (colorPropertyNames[i] != null)
                renderers[i].material.SetColor(colorPropertyNames[i], color);
        }
    }

    private void RestoreOriginalColors()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (colorPropertyNames[i] != null)
                renderers[i].material.SetColor(colorPropertyNames[i], originalColors[i]);
        }
    }
}

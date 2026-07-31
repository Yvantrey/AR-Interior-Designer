using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class TouchInteractionManager : MonoBehaviour
{
    [Header("AR References (drag from your XR Origin)")]
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    [Tooltip("Locks placed furniture to the real-world plane it was placed on, so it doesn't drift or vanish as ARCore refines its tracking.")]
    public ARAnchorManager anchorManager;
    public Camera arCamera;

    [Header("Scene References")]
    [Tooltip("An empty GameObject that will be the parent of every piece of furniture you place.")]
    public Transform placedFurnitureRoot;

    [Tooltip("The panel containing the Rotate, Scale, Move and Delete buttons.")]
    public GameObject controlsPanel;

    [Tooltip("The panel containing the furniture selection buttons.")]
    public GameObject furniturePanel;

    [Header("State (read only, just for you to watch in Play Mode)")]
    public bool isMoveModeOn = false;

    [Header("Manipulation Limits")]
    [Min(0.01f)] public float minimumScale = 0.25f;
    [Min(0.01f)] public float maximumScale = 3f;

    private static List<ARRaycastHit> arHits = new List<ARRaycastHit>();

    private GameObject furnitureToPlacePrefab;
    private GameObject placementPreview;
    private SelectableFurniture currentSelection;
    private bool floorDetected;
    private readonly List<GameObject> placedFurniture = new List<GameObject>();

    public bool FloorDetected => floorDetected;

    private void Start()
    {
        if (raycastManager == null)
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (arCamera == null)
            arCamera = Camera.main;
        if (anchorManager == null)
            anchorManager = FindFirstObjectByType<ARAnchorManager>();

        FloorPlaneIndicator indicator = GetComponent<FloorPlaneIndicator>();
        if (indicator == null)
            indicator = gameObject.AddComponent<FloorPlaneIndicator>();
        indicator.Initialize(raycastManager, planeManager);

        PlaneTrackingVisualizer planeVisualizer = GetComponent<PlaneTrackingVisualizer>();
        if (planeVisualizer == null)
            planeVisualizer = gameObject.AddComponent<PlaneTrackingVisualizer>();
        planeVisualizer.Initialize(planeManager);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        if (furniturePanel != null)
            furniturePanel.SetActive(false);
    }

    void Update()
    {
        UpdateFloorDetectedState();
        if (!floorDetected)
            return;

        UpdatePlacementPreview();

        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase != TouchPhase.Began) return;

        // Ignore touches on UI
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            return;

        if (arCamera == null || raycastManager == null)
            return;

        Ray ray = arCamera.ScreenPointToRay(touch.position);

        // Did we tap existing furniture?
        if (Physics.Raycast(ray, out RaycastHit physicsHit, 50f))
        {
            SelectableFurniture hitFurniture =
                physicsHit.collider.GetComponentInParent<SelectableFurniture>();

            if (hitFurniture != null)
            {
                SelectFurniture(hitFurniture);
                return;
            }
        }

        // Did we tap a detected plane?
        if (TryGetHorizontalPlanePose(touch.position, out Pose hitPose, out ARPlane hitPlane))
        {
            if (furnitureToPlacePrefab != null)
            {
                PlaceFurniture(hitPose, hitPlane);
            }
            else if (currentSelection != null && isMoveModeOn)
            {
                currentSelection.transform.position = hitPose.position;
                RestFurnitureOnSurface(currentSelection.gameObject, hitPose.position.y);
            }
            else
            {
                DeselectCurrent();
            }
        }
    }

    private void PlaceFurniture(Pose pose, ARPlane plane)
    {
        if (placedFurnitureRoot == null || furnitureToPlacePrefab == null)
            return;

        // Carry over whatever size the user dialled in on the preview
        // (via the Scale buttons) so the placed piece keeps that size.
        Vector3 previewScale = placementPreview != null
            ? placementPreview.transform.localScale
            : Vector3.one;

        pose.rotation = Quaternion.Euler(0f, arCamera.transform.eulerAngles.y, 0f);

        // Attach an ARAnchor to the plane the item was placed on. Without an
        // anchor, furniture stays fixed at a single Unity-world position; as
        // ARCore keeps refining its understanding of the room while you move
        // the phone, that fixed position drifts out of alignment with the
        // real floor (it can appear to sink, float, or vanish). An anchor is
        // continuously corrected by ARCore, so the furniture stays locked to
        // the physical point it was placed on.
        Transform parent = placedFurnitureRoot;
        if (anchorManager != null && plane != null)
        {
            ARAnchor anchor = anchorManager.AttachAnchor(plane, pose);
            if (anchor != null)
                parent = anchor.transform;
        }

        GameObject newFurniture = Instantiate(
            furnitureToPlacePrefab,
            pose.position,
            pose.rotation,
            parent);

        newFurniture.transform.localScale = previewScale;

        RestFurnitureOnSurface(newFurniture, pose.position.y);

        SelectableFurniture selectable =
            newFurniture.GetComponent<SelectableFurniture>();

        if (selectable == null)
            selectable = newFurniture.AddComponent<SelectableFurniture>();

        placedFurniture.Add(newFurniture);

        furnitureToPlacePrefab = null;
        DestroyPlacementPreview();

        SelectFurniture(selectable);
    }

    private void UpdatePlacementPreview()
    {
        if (placementPreview == null || furnitureToPlacePrefab == null || arCamera == null)
            return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!TryGetHorizontalPlanePose(screenCenter, out Pose pose, out _))
        {
            placementPreview.SetActive(false);
            return;
        }

        placementPreview.SetActive(true);
        placementPreview.transform.SetPositionAndRotation(
            pose.position,
            Quaternion.Euler(0f, arCamera.transform.eulerAngles.y, 0f));
        RestFurnitureOnSurface(placementPreview, pose.position.y);
    }

    private void CreatePlacementPreview(GameObject prefab)
    {
        DestroyPlacementPreview();

        placementPreview = Instantiate(prefab, placedFurnitureRoot);
        placementPreview.name = $"{prefab.name} Placement Preview";
        SetLayerRecursively(placementPreview, LayerMask.NameToLayer("Ignore Raycast"));
        UpdatePlacementPreview();
    }

    private void DestroyPlacementPreview()
    {
        if (placementPreview != null)
            Destroy(placementPreview);
        placementPreview = null;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private bool TryGetHorizontalPlanePose(Vector2 screenPosition, out Pose pose, out ARPlane plane)
    {
        pose = default;
        plane = null;

        if (!raycastManager.Raycast(screenPosition, arHits, TrackableType.PlaneWithinPolygon))
            return false;

        ARRaycastHit hit = arHits[0];
        if (planeManager != null)
        {
            plane = planeManager.GetPlane(hit.trackableId);
            if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp)
                return false;
        }

        pose = hit.pose;
        return true;
    }

    private void UpdateFloorDetectedState()
    {
        if (floorDetected || planeManager == null)
            return;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane.trackingState == TrackingState.Tracking &&
                plane.alignment == PlaneAlignment.HorizontalUp)
            {
                floorDetected = true;
                if (furniturePanel != null)
                    furniturePanel.SetActive(true);
                break;
            }
        }
    }

    private static void RestFurnitureOnSurface(GameObject furniture, float surfaceY)
    {
        Renderer[] renderers = furniture.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        furniture.transform.position += Vector3.up * (surfaceY - bounds.min.y);
    }

    private void SelectFurniture(SelectableFurniture furniture)
    {
        DeselectCurrent();

        currentSelection = furniture;
        currentSelection.Select();

        if (controlsPanel != null)
            controlsPanel.SetActive(true);

        if (furniturePanel != null)
            furniturePanel.SetActive(floorDetected);
    }

    private void DeselectCurrent()
    {
        isMoveModeOn = false;
        furnitureToPlacePrefab = null;
        DestroyPlacementPreview();

        if (currentSelection != null)
        {
            currentSelection.Deselect();
            currentSelection = null;
        }

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        if (furniturePanel != null)
            furniturePanel.SetActive(floorDetected);
    }

    //=========================================================
    // Called by UI Buttons
    //=========================================================

    /// <summary>
    /// Called when a furniture button is pressed.
    /// </summary>
    public void SetFurnitureToPlace(GameObject prefab)
    {
        if (!floorDetected || prefab == null)
        {
            Debug.LogWarning("Furniture cannot be selected until a floor is detected and its prefab is assigned.");
            return;
        }

        DeselectCurrent();

        furnitureToPlacePrefab = prefab;
        CreatePlacementPreview(prefab);

        // Keep the catalogue open (same as when furniture is selected, see
        // SelectFurniture) so the user can tap a different item to swap the
        // pending prefab, and show the controls panel so Scale (resize the
        // pending item before it's placed) and Delete (cancel back to
        // browsing) are available while previewing.
        if (furniturePanel != null)
            furniturePanel.SetActive(true);

        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }

    /// <summary>
    /// Called by the Move button.
    /// </summary>
    public void ToggleMoveMode()
    {
        isMoveModeOn = !isMoveModeOn;
    }

    /// <summary>
    /// Called by Rotate Left / Rotate Right buttons.
    /// </summary>
    public void RotateSelected(float degrees)
    {
        if (currentSelection != null)
        {
            currentSelection.transform.Rotate(
                Vector3.up,
                degrees,
                Space.World);
        }
    }

    /// <summary>
    /// Called by Scale Up / Scale Down buttons. Resizes whichever furniture
    /// is currently relevant: the pending preview if one is being placed,
    /// otherwise the currently selected, already-placed piece.
    /// </summary>
    public void ScaleSelected(float factor)
    {
        if (factor <= 0f)
            return;

        Transform target = placementPreview != null
            ? placementPreview.transform
            : (currentSelection != null ? currentSelection.transform : null);

        if (target == null)
            return;

        Vector3 scale = target.localScale * factor;
        float largestAxis = Mathf.Max(scale.x, scale.y, scale.z);
        if (largestAxis > 0f)
        {
            float clampedLargestAxis = Mathf.Clamp(largestAxis, minimumScale, maximumScale);
            target.localScale *= clampedLargestAxis / largestAxis;
        }
    }

    /// <summary>
    /// Called by the Delete button. While a placement is pending, this
    /// cancels it and returns to the catalogue. Otherwise it deletes the
    /// currently selected, already-placed furniture.
    /// </summary>
    public void DeleteSelected()
    {
        if (furnitureToPlacePrefab != null || placementPreview != null)
        {
            DeselectCurrent();
            return;
        }

        if (currentSelection != null)
        {
            GameObject obj = currentSelection.gameObject;
            placedFurniture.Remove(obj);

            DeselectCurrent();

            DestroyPlacedFurniture(obj);
        }
    }

    /// <summary>
    /// Called by the Reset button.
    /// </summary>
    public void ResetLayout()
    {
        DeselectCurrent();

        furnitureToPlacePrefab = null;
        DestroyPlacementPreview();

        foreach (GameObject furniture in placedFurniture)
        {
            DestroyPlacedFurniture(furniture);
        }

        placedFurniture.Clear();
    }

    /// <summary>
    /// Destroys a placed piece of furniture and, if it was attached to an
    /// ARAnchor, removes and destroys that anchor too (otherwise an empty,
    /// still-tracked anchor object would be left behind).
    /// </summary>
    private void DestroyPlacedFurniture(GameObject furniture)
    {
        ARAnchor anchor = furniture.transform.parent != null
            ? furniture.transform.parent.GetComponent<ARAnchor>()
            : null;

        if (anchor != null)
        {
            if (anchorManager != null)
                anchorManager.TryRemoveAnchor(anchor);

            Destroy(anchor.gameObject);
        }
        else
        {
            Destroy(furniture);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Placement and finger gestures for AR furniture.
///  - Tap floor (with a piece chosen in the catalogue): place it.
///  - Tap a piece: select it. Tap anywhere else: deselect.
///  - One-finger drag on a piece: move it along the floor.
///  - Two-finger pinch: resize. Two-finger twist: rotate.
/// Uses the Input System's EnhancedTouch API, which matches the project's
/// "Input System Package (New)" Active Input Handling setting.
/// </summary>
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

    [Tooltip("The panel containing the Delete and Reset buttons. Shown while a piece is selected or being placed.")]
    public GameObject controlsPanel;

    [Tooltip("The panel containing the furniture selection buttons.")]
    public GameObject furniturePanel;

    [Header("Manipulation Limits")]
    [Tooltip("Smallest allowed size, as a multiple of the prefab's original size (0.25 = 25%).")]
    [Min(0.01f)] public float minimumScale = 0.25f;
    [Tooltip("Largest allowed size, as a multiple of the prefab's original size (3 = 300%).")]
    [Min(0.01f)] public float maximumScale = 3f;

    [Header("Gesture Tuning")]
    [Tooltip("How far a finger must move (in dp, ~1/160 inch) before a touch becomes a drag instead of a tap.")]
    [Min(1f)] public float dragThresholdDp = 10f;
    [Tooltip("Change in finger spacing (dp) before a pinch starts resizing. Stops twists from accidentally resizing.")]
    [Min(0f)] public float pinchThresholdDp = 16f;
    [Tooltip("Twist angle (degrees) before rotation starts. Stops pinches from accidentally rotating.")]
    [Min(0f)] public float twistThresholdDegrees = 6f;
    [Tooltip("How quickly the furniture catches up with your fingers. Higher = snappier, lower = smoother.")]
    [Range(5f, 60f)] public float followSharpness = 25f;
    [Tooltip("A piece can't be dragged further than this from the camera (metres). Stops it shooting to the horizon when the finger points above the floor.")]
    [Min(0.5f)] public float maxDragDistance = 8f;
    [Tooltip("While dragging, only follow detected planes within this height (metres) of the current floor, so a sofa doesn't jump onto a table top.")]
    [Min(0.01f)] public float floorHeightTolerance = 0.15f;
    [Tooltip("A touch held longer than this (seconds) without moving is not treated as a tap.")]
    [Min(0.1f)] public float maxTapDuration = 0.6f;

    /// <summary>Raised whenever the selected piece changes (null = nothing selected).</summary>
    public event Action<SelectableFurniture> SelectionChanged;

    public SelectableFurniture CurrentSelection => currentSelection;
    public bool IsPlacementPending => furnitureToPlacePrefab != null;
    public bool IsManipulating => mode == GestureMode.Dragging || mode == GestureMode.TwoFinger;
    public bool FloorDetected => floorDetected;
    public IReadOnlyList<GameObject> PlacedFurniture => placedFurniture;

    private enum GestureMode
    {
        None,        // no fingers on the AR view
        PendingTap,  // one finger down, hasn't moved far enough to be a drag yet
        Dragging,    // one finger moving the selected piece
        TwoFinger,   // pinch / twist
        Consumed     // gesture finished or invalid; ignore fingers until they all lift
    }

    private static readonly List<ARRaycastHit> arHits = new List<ARRaycastHit>();
    private static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    private GameObject furnitureToPlacePrefab;
    private GameObject placementPreview;
    private SelectableFurniture previewFurniture;
    private float previewScaleFactor = 1f;
    private float previewYawOffset;

    private SelectableFurniture currentSelection;
    private bool floorDetected;
    private readonly List<GameObject> placedFurniture = new List<GameObject>();

    // Touch tracking
    private GestureMode mode = GestureMode.None;
    private int primaryId = -1;
    private int secondaryId = -1;
    private bool secondFingerSeen;
    private Vector2 primaryStartPosition;
    private SelectableFurniture primaryStartFurniture;
    private readonly HashSet<int> uiTouchIds = new HashSet<int>();
    private PointerEventData uiPointer;
    private EventSystem uiPointerOwner;

    // Manipulation target. "Target" values follow the fingers exactly; "current"
    // values chase them smoothly and are what the furniture actually shows.
    private SelectableFurniture manipulated;
    private bool manipulatingPreview;
    private Vector3 targetFootprint, currentFootprint, manipulationStartFootprint;
    private float targetYaw, currentYaw;
    private float targetScale, currentScale;

    // Drag
    private Vector2 dragScreenOffset;
    private float dragFloorY;

    // Pinch / twist
    private float pinchStartDistance;
    private float pinchBaseScale;
    private bool pinchActive;
    private Vector2 twistPreviousVector;
    private float twistAccumulated;
    private bool twistActive;

    private void OnEnable()
    {
        EnhancedTouchSupport.Enable();
    }

    private void OnDisable()
    {
        CancelGesture();
        EnhancedTouchSupport.Disable();
    }

    private void OnApplicationPause(bool paused)
    {
        // Fingers that were down when the app was backgrounded never report "Ended".
        if (paused)
        {
            CancelGesture();
            primaryId = -1;
            secondaryId = -1;
            uiTouchIds.Clear();
            mode = GestureMode.None;
        }
    }

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

    private void Update()
    {
        UpdateFloorDetectedState();
        if (!floorDetected || arCamera == null || raycastManager == null)
            return;

        ProcessTouches();
        ApplySmoothedManipulation();
        UpdatePlacementPreview();
    }

    //=========================================================
    // Touch handling
    //=========================================================

    private void ProcessTouches()
    {
        ReadOnlyArray<Touch> touches = Touch.activeTouches;

        // 1. Register fingers that just went down.
        for (int i = 0; i < touches.Count; i++)
        {
            Touch touch = touches[i];
            if (touch.phase != TouchPhase.Began)
                continue;

            // Touches that start on UI (sliders, buttons, panels) belong to the UI for their whole life.
            if (IsOverUI(touch.screenPosition))
            {
                uiTouchIds.Add(touch.touchId);
                continue;
            }

            if (primaryId == -1 && secondaryId == -1)
                BeginPrimary(touch);
            else if (secondaryId == -1 && primaryId != -1)
                BeginSecondary(touch, touches);
            // A third finger, or a new finger while one is left over from a
            // finished pinch, is ignored.
        }

        // 2. Look up the fingers we are tracking.
        bool hasPrimary = TryGetTouch(touches, primaryId, out Touch primary);
        bool hasSecondary = TryGetTouch(touches, secondaryId, out Touch secondary);
        bool primaryEnded = primaryId != -1 && (!hasPrimary || IsFinished(primary.phase));
        bool secondaryEnded = secondaryId != -1 && (!hasSecondary || IsFinished(secondary.phase));

        // 3. Advance the gesture.
        switch (mode)
        {
            case GestureMode.PendingTap:
                if (primaryEnded)
                {
                    bool isTap = hasPrimary &&
                                 primary.phase == TouchPhase.Ended &&
                                 !secondFingerSeen &&
                                 primary.time - primary.startTime <= maxTapDuration;
                    if (isTap)
                        HandleTap(primary.screenPosition);
                    mode = GestureMode.Consumed;
                }
                else if (hasPrimary &&
                         (primary.screenPosition - primaryStartPosition).sqrMagnitude > Sqr(DpToPixels(dragThresholdDp)))
                {
                    BeginDrag(primary.screenPosition);
                }
                break;

            case GestureMode.Dragging:
                if (primaryEnded || manipulated == null)
                {
                    EndManipulation();
                    mode = GestureMode.Consumed;
                }
                else
                {
                    UpdateDrag(primary.screenPosition);
                }
                break;

            case GestureMode.TwoFinger:
                if (primaryEnded || secondaryEnded || manipulated == null)
                {
                    // Lifting one finger ends the pinch. The remaining finger is
                    // ignored until it lifts, so the piece never jumps under it.
                    EndManipulation();
                    mode = GestureMode.Consumed;
                }
                else
                {
                    UpdateTwoFinger(primary.screenPosition, secondary.screenPosition);
                }
                break;
        }

        // 4. Forget fingers that lifted.
        if (primaryEnded) primaryId = -1;
        if (secondaryEnded) secondaryId = -1;

        if (primaryId == -1 && secondaryId == -1 && mode != GestureMode.None)
        {
            if (IsManipulating)
                EndManipulation();
            mode = GestureMode.None;
            secondFingerSeen = false;
        }

        if (uiTouchIds.Count > 0)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                if (IsFinished(touches[i].phase))
                    uiTouchIds.Remove(touches[i].touchId);
            }
            if (touches.Count == 0)
                uiTouchIds.Clear();
        }
    }

    private void BeginPrimary(Touch touch)
    {
        primaryId = touch.touchId;
        primaryStartPosition = touch.screenPosition;
        primaryStartFurniture = RaycastFurniture(touch.screenPosition);
        secondFingerSeen = false;
        mode = GestureMode.PendingTap;
    }

    private void BeginSecondary(Touch touch, ReadOnlyArray<Touch> touches)
    {
        secondaryId = touch.touchId;
        secondFingerSeen = true;

        if (mode != GestureMode.PendingTap && mode != GestureMode.Dragging)
        {
            mode = GestureMode.Consumed;
            return;
        }

        if (!TryGetTouch(touches, primaryId, out Touch primary))
        {
            mode = GestureMode.Consumed;
            return;
        }

        // Pinch/twist the piece being placed, otherwise the selected piece.
        SelectableFurniture target = furnitureToPlacePrefab != null ? previewFurniture : currentSelection;
        if (target == null)
        {
            mode = GestureMode.Consumed;
            return;
        }

        // Going from drag to pinch keeps the drag's targets, so nothing jumps.
        if (mode != GestureMode.Dragging)
            BeginManipulation(target, target == previewFurniture);

        Vector2 between = touch.screenPosition - primary.screenPosition;
        pinchStartDistance = Mathf.Max(between.magnitude, 1f);
        pinchBaseScale = targetScale;
        pinchActive = false;
        twistPreviousVector = between;
        twistAccumulated = 0f;
        twistActive = false;

        mode = GestureMode.TwoFinger;
    }

    private void HandleTap(Vector2 screenPosition)
    {
        SelectableFurniture tappedFurniture = RaycastFurniture(screenPosition);
        if (tappedFurniture != null)
        {
            SelectFurniture(tappedFurniture);
            return;
        }

        if (furnitureToPlacePrefab != null)
        {
            if (TryGetHorizontalPlanePose(screenPosition, float.NaN, out Pose hitPose, out ARPlane hitPlane))
                PlaceFurniture(hitPose, hitPlane);
            return;
        }

        // Tapped empty floor (or empty space) with nothing pending.
        DeselectCurrent();
    }

    //=========================================================
    // Drag
    //=========================================================

    private void BeginDrag(Vector2 screenPosition)
    {
        // Only drag when the finger started on a placed piece, and never while
        // a new piece is waiting to be placed.
        if (furnitureToPlacePrefab != null || primaryStartFurniture == null)
        {
            mode = GestureMode.Consumed;
            return;
        }

        if (primaryStartFurniture != currentSelection)
            SelectFurniture(primaryStartFurniture);

        BeginManipulation(currentSelection, false);
        dragFloorY = currentFootprint.y;

        // Remember where the finger is relative to the piece's footprint on
        // screen, so the piece doesn't snap its centre to the fingertip.
        Vector3 footprintOnScreen = arCamera.WorldToScreenPoint(currentFootprint);
        dragScreenOffset = (Vector2)footprintOnScreen - primaryStartPosition;

        mode = GestureMode.Dragging;
        UpdateDrag(screenPosition);
    }

    private void UpdateDrag(Vector2 fingerPosition)
    {
        Vector2 screenPoint = fingerPosition + dragScreenOffset;
        if (TryGetFloorPoint(screenPoint, dragFloorY, out Vector3 floorPoint))
        {
            targetFootprint = floorPoint;
            dragFloorY = floorPoint.y;
        }
        // Otherwise the finger is pointing above the horizon: hold the last position.
    }

    /// <summary>
    /// Finds the floor point under a screen position. Prefers a real detected
    /// plane near the current floor height; falls back to an infinite plane at
    /// that height so dragging stays smooth over not-yet-scanned floor.
    /// </summary>
    private bool TryGetFloorPoint(Vector2 screenPoint, float floorY, out Vector3 point)
    {
        if (TryGetHorizontalPlanePose(screenPoint, floorY, out Pose pose, out _))
        {
            point = ClampToMaxDragDistance(pose.position);
            return true;
        }

        Ray ray = arCamera.ScreenPointToRay(screenPoint);
        Plane floor = new Plane(Vector3.up, new Vector3(0f, floorY, 0f));
        if (floor.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            point.y = floorY;
            point = ClampToMaxDragDistance(point);
            return true;
        }

        point = default;
        return false;
    }

    private Vector3 ClampToMaxDragDistance(Vector3 point)
    {
        Vector3 cameraPosition = arCamera.transform.position;
        Vector3 flat = point - cameraPosition;
        flat.y = 0f;
        float distance = flat.magnitude;
        if (distance <= maxDragDistance)
            return point;

        Vector3 clamped = cameraPosition + flat * (maxDragDistance / distance);
        clamped.y = point.y;
        return clamped;
    }

    //=========================================================
    // Pinch and twist
    //=========================================================

    private void UpdateTwoFinger(Vector2 a, Vector2 b)
    {
        Vector2 between = b - a;
        float distance = between.magnitude;

        // Pinch (resize). Starts only once the fingers have clearly spread or
        // closed, then follows the ratio of finger spacing.
        if (!pinchActive && Mathf.Abs(distance - pinchStartDistance) > DpToPixels(pinchThresholdDp))
        {
            pinchActive = true;
            pinchStartDistance = Mathf.Max(distance, 1f);
            pinchBaseScale = targetScale;
        }

        if (pinchActive)
        {
            float raw = pinchBaseScale * distance / pinchStartDistance;
            float clamped = Mathf.Clamp(raw, minimumScale, maximumScale);
            if (!Mathf.Approximately(raw, clamped))
            {
                // Re-base at the limit, so pinching back responds immediately
                // instead of first "unwinding" the overshoot.
                pinchBaseScale = clamped;
                pinchStartDistance = Mathf.Max(distance, 1f);
            }
            targetScale = clamped;
        }

        // Twist (rotate). Accumulated frame by frame, so turning past 180
        // degrees keeps going instead of flipping.
        if (between.sqrMagnitude > 1f && twistPreviousVector.sqrMagnitude > 1f)
        {
            float delta = Vector2.SignedAngle(twistPreviousVector, between);
            twistAccumulated += delta;

            if (!twistActive && Mathf.Abs(twistAccumulated) > twistThresholdDegrees)
                twistActive = true;
            else if (twistActive)
                targetYaw -= delta; // counter-clockwise on screen = counter-clockwise seen from above
        }
        twistPreviousVector = between;
    }

    //=========================================================
    // Manipulation state shared by drag / pinch / twist
    //=========================================================

    private void BeginManipulation(SelectableFurniture target, bool isPreview)
    {
        manipulated = target;
        manipulatingPreview = isPreview;

        if (isPreview)
        {
            currentYaw = targetYaw = previewYawOffset;
            currentScale = targetScale = previewScaleFactor;
        }
        else
        {
            currentFootprint = targetFootprint = target.FootprintCenter;
            currentYaw = targetYaw = target.Yaw;
            currentScale = targetScale = target.ScaleFactor;
        }

        manipulationStartFootprint = currentFootprint;
    }

    private void ApplySmoothedManipulation()
    {
        if (!IsManipulating || manipulated == null)
            return;

        // Frame-rate independent exponential smoothing.
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, t);
        currentScale = Mathf.Lerp(currentScale, targetScale, t);

        if (manipulatingPreview)
        {
            previewYawOffset = currentYaw;
            previewScaleFactor = currentScale;
            return;
        }

        currentFootprint = Vector3.Lerp(currentFootprint, targetFootprint, t);
        manipulated.ApplyFootprintPose(currentFootprint, currentYaw, currentScale);
    }

    /// <summary>Snaps to where the fingers ended and re-anchors the piece if it moved.</summary>
    private void EndManipulation()
    {
        if (manipulated != null)
        {
            if (manipulatingPreview)
            {
                previewYawOffset = targetYaw;
                previewScaleFactor = targetScale;
            }
            else
            {
                manipulated.ApplyFootprintPose(targetFootprint, targetYaw, targetScale);

                // Moved more than 1 cm: give it a fresh anchor at its new spot.
                if ((targetFootprint - manipulationStartFootprint).sqrMagnitude > 0.0001f)
                    ReanchorToFloor(manipulated);
            }
        }

        manipulated = null;
        manipulatingPreview = false;
    }

    /// <summary>Ends any gesture in progress (used by buttons, pause and disable).</summary>
    private void CancelGesture()
    {
        if (IsManipulating)
            EndManipulation();

        mode = (primaryId != -1 || secondaryId != -1) ? GestureMode.Consumed : GestureMode.None;
    }

    /// <summary>
    /// Moves the piece onto a new ARAnchor attached to the plane under its new
    /// position. An anchor only corrects drift near where it was created, so
    /// after a move the old, distant anchor would let the piece slowly sink,
    /// float or slide; a fresh one keeps it locked to the real floor.
    /// </summary>
    private void ReanchorToFloor(SelectableFurniture furniture)
    {
        if (anchorManager == null || furniture == null)
            return;

        Vector3 footprint = furniture.FootprintCenter;
        Pose hitPose = default;
        ARPlane plane = null;
        bool found = false;

        // Normally the piece is on screen: raycast through it to the floor.
        Vector3 screenPoint = arCamera.WorldToScreenPoint(footprint);
        if (screenPoint.z > 0f &&
            screenPoint.x >= 0f && screenPoint.x <= Screen.width &&
            screenPoint.y >= 0f && screenPoint.y <= Screen.height)
        {
            found = TryGetHorizontalPlanePose(screenPoint, footprint.y, out hitPose, out plane);
        }

        // Otherwise cast straight down from just above it.
        if (!found)
        {
            Ray down = new Ray(footprint + Vector3.up * 0.5f, Vector3.down);
            if (raycastManager.Raycast(down, arHits, TrackableType.PlaneWithinPolygon))
                found = TryPickHorizontalHit(footprint.y, out hitPose, out plane);
        }

        // No plane under it: keep the old anchor rather than leave it un-anchored.
        if (!found || plane == null)
            return;

        float yaw = furniture.Yaw;
        Pose anchorPose = new Pose(
            new Vector3(footprint.x, hitPose.position.y, footprint.z),
            Quaternion.Euler(0f, yaw, 0f));

        ARAnchor newAnchor = anchorManager.AttachAnchor(plane, anchorPose);
        if (newAnchor == null)
            return;

        Transform oldParent = furniture.transform.parent;
        ARAnchor oldAnchor = oldParent != null ? oldParent.GetComponent<ARAnchor>() : null;

        // Re-parent BEFORE removing the old anchor, or the furniture would be
        // destroyed along with it.
        furniture.transform.SetParent(newAnchor.transform, true);
        furniture.ApplyFootprintPose(anchorPose.position, yaw, furniture.ScaleFactor);

        if (oldAnchor != null)
            RemoveAnchor(oldAnchor);
    }

    //=========================================================
    // Placement
    //=========================================================

    private void PlaceFurniture(Pose pose, ARPlane plane)
    {
        if (furnitureToPlacePrefab == null)
            return;

        // Keep the rotation and size the user gave the preview.
        float yaw = FacingUserYaw() + previewYawOffset;
        pose.rotation = Quaternion.Euler(0f, yaw, 0f);

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

        SelectableFurniture selectable = GetOrAddSelectable(newFurniture);
        selectable.ApplyFootprintPose(pose.position, yaw, previewScaleFactor);

        placedFurniture.Add(newFurniture);

        furnitureToPlacePrefab = null;
        DestroyPlacementPreview();

        SelectFurniture(selectable);
    }

    private void UpdatePlacementPreview()
    {
        if (placementPreview == null || previewFurniture == null || furnitureToPlacePrefab == null)
            return;

        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (!TryGetHorizontalPlanePose(screenCenter, float.NaN, out Pose pose, out _))
        {
            placementPreview.SetActive(false);
            return;
        }

        placementPreview.SetActive(true);
        previewFurniture.ApplyFootprintPose(
            pose.position,
            FacingUserYaw() + previewYawOffset,
            previewScaleFactor);
    }

    /// <summary>
    /// Yaw that turns a piece's front (+Z, which every catalog item and room
    /// set is built to) toward the user: the camera's heading turned 180°.
    /// </summary>
    private float FacingUserYaw()
    {
        return arCamera.transform.eulerAngles.y + 180f;
    }

    private void CreatePlacementPreview(GameObject prefab)
    {
        DestroyPlacementPreview();

        previewScaleFactor = 1f;
        previewYawOffset = 0f;

        placementPreview = Instantiate(prefab, placedFurnitureRoot);
        placementPreview.name = $"{prefab.name} Placement Preview";
        previewFurniture = GetOrAddSelectable(placementPreview);
        SetLayerRecursively(placementPreview, LayerMask.NameToLayer("Ignore Raycast"));
        UpdatePlacementPreview();
    }

    private void DestroyPlacementPreview()
    {
        if (manipulatingPreview)
            CancelGesture();

        if (placementPreview != null)
            Destroy(placementPreview);
        placementPreview = null;
        previewFurniture = null;
    }

    private static SelectableFurniture GetOrAddSelectable(GameObject furniture)
    {
        SelectableFurniture selectable = furniture.GetComponent<SelectableFurniture>();
        if (selectable == null)
            selectable = furniture.AddComponent<SelectableFurniture>();
        return selectable;
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    //=========================================================
    // Raycast helpers
    //=========================================================

    /// <summary>
    /// AR-raycasts a screen point and returns the first upward-facing plane.
    /// When <paramref name="nearY"/> is not NaN, planes further than
    /// floorHeightTolerance from that height are skipped.
    /// </summary>
    private bool TryGetHorizontalPlanePose(Vector2 screenPosition, float nearY, out Pose pose, out ARPlane plane)
    {
        pose = default;
        plane = null;

        if (!raycastManager.Raycast(screenPosition, arHits, TrackableType.PlaneWithinPolygon))
            return false;

        return TryPickHorizontalHit(nearY, out pose, out plane);
    }

    private bool TryPickHorizontalHit(float nearY, out Pose pose, out ARPlane plane)
    {
        for (int i = 0; i < arHits.Count; i++)
        {
            ARRaycastHit hit = arHits[i];
            ARPlane hitPlane = null;

            if (planeManager != null)
            {
                hitPlane = planeManager.GetPlane(hit.trackableId);
                if (hitPlane == null || hitPlane.alignment != PlaneAlignment.HorizontalUp)
                    continue;
            }

            if (!float.IsNaN(nearY) && Mathf.Abs(hit.pose.position.y - nearY) > floorHeightTolerance)
                continue;

            pose = hit.pose;
            plane = hitPlane;
            return true;
        }

        pose = default;
        plane = null;
        return false;
    }

    private SelectableFurniture RaycastFurniture(Vector2 screenPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 50f))
            return null;

        SelectableFurniture furniture = hit.collider.GetComponentInParent<SelectableFurniture>();
        return furniture == previewFurniture ? null : furniture;
    }

    /// <summary>
    /// True when a screen point is over any UI element. Raycasts the UI directly
    /// rather than using IsPointerOverGameObject, which can be one frame late for
    /// a touch that has only just begun.
    /// </summary>
    private bool IsOverUI(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (uiPointer == null || uiPointerOwner != eventSystem)
        {
            uiPointer = new PointerEventData(eventSystem);
            uiPointerOwner = eventSystem;
        }

        uiPointer.position = screenPosition;
        uiHits.Clear();
        eventSystem.RaycastAll(uiPointer, uiHits);
        return uiHits.Count > 0;
    }

    private static bool TryGetTouch(ReadOnlyArray<Touch> touches, int touchId, out Touch result)
    {
        if (touchId != -1)
        {
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].touchId == touchId)
                {
                    result = touches[i];
                    return true;
                }
            }
        }

        result = default;
        return false;
    }

    private static bool IsFinished(TouchPhase phase) =>
        phase == TouchPhase.Ended || phase == TouchPhase.Canceled;

    private static float DpToPixels(float dp) =>
        dp * (Screen.dpi > 0f ? Screen.dpi / 160f : 2.5f);

    private static float Sqr(float value) => value * value;

    //=========================================================
    // Floor detection and selection
    //=========================================================

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

    private void SelectFurniture(SelectableFurniture furniture)
    {
        if (furniture == null)
            return;

        ClearSelection(false);

        currentSelection = furniture;
        currentSelection.Select();

        if (controlsPanel != null)
            controlsPanel.SetActive(true);

        if (furniturePanel != null)
            furniturePanel.SetActive(floorDetected);

        SelectionChanged?.Invoke(currentSelection);
    }

    private void DeselectCurrent()
    {
        ClearSelection(true);
    }

    private void ClearSelection(bool notify)
    {
        if (IsManipulating && !manipulatingPreview)
            CancelGesture();

        furnitureToPlacePrefab = null;
        DestroyPlacementPreview();

        bool hadSelection = currentSelection != null;
        if (currentSelection != null)
        {
            currentSelection.Deselect();
            currentSelection = null;
        }

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        if (furniturePanel != null)
            furniturePanel.SetActive(floorDetected);

        if (notify && hadSelection)
            SelectionChanged?.Invoke(null);
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

        // Keep the catalogue open so the user can tap a different item to swap
        // the pending prefab, and show the controls panel so Delete (cancel back
        // to browsing) is available while previewing. Pinch/twist resize and
        // rotate the preview before it is placed.
        if (furniturePanel != null)
            furniturePanel.SetActive(true);

        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }

    /// <summary>
    /// Rotates the selected piece around its footprint centre. No longer wired
    /// to a button (twist replaces it); kept for accessibility or testing.
    /// </summary>
    public void RotateSelected(float degrees)
    {
        if (currentSelection == null || IsManipulating)
            return;

        currentSelection.ApplyFootprintPose(
            currentSelection.FootprintCenter,
            currentSelection.Yaw + degrees,
            currentSelection.ScaleFactor);
    }

    /// <summary>
    /// Resizes the pending preview, or the selected piece, by a factor. No longer
    /// wired to a button (pinch replaces it); kept for accessibility or testing.
    /// </summary>
    public void ScaleSelected(float factor)
    {
        if (factor <= 0f || IsManipulating)
            return;

        if (placementPreview != null)
        {
            previewScaleFactor = Mathf.Clamp(previewScaleFactor * factor, minimumScale, maximumScale);
            return;
        }

        if (currentSelection != null)
        {
            currentSelection.ApplyFootprintPose(
                currentSelection.FootprintCenter,
                currentSelection.Yaw,
                Mathf.Clamp(currentSelection.ScaleFactor * factor, minimumScale, maximumScale));
        }
    }

    /// <summary>
    /// Called by the Delete button. While a placement is pending, this
    /// cancels it and returns to the catalogue. Otherwise it deletes the
    /// currently selected, already-placed furniture.
    /// </summary>
    public void DeleteSelected()
    {
        CancelGesture();

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
        CancelGesture();
        DeselectCurrent();

        foreach (GameObject furniture in placedFurniture)
        {
            if (furniture != null)
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
            RemoveAnchor(anchor);
        else
            Destroy(furniture);
    }

    private void RemoveAnchor(ARAnchor anchor)
    {
        if (anchorManager != null)
            anchorManager.TryRemoveAnchor(anchor);

        if (anchor != null)
            Destroy(anchor.gameObject);
    }
}

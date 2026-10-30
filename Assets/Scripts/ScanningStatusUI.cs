using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// One-line hint at the top of the AR screen: AR start-up and scanning state
/// first, then what the user can do right now (place, adjust, browse).
/// Assigning the same string again is free (TextMeshPro ignores unchanged
/// text), so setting it every frame costs nothing.
/// </summary>
public class ScanningStatusUI : MonoBehaviour
{
    public ARPlaneManager planeManager;
    public TMP_Text statusText;

    [Tooltip("Optional. When set, hints change while placing or adjusting a piece.")]
    public TouchInteractionManager interactionManager;

    private void Awake()
    {
        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<TouchInteractionManager>();
    }

    private void Update()
    {
        if (planeManager == null || statusText == null)
            return;

        if (ARSession.state == ARSessionState.Unsupported)
        {
            statusText.text = "This device does not support ARCore";
            return;
        }

        if (ARSession.state != ARSessionState.SessionTracking)
        {
            statusText.text = "Starting AR camera...";
            return;
        }

        if (!HasHorizontalPlane())
        {
            statusText.text = "Scanning... slowly move your phone over the floor";
            return;
        }

        if (interactionManager != null)
        {
            if (interactionManager.IsPlacementPending)
            {
                statusText.text = "Tap the floor to place  •  pinch to resize  •  twist to rotate";
                return;
            }

            if (interactionManager.CurrentSelection != null)
            {
                statusText.text = "Drag to move  •  pinch to resize  •  twist to rotate";
                return;
            }
        }

        statusText.text = "Floor detected  •  choose an item from the catalog";
    }

    private bool HasHorizontalPlane()
    {
        if (interactionManager != null && interactionManager.FloorDetected)
            return true;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane.trackingState == TrackingState.Tracking &&
                plane.alignment == PlaneAlignment.HorizontalUp)
                return true;
        }
        return false;
    }
}

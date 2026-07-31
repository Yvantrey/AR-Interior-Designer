using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ScanningStatusUI : MonoBehaviour
{
    public ARPlaneManager planeManager;
    public TMP_Text statusText;

    void Update()
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

        int horizontalPlaneCount = 0;
        foreach (var plane in planeManager.trackables)
        {
            if (plane.trackingState == TrackingState.Tracking &&
                plane.alignment == PlaneAlignment.HorizontalUp)
            {
                horizontalPlaneCount++;
            }
        }

        statusText.text = horizontalPlaneCount > 0
            ? "Floor detected - choose furniture, then tap the floor to place it"
            : "Scanning... slowly move your phone over the floor";
    }
}

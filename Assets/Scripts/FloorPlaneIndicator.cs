using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Draws a small ring on the floor under the centre of the screen. It is only
/// visible when ARCore has a tracked, horizontal plane that can accept furniture.
/// </summary>
public class FloorPlaneIndicator : MonoBehaviour
{
    private const int SegmentCount = 40;
    private const float Radius = 0.25f;

    private readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    private ARRaycastManager raycastManager;
    private ARPlaneManager planeManager;
    private LineRenderer line;

    public void Initialize(ARRaycastManager raycast, ARPlaneManager planes)
    {
        raycastManager = raycast;
        planeManager = planes;
        CreateRing();
    }

    private void CreateRing()
    {
        if (line != null)
            return;

        GameObject ring = new GameObject("Floor Placement Indicator");
        ring.transform.SetParent(transform, false);

        line = ring.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = SegmentCount;
        line.startWidth = 0.018f;
        line.endWidth = 0.018f;
        line.numCapVertices = 4;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(0.2f, 0.95f, 1f, 0.95f);
        line.endColor = line.startColor;

        for (int i = 0; i < SegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / SegmentCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * Radius, 0f, Mathf.Sin(angle) * Radius));
        }

        ring.SetActive(false);
    }

    private void Update()
    {
        if (line == null || raycastManager == null)
            return;

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        bool hasFloorHit = raycastManager.Raycast(center, hits, TrackableType.PlaneWithinPolygon) &&
                           IsHorizontalFloor(hits[0]);

        line.gameObject.SetActive(hasFloorHit);
        if (!hasFloorHit)
            return;

        Pose pose = hits[0].pose;
        line.transform.SetPositionAndRotation(pose.position + Vector3.up * 0.01f, Quaternion.identity);
    }

    private bool IsHorizontalFloor(ARRaycastHit hit)
    {
        if (planeManager == null)
            return true;

        ARPlane plane = planeManager.GetPlane(hit.trackableId);
        return plane != null && plane.trackingState == TrackingState.Tracking &&
               plane.alignment == PlaneAlignment.HorizontalUp;
    }
}

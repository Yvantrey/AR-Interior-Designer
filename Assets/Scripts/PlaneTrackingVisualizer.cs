using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Outlines every currently tracked horizontal plane so the user can see how
/// much of the floor ARCore has actually locked onto, instead of only the
/// small placement reticle at the centre of the screen.
/// </summary>
public class PlaneTrackingVisualizer : MonoBehaviour
{
    [SerializeField] private Color trackedColor = new Color(0.2f, 0.95f, 1f, 0.5f);
    [SerializeField] private float lineWidth = 0.01f;

    private ARPlaneManager planeManager;
    private readonly Dictionary<TrackableId, LineRenderer> outlines = new Dictionary<TrackableId, LineRenderer>();
    private readonly List<TrackableId> staleIds = new List<TrackableId>();
    private readonly HashSet<TrackableId> seenIds = new HashSet<TrackableId>();

    public void Initialize(ARPlaneManager planes)
    {
        planeManager = planes;
    }

    private void Update()
    {
        if (planeManager == null)
            return;

        seenIds.Clear();

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane.alignment != PlaneAlignment.HorizontalUp)
                continue;

            seenIds.Add(plane.trackableId);

            bool isTracking = plane.trackingState == TrackingState.Tracking;
            LineRenderer line = GetOrCreateOutline(plane.trackableId);
            line.gameObject.SetActive(isTracking);
            if (isTracking)
                DrawRectangle(line, plane);
        }

        staleIds.Clear();
        foreach (KeyValuePair<TrackableId, LineRenderer> entry in outlines)
        {
            if (!seenIds.Contains(entry.Key))
                staleIds.Add(entry.Key);
        }

        foreach (TrackableId id in staleIds)
        {
            Destroy(outlines[id].gameObject);
            outlines.Remove(id);
        }
    }

    private LineRenderer GetOrCreateOutline(TrackableId id)
    {
        if (outlines.TryGetValue(id, out LineRenderer existing))
            return existing;

        GameObject outline = new GameObject("Plane Outline");
        outline.transform.SetParent(transform, false);

        LineRenderer line = outline.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.startWidth = lineWidth;
        line.endWidth = lineWidth;
        line.numCapVertices = 2;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = trackedColor;
        line.endColor = trackedColor;

        outlines.Add(id, line);
        return line;
    }

    private static void DrawRectangle(LineRenderer line, ARPlane plane)
    {
        Vector3 center = plane.center;
        Vector3 right = plane.transform.right * (plane.size.x * 0.5f);
        Vector3 forward = plane.transform.forward * (plane.size.y * 0.5f);
        Vector3 up = Vector3.up * 0.005f; // lift slightly to avoid z-fighting with the floor

        line.SetPosition(0, center - right - forward + up);
        line.SetPosition(1, center + right - forward + up);
        line.SetPosition(2, center + right + forward + up);
        line.SetPosition(3, center - right + forward + up);
    }
}

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders a prefab to a transparent PNG-ready texture in an isolated preview
/// scene (nothing is added to the open scene). Used for catalog thumbnails and
/// for the prefab survey.
///
/// Transparency is recovered by rendering twice, over black and over white:
/// a pixel's alpha is 1 - (white - black). This works whatever the render
/// pipeline does with the camera's alpha channel (HDR targets drop it).
/// </summary>
public static class PrefabThumbnailRenderer
{
    private const float FieldOfView = 24f;

    /// <param name="yaw">Camera heading in degrees. 0 looks along +Z (sees the object's -Z side); 180 looks along -Z.</param>
    /// <param name="pitch">Camera tilt down in degrees.</param>
    /// <param name="padding">Empty margin around the object, as a fraction of the image (0.06 = 6 %).</param>
    /// <param name="directionMarkers">Adds a blue block on the object's +Z side and a red one on +X (survey only).</param>
    public static Texture2D Render(GameObject prefab, int size, float yaw, float pitch, float padding, bool directionMarkers)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = Vector3.zero;

            Bounds bounds = WorldBounds(instance);
            if (directionMarkers)
                AddDirectionMarkers(scene, bounds);
            if (directionMarkers)
                bounds = WorldBounds(instance.scene);

            AddLights(scene, bounds);

            GameObject cameraObject = new GameObject("Thumbnail Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.cameraType = CameraType.Game;
            camera.fieldOfView = FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            FrameBounds(camera, bounds, yaw, pitch, padding);

            Color32[] overBlack = RenderOnce(camera, size, Color.black);
            Color32[] overWhite = RenderOnce(camera, size, Color.white);

            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[overBlack.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 b = overBlack[i];
                Color32 w = overWhite[i];
                int coverage = 255 - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3;
                coverage = Mathf.Clamp(coverage, 0, 255);
                if (coverage == 0)
                {
                    pixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                // Over black the colour is premultiplied by coverage; undo that.
                float k = 255f / coverage;
                pixels[i] = new Color32(
                    (byte)Mathf.Min(255f, b.r * k),
                    (byte)Mathf.Min(255f, b.g * k),
                    (byte)Mathf.Min(255f, b.b * k),
                    (byte)coverage);
            }
            result.SetPixels32(pixels);
            result.Apply();
            return result;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    /// <summary>Combined renderer bounds of an object in world space (zero-size bounds at its position if it has none).</summary>
    public static Bounds WorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in renderers)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer))
                continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    private static Bounds WorldBounds(Scene scene)
    {
        Bounds bounds = default;
        bool any = false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<Renderer>() == null)
                continue;
            Bounds b = WorldBounds(root);
            if (!any) { bounds = b; any = true; }
            else bounds.Encapsulate(b);
        }
        return bounds;
    }

    private static Color32[] RenderOnce(Camera camera, int size, Color background)
    {
        RenderTexture target = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.backgroundColor = background;
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            Texture2D readback = new Texture2D(size, size, TextureFormat.RGBA32, false);
            readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            readback.Apply();
            Color32[] pixels = readback.GetPixels32();
            Object.DestroyImmediate(readback);
            return pixels;
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
    }

    /// <summary>
    /// Points the camera at the bounds from the given angle, then moves it so
    /// the bounds' eight corners just fit inside the frame (minus padding).
    /// </summary>
    private static void FrameBounds(Camera camera, Bounds bounds, float yaw, float pitch, float padding)
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 forward = rotation * Vector3.forward;
        float radius = Mathf.Max(bounds.extents.magnitude, 0.01f);
        float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);

        Vector3[] corners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            corners[i] = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
        }

        // A few passes: measure how much of the frame the corners use, re-aim at
        // their projected centre, then rescale the distance to fit.
        Vector3 target = bounds.center;
        for (int pass = 0; pass < 3; pass++)
        {
            camera.transform.SetPositionAndRotation(target - forward * distance, rotation);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 1.5f);
            camera.farClipPlane = distance + radius * 1.5f;
            camera.aspect = 1f;

            float minX = 1f, maxX = 0f, minY = 1f, maxY = 0f;
            foreach (Vector3 corner in corners)
            {
                Vector3 v = camera.WorldToViewportPoint(corner);
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }

            // Re-centre on the projected box so the object sits in the middle of the image.
            Vector3 centreWorld = camera.ViewportToWorldPoint(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, distance));
            Vector3 shift = centreWorld - target;
            target += shift - Vector3.Project(shift, forward);

            float halfExtent = Mathf.Max(maxX - minX, maxY - minY) * 0.5f;
            distance *= halfExtent / (0.5f - padding);
        }
        camera.transform.SetPositionAndRotation(target - forward * distance, rotation);
    }

    private static void AddLights(Scene scene, Bounds bounds)
    {
        // Key light (URP's main light) from upper front-left, plus two soft fills.
        NewLight(scene, "Key", LightType.Directional, Quaternion.Euler(42f, 140f, 0f), 1.25f, new Color(1f, 0.97f, 0.92f));
        NewLight(scene, "Fill", LightType.Directional, Quaternion.Euler(20f, -60f, 0f), 0.55f, new Color(0.85f, 0.9f, 1f));
        NewLight(scene, "Top", LightType.Directional, Quaternion.Euler(85f, 0f, 0f), 0.35f, Color.white);
    }

    private static void NewLight(Scene scene, string name, LightType type, Quaternion rotation, float intensity, Color color)
    {
        GameObject go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.rotation = rotation;
        Light light = go.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
    }

    private static void AddDirectionMarkers(Scene scene, Bounds bounds)
    {
        float s = Mathf.Max(0.05f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.12f);
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        NewMarker(scene, unlit, new Color(0.2f, 0.45f, 1f),
            new Vector3(bounds.center.x, bounds.min.y + s * 0.5f, bounds.max.z + s * 1.2f), s);
        NewMarker(scene, unlit, new Color(1f, 0.25f, 0.25f),
            new Vector3(bounds.max.x + s * 1.2f, bounds.min.y + s * 0.5f, bounds.center.z), s * 0.7f);
    }

    private static void NewMarker(Scene scene, Shader shader, Color color, Vector3 position, float size)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SceneManager.MoveGameObjectToScene(cube, scene);
        cube.transform.position = position;
        cube.transform.localScale = Vector3.one * size;
        Material material = new Material(shader);
        material.SetColor("_BaseColor", color);
        cube.GetComponent<Renderer>().sharedMaterial = material;
    }
}

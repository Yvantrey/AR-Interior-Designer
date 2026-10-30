using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Developer tool: renders every furniture prefab in the imported packs with
/// direction markers (blue = +Z side, red = +X side) and writes their sizes
/// to sizes.csv. Used to check scale and which way each model faces before
/// laying out room sets. Output goes to Temp/CatalogSurvey (not imported).
///
/// Batch mode: Unity.exe -batchmode -projectPath . -executeMethod CatalogSurvey.RunBatch -quit
/// </summary>
public static class CatalogSurvey
{
    public static readonly string[] PackFolders =
    {
        "Assets/Furniture/Prefabs",
        "Assets/Modern Furniture/Prefabs",
        "Assets/MinimalistBedroom/Prefabs/Interior",
        "Assets/diningRoom/diningRoom/prefabs",
        "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/Furniture",
        "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/Fast Food Build Kit/Counter Top",
        "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/Fast Food Build Kit/Divider",
        "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/Props/Electronics",
        "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/Props/Misc",
        "Assets/Brick Project Studio/Fast Food Restaurant Kit/_Prefabs/Props/Tray Setup",
        "Assets/Brick Project Studio/_BPS Basic Assets/_Prefabs/Basic Asset/Furniture_Props",
    };

    [MenuItem("Tools/AR Furniture Designer/Developer/Survey Furniture Prefabs")]
    public static void RunMenu()
    {
        string folder = Run(Path.Combine("Temp", "CatalogSurvey"), true);
        EditorUtility.RevealInFinder(folder);
    }

    public static void RunBatch()
    {
        string output = Path.Combine("Temp", "CatalogSurvey");
        string list = null;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-surveyOut")
                output = args[i + 1];
            if (args[i] == "-surveyList")
                list = args[i + 1];
        }

        if (list != null)
            RunFourViews(File.ReadAllLines(list), output);
        else
            Run(output, true);
    }

    /// <summary>
    /// Renders each listed prefab from its +Z, +X, -Z and -X sides (in that
    /// order) as one 4-panel strip, to see unambiguously which side is the front.
    /// </summary>
    public static void RunFourViews(string[] prefabPaths, string outputFolder)
    {
        Directory.CreateDirectory(outputFolder);
        const int size = 160;
        // Camera yaw that looks AT each side: from +Z the camera looks along -Z (yaw 180), etc.
        float[] yaws = { 180f, 270f, 0f, 90f };
        int index = 0;
        foreach (string raw in prefabPaths)
        {
            string path = raw.Trim();
            if (path.Length == 0)
                continue;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("[CatalogSurvey] Missing prefab: " + path);
                continue;
            }

            Texture2D strip = new Texture2D(size * 4, size, TextureFormat.RGBA32, false);
            for (int v = 0; v < 4; v++)
            {
                Texture2D view = PrefabThumbnailRenderer.Render(prefab, size, yaws[v], 15f, 0.06f, false);
                strip.SetPixels(v * size, 0, size, size, view.GetPixels());
                Object.DestroyImmediate(view);
            }
            strip.Apply();
            string file = string.Format("{0:000}_{1}.png", index++, Sanitize(Path.GetFileNameWithoutExtension(path)));
            File.WriteAllBytes(Path.Combine(outputFolder, file), strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
        }
        Debug.Log("[CatalogSurvey] Rendered four views of " + index + " prefabs to " + Path.GetFullPath(outputFolder));
    }

    public static string Run(string outputFolder, bool convertMaterials)
    {
        if (convertMaterials)
            BuiltinMaterialConverter.Convert(BuiltinMaterialConverter.DefaultFolders);

        Directory.CreateDirectory(outputFolder);
        StringBuilder csv = new StringBuilder("path,sizeX,sizeY,sizeZ,centerX,minY,centerZ\n");

        List<string> folders = new List<string>();
        foreach (string folder in PackFolders)
        {
            if (AssetDatabase.IsValidFolder(folder))
                folders.Add(folder);
        }
        int index = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<Renderer>() == null)
                continue;

            GameObject probe = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            probe.transform.position = Vector3.zero;
            Bounds b = PrefabThumbnailRenderer.WorldBounds(probe);
            Object.DestroyImmediate(probe);

            csv.AppendLine(string.Format(CultureInfo.InvariantCulture, "\"{0}\",{1:F3},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3}",
                path, b.size.x, b.size.y, b.size.z, b.center.x, b.min.y, b.center.z));

            Texture2D image = PrefabThumbnailRenderer.Render(prefab, 256, 150f, 25f, 0.08f, true);
            string file = string.Format("{0:000}_{1}.png", index++, Sanitize(Path.GetFileNameWithoutExtension(path)));
            File.WriteAllBytes(Path.Combine(outputFolder, file), image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        File.WriteAllText(Path.Combine(outputFolder, "sizes.csv"), csv.ToString());
        Debug.Log("[CatalogSurvey] Rendered " + index + " prefabs to " + Path.GetFullPath(outputFolder));
        return Path.GetFullPath(outputFolder);
    }

    private static string Sanitize(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name.Replace(' ', '_');
    }
}

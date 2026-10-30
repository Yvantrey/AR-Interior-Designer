using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Editor menu that sets the app icon in Player Settings from the PNGs in
/// Assets/UI/AppIcon: the Default Icon plus every Android icon slot
/// (Adaptive background/foreground, Round and Legacy), at every size.
/// Unity resizes the 1024 px sources for each slot when it builds.
/// </summary>
public static class AppIconSetup
{
    private const string Folder = "Assets/UI/AppIcon/";
    private const string LegacyPath = Folder + "AppIcon.png";               // rounded square, transparent corners
    private const string RoundPath = Folder + "AppIcon_Round.png";          // circle, for Android 7.1 launchers
    private const string ForegroundPath = Folder + "AppIcon_Foreground.png"; // art inside the adaptive safe zone
    private const string BackgroundPath = Folder + "AppIcon_Background.png"; // plain navy

    [MenuItem("Tools/AR Furniture Designer/Apply App Icon")]
    private static void ApplyAppIcon()
    {
        Texture2D legacy = LoadIcon(LegacyPath);
        Texture2D round = LoadIcon(RoundPath);
        Texture2D foreground = LoadIcon(ForegroundPath);
        Texture2D background = LoadIcon(BackgroundPath);
        if (legacy == null || round == null || foreground == null || background == null)
        {
            EditorUtility.DisplayDialog("AR Furniture Designer",
                "Icon images are missing. Expected these files in " + Folder + ":\n" +
                "AppIcon.png, AppIcon_Round.png, AppIcon_Foreground.png, AppIcon_Background.png", "OK");
            return;
        }

        // Default Icon (top of Player Settings). Used wherever no platform icon is set.
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);

        // Adaptive icons (Android 8+) have two layers: 0 = background, 1 = foreground.
        SetAndroidIcons(AndroidPlatformIconKind.Adaptive, background, foreground);
        SetAndroidIcons(AndroidPlatformIconKind.Round, round);
        SetAndroidIcons(AndroidPlatformIconKind.Legacy, legacy);

        AssetDatabase.SaveAssets();
        Debug.Log("[AppIconSetup] App icon applied to Default Icon and all Android icon slots.");
        EditorUtility.DisplayDialog("AR Furniture Designer",
            "App icon applied.\n\nCheck it in Edit > Project Settings > Player > Android tab > Icon.", "OK");
    }

    private static void SetAndroidIcons(PlatformIconKind kind, params Texture2D[] layers)
    {
        PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
        foreach (PlatformIcon icon in icons)
        {
            icon.SetTextures(layers);
        }
        PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
    }

    /// <summary>
    /// Loads an icon PNG, first fixing its import settings so Unity keeps it at
    /// full quality (no compression or mipmaps, which can blur or band icons).
    /// </summary>
    private static Texture2D LoadIcon(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool changed = importer.textureType != TextureImporterType.Default ||
                           importer.mipmapEnabled ||
                           !importer.alphaIsTransparency ||
                           importer.maxTextureSize != 1024 ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (changed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}

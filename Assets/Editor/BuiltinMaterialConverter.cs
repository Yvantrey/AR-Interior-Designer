using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts materials that use the Built-in pipeline's Standard shader to URP Lit,
/// copying colour, textures, smoothness, metallic, normal map, emission and
/// transparency. Without this they render pink under URP. The Polygon Dining
/// Room pack ships with Standard materials, so the catalog builder runs this
/// on it automatically; it is also available as its own menu item.
/// </summary>
public static class BuiltinMaterialConverter
{
    public static readonly string[] DefaultFolders = { "Assets/diningRoom", "Assets/Brick Project Studio" };

    private const string LitShaderName = "Universal Render Pipeline/Lit";

    [MenuItem("Tools/AR Furniture Designer/Convert Built-in Materials to URP")]
    private static void ConvertMenu()
    {
        int count = Convert(DefaultFolders);
        EditorUtility.DisplayDialog("AR Furniture Designer",
            count == 0 ? "No Built-in Standard materials were found. Nothing to convert."
                       : count + " material(s) converted to URP Lit.", "OK");
    }

    /// <summary>Converts every Standard material under the folders. Returns how many were converted.</summary>
    public static int Convert(IEnumerable<string> folders)
    {
        Shader lit = Shader.Find(LitShaderName);
        if (lit == null)
        {
            Debug.LogError("[MaterialConverter] URP Lit shader not found.");
            return 0;
        }

        List<string> existing = new List<string>();
        foreach (string folder in folders)
        {
            if (AssetDatabase.IsValidFolder(folder))
                existing.Add(folder);
        }
        if (existing.Count == 0)
            return 0;

        int converted = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Material", existing.ToArray()))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null)
                continue;

            string shaderName = material.shader.name;
            if (shaderName != "Standard" && shaderName != "Standard (Specular setup)")
                continue;

            ConvertOne(material, lit);
            EditorUtility.SetDirty(material);
            converted++;
        }

        if (converted > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[MaterialConverter] Converted " + converted + " Standard material(s) to URP Lit.");
        }
        return converted;
    }

    private static void ConvertOne(Material material, Shader lit)
    {
        // Read everything before switching shader (properties not on the new
        // shader are still stored, but reading first keeps this explicit).
        Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
        Texture mainTex = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
        Vector2 tiling = material.HasProperty("_MainTex") ? material.GetTextureScale("_MainTex") : Vector2.one;
        Vector2 offset = material.HasProperty("_MainTex") ? material.GetTextureOffset("_MainTex") : Vector2.zero;
        float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
        float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
        Texture metallicMap = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
        Texture normalMap = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
        float normalScale = material.HasProperty("_BumpScale") ? material.GetFloat("_BumpScale") : 1f;
        Texture occlusion = material.HasProperty("_OcclusionMap") ? material.GetTexture("_OcclusionMap") : null;
        Color emission = material.HasProperty("_EmissionColor") ? material.GetColor("_EmissionColor") : Color.black;
        Texture emissionMap = material.HasProperty("_EmissionMap") ? material.GetTexture("_EmissionMap") : null;
        bool emissive = material.IsKeywordEnabled("_EMISSION") && (emission.maxColorComponent > 0.001f || emissionMap != null);
        float mode = material.HasProperty("_Mode") ? material.GetFloat("_Mode") : 0f; // 0 opaque, 1 cutout, 2 fade, 3 transparent
        float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;

        material.shader = lit;
        material.shaderKeywords = new string[0];

        material.SetColor("_BaseColor", color);
        material.SetTexture("_BaseMap", mainTex);
        material.SetTextureScale("_BaseMap", tiling);
        material.SetTextureOffset("_BaseMap", offset);
        material.SetFloat("_Smoothness", smoothness);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_WorkflowMode", 1f); // metallic

        if (metallicMap != null)
        {
            material.SetTexture("_MetallicGlossMap", metallicMap);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", normalMap);
            material.SetFloat("_BumpScale", normalScale);
            material.EnableKeyword("_NORMALMAP");
        }
        if (occlusion != null)
        {
            material.SetTexture("_OcclusionMap", occlusion);
            material.EnableKeyword("_OCCLUSIONMAP");
        }
        if (emissive)
        {
            material.SetColor("_EmissionColor", emission);
            material.SetTexture("_EmissionMap", emissionMap);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        bool transparent = mode >= 2f;
        bool cutout = Mathf.Approximately(mode, 1f);

        material.SetFloat("_Surface", transparent ? 1f : 0f);
        material.SetFloat("_Blend", 0f); // alpha blend
        material.SetFloat("_AlphaClip", cutout ? 1f : 0f);
        material.SetFloat("_Cutoff", cutoff);
        material.SetFloat("_Cull", 2f);

        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
        }
        else
        {
            material.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.SetFloat("_ZWrite", 1f);
            if (cutout)
                material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = cutout
                ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest
                : (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.SetShaderPassEnabled("DepthOnly", true);
            material.SetShaderPassEnabled("ShadowCaster", true);
        }
    }
}

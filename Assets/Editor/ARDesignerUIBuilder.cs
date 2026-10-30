using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Editor menu that builds the app's new UI directly into the open scene,
/// styled with the Figma prototype's tokens (figma-plugin/code.js). Everything
/// it creates is normal UI you can tweak afterwards in the Inspector.
/// </summary>
public static class ARDesignerUIBuilder
{
    // Figma design tokens
    public static readonly Color Teal = Hex(0x0E8A9C);
    public static readonly Color Cyan = Hex(0x2FD6E6);
    public static readonly Color Gold = Hex(0xEFC65E);
    public static readonly Color Navy = Hex(0x081E2A);
    public static readonly Color Ink = Hex(0x3A2A05);
    public static readonly Color SheetColor = Hex(0x0F3140);
    public static readonly Color BodyText = Hex(0xDBEAEE);
    public static readonly Color MutedText = Hex(0xB9D0D7);
    public static readonly Color Ghost = new Color(1f, 1f, 1f, 0.16f);

    private static readonly Color RedChannel = Hex(0xE5484D);
    private static readonly Color GreenChannel = Hex(0x30A46C);
    private static readonly Color BlueChannel = Hex(0x3E7BFA);

    private const string OverlayCanvasName = "AR Overlay Canvas";
    private const string RoundedSpritePath = "Assets/UI/ARD_Rounded.png";
    private const int RoundedSpriteRadius = 32;

    //=========================================================
    // Menu
    //=========================================================

    [MenuItem("Tools/AR Furniture Designer/Create Color Picker UI")]
    private static void CreateColorPickerUI()
    {
        TouchInteractionManager manager = Object.FindFirstObjectByType<TouchInteractionManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("AR Furniture Designer",
                "Open MainScene first. No TouchInteractionManager was found in the open scene.", "OK");
            return;
        }

        if (Object.FindFirstObjectByType<ColorPickerPanel>() != null &&
            !EditorUtility.DisplayDialog("AR Furniture Designer",
                "A Color Picker already exists in this scene. Create another one?", "Create", "Cancel"))
        {
            return;
        }

        ColorPickerPanel panel = BuildColorPicker(manager);
        Undo.RegisterCreatedObjectUndo(panel.gameObject, "Create Color Picker UI");
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Selection.activeGameObject = panel.gameObject;

        Debug.Log("Color Picker UI created under '" + OverlayCanvasName + "'. Move the 'Color Button' if it overlaps your existing controls.");
    }

    /// <summary>Builds the colour picker (open button + sheet) on the overlay canvas, without dialogs.</summary>
    public static ColorPickerPanel BuildColorPicker(TouchInteractionManager manager)
    {
        Sprite rounded = GetOrCreateRoundedSprite();
        Canvas canvas = GetOrCreateOverlayCanvas();

        // Root: full-screen, no graphic, so it never blocks touches by itself.
        RectTransform root = NewRect("Color Picker", canvas.transform);
        Stretch(root);
        ColorPickerPanel panel = root.gameObject.AddComponent<ColorPickerPanel>();
        panel.interactionManager = manager;

        BuildOpenButton(root, rounded, panel);
        BuildSheet(root, rounded, panel);
        return panel;
    }

    //=========================================================
    // Color picker pieces
    //=========================================================

    /// <summary>Round "Color" control in the style of the Figma Rotate/Remove buttons.</summary>
    private static void BuildOpenButton(RectTransform parent, Sprite rounded, ColorPickerPanel panel)
    {
        RectTransform buttonRoot = NewRect("Color Button", parent);
        Place(buttonRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-14f, 240f), new Vector2(60f, 76f));

        Image circle = NewImage("Circle", buttonRoot, Ghost, rounded, 23f);
        PlaceTop(circle.rectTransform, 7f, 8f, 46f, 46f);

        Image dot = NewImage("Swatch Dot", circle.rectTransform, Color.white, rounded, 11f);
        Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        dot.raycastTarget = false;

        // Child of the circle (not the root) so it hides together with the button
        // when nothing is selected; ColorPickerPanel toggles the circle's object.
        TextMeshProUGUI label = NewText("Label", circle.rectTransform, "Color", 11f, true, Color.white, TextAlignmentOptions.Center);
        PlaceTop(label.rectTransform, -7f, 50f, 60f, 16f);

        Button button = circle.gameObject.AddComponent<Button>();
        button.targetGraphic = circle;

        panel.openButton = button;
        panel.openButtonSwatch = dot;
    }

    private static void BuildSheet(RectTransform parent, Sprite rounded, ColorPickerPanel panel)
    {
        const float visibleHeight = 400f;
        const float radius = 28f;

        // Extends `radius` below the screen so only the top corners look rounded.
        Image sheet = NewImage("Color Sheet", parent, SheetColor, rounded, radius);
        RectTransform rt = sheet.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, -radius);
        rt.sizeDelta = new Vector2(0f, visibleHeight + radius);
        sheet.raycastTarget = true; // touches on the sheet never reach the AR view

        // Content area matches the visible part of the sheet.
        RectTransform content = NewRect("Content", rt);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 1f);
        content.offsetMin = new Vector2(0f, radius);
        content.offsetMax = Vector2.zero;

        Image grabber = NewImage("Grabber", content, new Color(1f, 1f, 1f, 0.3f), rounded, 3f);
        Place(grabber.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(40f, 5f));
        grabber.raycastTarget = false;

        TextMeshProUGUI title = NewText("Title", content, "Furniture color", 20f, true, Color.white, TextAlignmentOptions.Left);
        PlaceTop(title.rectTransform, 24f, 24f, 240f, 28f);

        // Swatch with a light border (a slightly larger white square behind it).
        Image border = NewImage("Swatch Border", content, new Color(1f, 1f, 1f, 0.4f), rounded, 15f);
        PlaceTop(border.rectTransform, 22f, 66f, 60f, 60f);
        border.raycastTarget = false;
        Image swatch = NewImage("Swatch", border.rectTransform, Color.white, rounded, 13f);
        Place(swatch.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
        swatch.raycastTarget = false;

        TextMeshProUGUI valueLabel = NewText("Value Label", content, "RGB(255, 255, 255)  /  #FFFFFF", 16f, true, Color.white, TextAlignmentOptions.Left);
        PlaceTop(valueLabel.rectTransform, 96f, 68f, 280f, 24f);

        TextMeshProUGUI hexCaption = NewText("HEX Caption", content, "HEX", 13f, true, MutedText, TextAlignmentOptions.Left);
        PlaceTop(hexCaption.rectTransform, 96f, 100f, 40f, 24f);

        TMP_InputField hexInput = NewHexInput(content, rounded);
        PlaceTop((RectTransform)hexInput.transform, 136f, 96f, 130f, 32f);

        panel.sheet = sheet.gameObject;
        panel.swatch = swatch;
        panel.valueLabel = valueLabel;
        panel.hexInput = hexInput;

        panel.redSlider = NewChannelRow(content, rounded, "R", RedChannel, 150f, out panel.redValue);
        panel.greenSlider = NewChannelRow(content, rounded, "G", GreenChannel, 200f, out panel.greenValue);
        panel.blueSlider = NewChannelRow(content, rounded, "B", BlueChannel, 250f, out panel.blueValue);

        panel.resetButton = NewPillButton("Reset Color Button", content, rounded, "Reset color", false);
        PlaceTop((RectTransform)panel.resetButton.transform, 24f, 316f, 165f, 50f);

        panel.doneButton = NewPillButton("Done Button", content, rounded, "Done", true);
        PlaceTop((RectTransform)panel.doneButton.transform, 201f, 316f, 165f, 50f);

        sheet.gameObject.SetActive(false);
    }

    private static Slider NewChannelRow(RectTransform parent, Sprite rounded, string channel, Color fillColor, float top, out TMP_Text valueText)
    {
        TextMeshProUGUI letter = NewText(channel + " Label", parent, channel, 16f, true, Color.white, TextAlignmentOptions.Left);
        PlaceTop(letter.rectTransform, 24f, top, 24f, 32f);

        Slider slider = NewSlider(channel + " Slider", parent, rounded, fillColor);
        PlaceTop((RectTransform)slider.transform, 52f, top, 262f, 32f);

        TextMeshProUGUI value = NewText(channel + " Value", parent, "255", 15f, true, Color.white, TextAlignmentOptions.Right);
        PlaceTop(value.rectTransform, 318f, top, 48f, 32f);

        valueText = value;
        return slider;
    }

    //=========================================================
    // Generic styled controls (reused by Stage B screens)
    //=========================================================

    public static Slider NewSlider(string name, RectTransform parent, Sprite rounded, Color fillColor)
    {
        const float knob = 28f;
        const float track = 8f;

        RectTransform root = NewRect(name, parent);

        Image background = NewImage("Track", root, new Color(1f, 1f, 1f, 0.18f), rounded, track * 0.5f);
        background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        background.rectTransform.sizeDelta = new Vector2(0f, track);
        background.rectTransform.anchoredPosition = Vector2.zero;
        background.raycastTarget = true; // whole row is draggable, not just the knob

        // Fill area is inset by half a knob on each side so the fill ends under the knob centre.
        RectTransform fillArea = NewRect("Fill Area", root);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(knob * 0.5f, -track * 0.5f);
        fillArea.offsetMax = new Vector2(-knob * 0.5f, track * 0.5f);

        Image fill = NewImage("Fill", fillArea, fillColor, rounded, track * 0.5f);
        fill.rectTransform.sizeDelta = new Vector2(knob, 0f); // reaches back to the track start
        fill.raycastTarget = false;

        RectTransform handleArea = NewRect("Handle Slide Area", root);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(knob * 0.5f, 2f);
        handleArea.offsetMax = new Vector2(-knob * 0.5f, -2f);

        Image handle = NewImage("Handle", handleArea, Color.white, rounded, knob * 0.5f);
        handle.rectTransform.sizeDelta = new Vector2(knob, 0f);

        Slider slider = root.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 255f;
        slider.wholeNumbers = true;
        slider.value = 255f;
        return slider;
    }

    public static Button NewPillButton(string name, RectTransform parent, Sprite rounded, string label, bool gold)
    {
        Image image = NewImage(name, parent, gold ? Gold : Ghost, rounded, 25f);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = NewText("Label", image.rectTransform, label, gold ? 17f : 15f, true,
            gold ? Ink : Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_InputField NewHexInput(RectTransform parent, Sprite rounded)
    {
        Image background = NewImage("HEX Input", parent, Ghost, rounded, 16f);
        RectTransform root = background.rectTransform;

        RectTransform textArea = NewRect("Text Area", root);
        Stretch(textArea);
        textArea.offsetMin = new Vector2(12f, 4f);
        textArea.offsetMax = new Vector2(-12f, -4f);
        textArea.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = NewText("Placeholder", textArea, "#RRGGBB", 15f, false,
            new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Center);
        Stretch(placeholder.rectTransform);

        TextMeshProUGUI text = NewText("Text", textArea, "", 15f, true, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.characterLimit = 7;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.contentType = TMP_InputField.ContentType.Standard;
        input.caretColor = Cyan;
        input.customCaretColor = true;
        input.selectionColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.4f);
        input.fontAsset = text.font;
        input.pointSize = 15f;
        return input;
    }

    //=========================================================
    // Low-level helpers
    //=========================================================

    public static Canvas GetOrCreateOverlayCanvas()
    {
        GameObject existing = GameObject.Find(OverlayCanvasName);
        if (existing != null && existing.TryGetComponent(out Canvas existingCanvas))
            return existingCanvas;

        GameObject go = new GameObject(OverlayCanvasName,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = LayerMask.NameToLayer("UI");
        Undo.RegisterCreatedObjectUndo(go, "Create " + OverlayCanvasName);

        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above the existing Canvas

        // Same 390 x 800 frame as the Figma prototype, scaled to the phone's width,
        // so positions here match the Figma layout one-to-one.
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(390f, 800f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0f;
        scaler.referencePixelsPerUnit = 100f;

        return canvas;
    }

    /// <summary>
    /// A white 66x66 sprite with 32 px rounded corners, 9-sliced. With Image
    /// type Sliced it draws pills, circles and rounded rectangles; the corner
    /// radius is controlled through pixelsPerUnitMultiplier (see NewImage).
    /// </summary>
    public static Sprite GetOrCreateRoundedSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        if (existing != null)
            return existing;

        const int size = RoundedSpriteRadius * 2 + 2;
        float r = RoundedSpriteRadius;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Distance to the nearest corner-circle centre (0 inside the straight edges).
                float px = x + 0.5f;
                float py = y + 0.5f;
                float cx = Mathf.Clamp(px, r, size - r);
                float cy = Mathf.Clamp(py, r, size - r);
                float distance = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                byte alpha = (byte)(Mathf.Clamp01(r - distance + 0.5f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(RoundedSpritePath));
        File.WriteAllBytes(RoundedSpritePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(RoundedSpritePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(RoundedSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = new Vector4(r, r, r, r);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    /// <summary>Image using the rounded sprite with the given corner radius (in canvas units).</summary>
    public static Image NewImage(string name, Transform parent, Color color, Sprite rounded, float cornerRadius)
    {
        RectTransform rt = NewRect(name, parent);
        Image image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        if (rounded != null && cornerRadius > 0f)
        {
            image.sprite = rounded;
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            // Borders are 32 px; this scales them so the visible radius is cornerRadius.
            image.pixelsPerUnitMultiplier = RoundedSpriteRadius / cornerRadius;
        }
        return image;
    }

    public static TextMeshProUGUI NewText(string name, Transform parent, string text, float size, bool bold, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rt = NewRect(name, parent);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false; // labels must never swallow touches meant for the AR view
        return tmp;
    }

    /// <summary>Positions a child by its top-left corner, Figma-style (x right, y down from parent top).</summary>
    public static void PlaceTop(RectTransform rt, float x, float y, float width, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(width, height);
    }

    public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static Color Hex(int rgb)
    {
        return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}

using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ARDesignerUIBuilder;

/// <summary>
/// Builds the app's final UI in the Figma style (390 x 800 frame), using the
/// flat sprites from the imported button pack for the main actions:
///
///  MainScene: status bar at the top, the furniture catalogue bottom sheet
///  (room tabs + item cards with thumbnails), and an action bar above it
///  (Color, Delete, Clear all). The old Canvas panels are switched off and the
///  InteractionManager / status script are re-wired to the new UI.
///
///  MainMenu: a branded start screen (app icon, tagline, feature list, Start /
///  How it works / Exit) and a step-by-step "How it works" sheet.
///
/// Both builders are safe to run again; they replace what they built before.
/// </summary>
public static class CatalogUIBuilder
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string AppIconPath = "Assets/UI/AppIcon/AppIcon.png";
    private const string GradientPath = "Assets/UI/ARD_Gradient.png";

    // Flat sprites from the button pack (the least "game-like" ones).
    private const string BlueButton = "Assets/Buttons/PNG/29button_blue.png";
    private const string RedButton = "Assets/Buttons/PNG/30button_red.png";
    // 9-slice borders (left, bottom, right, top) in pixels: 16 px corners plus
    // the darker base and drop shadow at the bottom of each sprite.
    private static readonly Vector4 ButtonBorder = new Vector4(24f, 30f, 24f, 22f);
    private const float ButtonSpriteHeight = 130f;

    private const float SheetVisibleHeight = 262f;
    private const float SheetRadius = 24f;

    private static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

    //=========================================================
    // Menu items
    //=========================================================

    [MenuItem("Tools/AR Furniture Designer/Create Catalog UI (open MainScene first)")]
    private static void BuildMainSceneMenu()
    {
        string error = BuildMainSceneUI();
        EditorUtility.DisplayDialog("AR Furniture Designer", error ?? "Catalog UI built. Save the scene (Ctrl+S).", "OK");
    }

    [MenuItem("Tools/AR Furniture Designer/Style Main Menu (open MainMenu first)")]
    private static void BuildMainMenuMenu()
    {
        string error = BuildMainMenuUI();
        EditorUtility.DisplayDialog("AR Furniture Designer", error ?? "Main menu styled. Save the scene (Ctrl+S).", "OK");
    }

    /// <summary>
    /// Batch mode: builds the catalog assets, both scenes' UI, saves them, and
    /// (with -previewOut folder) renders phone-sized previews of the screens.
    /// </summary>
    public static void BuildAllBatch()
    {
        string previewOut = null;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-previewOut")
                previewOut = args[i + 1];
        }

        if (AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(FurnitureCatalogBuilder.CatalogPath) == null)
            Debug.Log("[CatalogUI] " + FurnitureCatalogBuilder.BuildAll(null));

        EditorSceneManager.OpenScene(MainScenePath);
        string error = BuildMainSceneUI();
        if (error != null) { Debug.LogError("[CatalogUI] " + error); return; }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        EditorSceneManager.OpenScene(MainMenuPath);
        error = BuildMainMenuUI();
        if (error != null) { Debug.LogError("[CatalogUI] " + error); return; }
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Debug.Log("[CatalogUI] Both scenes built and saved.");

        if (!string.IsNullOrEmpty(previewOut))
        {
            Directory.CreateDirectory(previewOut);
            UIPreview.RenderMainScene(MainScenePath, previewOut);
            UIPreview.RenderMainMenu(MainMenuPath, previewOut);
        }
    }

    //=========================================================
    // MainScene
    //=========================================================

    public static string BuildMainSceneUI()
    {
        TouchInteractionManager manager = Object.FindFirstObjectByType<TouchInteractionManager>(FindObjectsInactive.Include);
        if (manager == null)
            return "Open Assets/Scenes/MainScene first (no TouchInteractionManager found).";

        FurnitureCatalog catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(FurnitureCatalogBuilder.CatalogPath);
        if (catalog == null)
        {
            Debug.Log("[CatalogUI] " + FurnitureCatalogBuilder.BuildAll(null));
            catalog = AssetDatabase.LoadAssetAtPath<FurnitureCatalog>(FurnitureCatalogBuilder.CatalogPath);
        }

        Sprite rounded = GetOrCreateRoundedSprite();
        Sprite blue = LoadButtonSprite(BlueButton);
        Sprite red = LoadButtonSprite(RedButton);
        Canvas canvas = GetOrCreateOverlayCanvas();

        DestroyChild(canvas.transform, "Top Bar");
        DestroyChild(canvas.transform, "Catalog");

        ColorPickerPanel colorPicker = Object.FindFirstObjectByType<ColorPickerPanel>(FindObjectsInactive.Include);
        if (colorPicker == null)
            colorPicker = BuildColorPicker(manager);

        // --- Status bar ---
        TMP_Text statusText = BuildTopBar(canvas.transform, rounded);
        ScanningStatusUI status = Object.FindFirstObjectByType<ScanningStatusUI>(FindObjectsInactive.Include);
        if (status != null)
        {
            Undo.RecordObject(status, "Wire status");
            status.statusText = statusText;
            status.interactionManager = manager;
            EditorUtility.SetDirty(status);
        }

        // --- Catalogue sheet ---
        RectTransform root = NewRect("Catalog", canvas.transform);
        Stretch(root);
        FurnitureCatalogPanel panel = root.gameObject.AddComponent<FurnitureCatalogPanel>();
        panel.catalog = catalog;
        panel.interactionManager = manager;
        BuildCatalogSheet(root, panel, rounded);

        // --- Action bar (Color, Delete, Clear all) riding on top of the sheet ---
        RectTransform bar = BuildActionBar(panel.sheet, panel, colorPicker, manager, rounded, red);

        // --- Colour sheet: pack-style Done button ---
        if (colorPicker.doneButton != null && blue != null)
            StyleAsFlatButton(colorPicker.doneButton, blue, 50f);

        // Draw order: catalogue below the colour sheet, status bar on top of both.
        root.SetSiblingIndex(colorPicker.transform.GetSiblingIndex());

        // --- Re-wire the manager and retire the old Canvas panels ---
        Undo.RecordObject(manager, "Wire catalog");
        manager.furniturePanel = root.gameObject;
        manager.controlsPanel = bar.gameObject;
        EditorUtility.SetDirty(manager);
        RetireOldCanvas(manager.gameObject.scene);

        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        return null;
    }

    private static TMP_Text BuildTopBar(Transform canvas, Sprite rounded)
    {
        Image pill = NewImage("Top Bar", canvas, Alpha(Navy, 0.84f), rounded, 20f);
        RectTransform rt = pill.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f); // clears the Android status bar
        rt.sizeDelta = new Vector2(-32f, 40f);
        pill.raycastTarget = false; // never blocks touches on the AR view

        Image dot = NewImage("Status Dot", rt, Cyan, rounded, 4f);
        Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(8f, 8f));
        dot.raycastTarget = false;

        TextMeshProUGUI text = NewText("Status Text", rt, "Starting AR camera...", 13f, false, BodyText, TextAlignmentOptions.Left);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(34f, 0f);
        text.rectTransform.offsetMax = new Vector2(-14f, 0f);
        return text;
    }

    private static void BuildCatalogSheet(RectTransform root, FurnitureCatalogPanel panel, Sprite rounded)
    {
        // The sheet hangs SheetRadius below the screen so only its top corners are rounded.
        Image sheet = NewImage("Catalog Sheet", root, Alpha(SheetColor, 0.97f), rounded, SheetRadius);
        RectTransform rt = sheet.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, -SheetRadius);
        rt.sizeDelta = new Vector2(0f, SheetVisibleHeight + SheetRadius);
        sheet.raycastTarget = true; // touches on the sheet never reach the AR view

        RectTransform content = NewRect("Content", rt);
        content.anchorMin = Vector2.zero;
        content.anchorMax = Vector2.one;
        content.offsetMin = new Vector2(0f, SheetRadius);
        content.offsetMax = Vector2.zero;

        Image grabber = NewImage("Grabber", content, new Color(1f, 1f, 1f, 0.28f), rounded, 2f);
        Place(grabber.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(36f, 4f));
        grabber.raycastTarget = false;

        TextMeshProUGUI title = NewText("Title", content, "Catalog", 18f, true, Color.white, TextAlignmentOptions.Left);
        PlaceTop(title.rectTransform, 20f, 16f, 90f, 26f);

        TextMeshProUGUI count = NewText("Count", content, "15 items", 12f, false, MutedText, TextAlignmentOptions.Left);
        PlaceTop(count.rectTransform, 100f, 20f, 120f, 20f);

        Image hide = NewImage("Collapse Button", content, new Color(1f, 1f, 1f, 0.1f), rounded, 14f);
        Place(hide.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -14f), new Vector2(64f, 28f));
        Button hideButton = hide.gameObject.AddComponent<Button>();
        hideButton.targetGraphic = hide;
        hideButton.navigation = new Navigation { mode = Navigation.Mode.None };
        TextMeshProUGUI hideLabel = NewText("Label", hide.rectTransform, "Hide", 13f, true, Color.white, TextAlignmentOptions.Center);
        Stretch(hideLabel.rectTransform);

        // Tabs
        ScrollRect tabs = NewHorizontalScroll("Tabs", content, 48f, 36f, 8, 2f, out RectTransform tabContent);
        Button tabTemplate = BuildTabTemplate(tabContent, rounded);

        Image divider = NewImage("Divider", content, new Color(1f, 1f, 1f, 0.08f), null, 0f);
        divider.rectTransform.anchorMin = new Vector2(0f, 1f);
        divider.rectTransform.anchorMax = new Vector2(1f, 1f);
        divider.rectTransform.pivot = new Vector2(0.5f, 1f);
        divider.rectTransform.anchoredPosition = new Vector2(0f, -84f);
        divider.rectTransform.sizeDelta = new Vector2(0f, 1f);
        divider.raycastTarget = false;

        // Cards
        ScrollRect cards = NewHorizontalScroll("Cards", content, 94f, 150f, 16, 10f, out RectTransform cardContent);
        Button cardTemplate = BuildCardTemplate(cardContent, rounded);

        panel.sheet = rt;
        panel.collapseButton = hideButton;
        panel.collapseLabel = hideLabel;
        panel.countLabel = count;
        panel.tabContent = tabContent;
        panel.tabTemplate = tabTemplate;
        panel.cardScroll = cards;
        panel.cardContent = cardContent;
        panel.cardTemplate = cardTemplate;
        panel.tabActiveColor = Color.white;
        panel.tabInactiveColor = Alpha(MutedText, 0.75f);
        _ = tabs;
    }

    private static Button BuildTabTemplate(RectTransform parent, Sprite rounded)
    {
        RectTransform tab = NewRect("Tab Template", parent);
        Image hit = tab.gameObject.AddComponent<Image>();
        hit.color = Color.clear; // invisible, but catches taps across the whole tab

        HorizontalLayoutGroup layout = tab.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 0, 4);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        tab.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        TextMeshProUGUI label = NewText("Label", tab, "Tab", 14f, true, MutedText, TextAlignmentOptions.Center);
        label.overflowMode = TextOverflowModes.Overflow;

        Image underline = NewImage("Underline", tab, Cyan, rounded, 1.5f);
        underline.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        RectTransform u = underline.rectTransform;
        u.anchorMin = new Vector2(0f, 0f);
        u.anchorMax = new Vector2(1f, 0f);
        u.pivot = new Vector2(0.5f, 0f);
        u.anchoredPosition = Vector2.zero;
        u.sizeDelta = new Vector2(-24f, 3f);
        underline.raycastTarget = false;

        Button button = tab.gameObject.AddComponent<Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        tab.gameObject.SetActive(false);
        return button;
    }

    private static Button BuildCardTemplate(RectTransform parent, Sprite rounded)
    {
        RectTransform card = NewRect("Card Template", parent);
        Image hit = card.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        LayoutElement size = card.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = 120f;
        size.preferredHeight = 140f;

        // Cyan ring when the card's item is being placed: a cyan rounded rect
        // with the card background drawn 2 px inside it.
        Image selected = NewImage("Selected", card, Cyan, rounded, 16f);
        Stretch(selected.rectTransform);
        selected.raycastTarget = false;

        // Opaque (sheet colour lifted ~7 % toward white) so the cyan layer behind shows only as a ring.
        Image background = NewImage("Background", card, new Color32(32, 63, 77, 255), rounded, 14f);
        Stretch(background.rectTransform);
        background.rectTransform.offsetMin = new Vector2(2f, 2f);
        background.rectTransform.offsetMax = new Vector2(-2f, -2f);
        background.raycastTarget = false;

        // Light "product photo" well so dark furniture stays visible on the navy sheet.
        Image well = NewImage("Well", card, new Color(0.9f, 0.937f, 0.945f, 1f), rounded, 10f);
        PlaceTop(well.rectTransform, 8f, 8f, 104f, 84f);
        well.raycastTarget = false;

        Image thumbnail = NewImage("Thumbnail", card, Color.white, null, 0f);
        PlaceTop(thumbnail.rectTransform, 12f, 10f, 96f, 80f);
        thumbnail.preserveAspect = true;
        thumbnail.raycastTarget = false;

        // Long names shrink to fit instead of being cut off (sizing runs only when the text changes).
        TextMeshProUGUI name = NewText("Name", card, "Item", 12.5f, true, Color.white, TextAlignmentOptions.Left);
        name.enableAutoSizing = true;
        name.fontSizeMin = 9.5f;
        name.fontSizeMax = 12.5f;
        PlaceTop(name.rectTransform, 10f, 97f, 102f, 18f);

        TextMeshProUGUI subtitle = NewText("Subtitle", card, "", 10f, false, MutedText, TextAlignmentOptions.Left);
        PlaceTop(subtitle.rectTransform, 10f, 115f, 102f, 16f);

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = well;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.86f, 0.9f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        card.gameObject.SetActive(false);
        return button;
    }

    private static RectTransform BuildActionBar(RectTransform sheet, FurnitureCatalogPanel panel, ColorPickerPanel colorPicker,
        TouchInteractionManager manager, Sprite rounded, Sprite red)
    {
        // Anchored to the sheet's top edge, so it slides with the sheet.
        RectTransform bar = NewRect("Action Bar", sheet);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.anchoredPosition = new Vector2(0f, 10f);
        bar.sizeDelta = new Vector2(0f, 44f);

        HorizontalLayoutGroup layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        // Color: replaces the colour picker's old round button (and fixes its
        // caption staying visible). ColorPickerPanel shows it only while a piece
        // is selected; the layout closes the gap when it is hidden.
        // Chips are 42 high and the flat sprite 50: the sprite's shadow strip makes its visible face about the same height.
        Button color = NewChipButton("Color Button", bar, rounded, "Color", 104f, 42f, 42f);
        Image dot = NewImage("Swatch Dot", color.transform, Color.white, rounded, 9f);
        Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(18f, 18f));
        dot.raycastTarget = false;
        if (colorPicker.openButton != null)
        {
            Transform oldRoot = colorPicker.openButton.transform.parent;
            Object.DestroyImmediate(oldRoot != null && oldRoot != colorPicker.transform
                ? oldRoot.gameObject
                : colorPicker.openButton.gameObject);
        }
        Undo.RecordObject(colorPicker, "Wire color button");
        colorPicker.openButton = color;
        colorPicker.openButtonSwatch = dot;
        EditorUtility.SetDirty(colorPicker);

        Button delete = red != null
            ? NewFlatButton("Delete Button", bar, red, "Delete", 104f, 50f)
            : NewChipButton("Delete Button", bar, rounded, "Delete", 104f, 42f, 0f);
        UnityEventTools.AddPersistentListener(delete.onClick, new UnityAction(manager.DeleteSelected));
        panel.deleteLabel = delete.GetComponentInChildren<TMP_Text>();

        Button clear = NewChipButton("Clear All Button", bar, rounded, "Clear all", 124f, 42f, 0f);
        panel.clearAllButton = clear;
        panel.clearAllLabel = clear.GetComponentInChildren<TMP_Text>();

        bar.gameObject.SetActive(false);
        return bar;
    }

    /// <summary>Switches off the old Canvas panels this UI replaces and removes the retired gesture buttons.</summary>
    private static void RetireOldCanvas(Scene scene)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            foreach (Transform t in rootObject.GetComponentsInChildren<Transform>(true))
            {
                if (t == null)
                    continue;
                switch (t.name)
                {
                    case "FurniturePanel":
                    case "ControlsPanel":
                    case "StatusText":
                        if (t.GetComponentInParent<Canvas>(true) != null && t.root.name != "AR Overlay Canvas")
                            t.gameObject.SetActive(false);
                        break;
                    case "Rotate Left":
                    case "Rotate Right":
                    case "Scale Up":
                    case "Scale Down":
                    case "Move":
                        if (t.parent != null && t.parent.name == "ControlsPanel")
                            Object.DestroyImmediate(t.gameObject);
                        break;
                }
            }
        }
    }

    //=========================================================
    // MainMenu
    //=========================================================

    private static readonly (string title, string body)[] Steps =
    {
        ("Scan the floor", "Move your phone slowly over the floor until it is detected."),
        ("Choose", "Pick a single piece or a complete room set from the catalog."),
        ("Place", "Tap the floor to place it. Pinch and twist first to size and turn it."),
        ("Arrange", "Drag to move, pinch to resize, twist to rotate. Tap a piece to select it."),
        ("Style", "Use Color to recolor, Delete to remove, Clear all to start over."),
    };

    public static string BuildMainMenuUI()
    {
        MenuManager menu = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        if (menu == null)
            return "Open Assets/Scenes/MainMenu first (no MenuManager found).";

        Scene scene = menu.gameObject.scene;
        Sprite rounded = GetOrCreateRoundedSprite();
        Sprite blue = LoadButtonSprite(BlueButton);
        Sprite gradient = GetOrCreateGradientSprite();
        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);

        // Reuse the existing background photo, then retire the old canvas.
        Sprite photo = null;
        GameObject oldCanvas = null;
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == "MainMenuCanvas")
                oldCanvas = rootObject;
            foreach (Image image in rootObject.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Background" && image.sprite != null)
                    photo = image.sprite;
            }
        }

        Canvas canvas = GetOrCreateOverlayCanvas();
        DestroyChild(canvas.transform, "Start Screen");
        DestroyChild(canvas.transform, "How It Works");

        RectTransform screen = NewRect("Start Screen", canvas.transform);
        Stretch(screen);

        // Photo (cropped to fill), then a navy gradient so text stays readable.
        Image backdrop = NewImage("Backdrop", screen, Color.white, null, 0f);
        backdrop.sprite = photo;
        backdrop.raycastTarget = false;
        Stretch(backdrop.rectTransform);
        if (photo != null)
        {
            AspectRatioFitter fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = photo.rect.width / photo.rect.height;
        }
        else
        {
            backdrop.color = Navy;
        }

        Image tint = NewImage("Tint", screen, Alpha(Navy, 0.6f), null, 0f);
        Stretch(tint.rectTransform);
        tint.raycastTarget = false;
        Image fade = NewImage("Gradient", screen, Navy, null, 0f);
        fade.sprite = gradient;
        Stretch(fade.rectTransform);
        fade.raycastTarget = false;

        if (icon != null)
        {
            RawImage logo = NewRect("App Icon", screen).gameObject.AddComponent<RawImage>();
            logo.texture = icon;
            logo.raycastTarget = false;
            Place(logo.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(112f, 112f));
        }

        TextMeshProUGUI title = NewText("Title", screen, "AR Furniture Designer", 28f, true, Color.white, TextAlignmentOptions.Center);
        TopStretch(title.rectTransform, 222f, 38f, 20f);
        TextMeshProUGUI tagline = NewText("Tagline", screen, "See real furniture in your real room", 15f, false, BodyText, TextAlignmentOptions.Center);
        TopStretch(tagline.rectTransform, 262f, 24f, 20f);

        Image features = NewImage("Features", screen, Alpha(Navy, 0.78f), rounded, 18f);
        TopStretch(features.rectTransform, 318f, 150f, 24f);
        features.raycastTarget = false;
        string[] lines =
        {
            "80+ real-size pieces and 15 room sets",
            "Drag, pinch and twist to arrange",
            "Recolor anything with RGB or HEX",
        };
        for (int i = 0; i < lines.Length; i++)
        {
            float top = 24f + i * 40f;
            Image bullet = NewImage("Bullet " + (i + 1), features.rectTransform, Cyan, rounded, 4f);
            PlaceTop(bullet.rectTransform, 22f, top + 8f, 8f, 8f);
            bullet.raycastTarget = false;
            TextMeshProUGUI line = NewText("Feature " + (i + 1), features.rectTransform, lines[i], 14f, false, Color.white, TextAlignmentOptions.Left);
            PlaceTop(line.rectTransform, 42f, top, 280f, 24f);
        }

        Button start = blue != null
            ? NewFlatButton("Start Button", screen, blue, "Start designing", 0f, 56f)
            : NewChipButton("Start Button", screen, rounded, "Start designing", 0f, 56f, 0f);
        BottomStretch((RectTransform)start.transform, 152f, 56f, 24f);
        start.GetComponentInChildren<TMP_Text>().fontSize = 18f;
        UnityEventTools.AddPersistentListener(start.onClick, new UnityAction(menu.StartAR));

        Button how = NewChipButton("How It Works Button", screen, rounded, "How it works", 0f, 50f, 0f);
        how.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.14f);
        BottomStretch((RectTransform)how.transform, 90f, 50f, 24f);
        UnityEventTools.AddPersistentListener(how.onClick, new UnityAction(menu.ShowInstructions));

        TextMeshProUGUI exitLabel = NewText("Exit Button", screen, "Exit", 14f, true, MutedText, TextAlignmentOptions.Center);
        exitLabel.raycastTarget = true;
        BottomStretch(exitLabel.rectTransform, 40f, 36f, 120f);
        Button exit = exitLabel.gameObject.AddComponent<Button>();
        exit.targetGraphic = exitLabel;
        UnityEventTools.AddPersistentListener(exit.onClick, new UnityAction(menu.ExitApp));

        GameObject sheet = BuildHowItWorks(canvas.transform, rounded, blue);
        Undo.RecordObject(menu, "Wire instructions");
        menu.instructionsPanel = sheet;
        EditorUtility.SetDirty(menu);

        if (oldCanvas != null)
            oldCanvas.SetActive(false);

        EditorSceneManager.MarkSceneDirty(scene);
        return null;
    }

    private static GameObject BuildHowItWorks(Transform canvas, Sprite rounded, Sprite blue)
    {
        RectTransform root = NewRect("How It Works", canvas);
        Stretch(root);

        Image dim = NewImage("Dim", root, new Color(0f, 0f, 0f, 0.6f), null, 0f);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image card = NewImage("Card", root, SheetColor, rounded, 24f);
        Place(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(350f, 590f));

        TextMeshProUGUI title = NewText("Title", card.rectTransform, "How it works", 22f, true, Color.white, TextAlignmentOptions.Left);
        PlaceTop(title.rectTransform, 24f, 24f, 300f, 30f);

        for (int i = 0; i < Steps.Length; i++)
        {
            float top = 74f + i * 84f;
            Image number = NewImage("Step " + (i + 1), card.rectTransform, Teal, rounded, 14f);
            PlaceTop(number.rectTransform, 24f, top, 28f, 28f);
            number.raycastTarget = false;
            TextMeshProUGUI digit = NewText("Number", number.rectTransform, (i + 1).ToString(), 14f, true, Color.white, TextAlignmentOptions.Center);
            Stretch(digit.rectTransform);

            TextMeshProUGUI heading = NewText("Heading", card.rectTransform, Steps[i].title, 16f, true, Color.white, TextAlignmentOptions.Left);
            PlaceTop(heading.rectTransform, 66f, top + 2f, 260f, 24f);
            TextMeshProUGUI body = NewText("Body", card.rectTransform, Steps[i].body, 13f, false, BodyText, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            PlaceTop(body.rectTransform, 66f, top + 28f, 260f, 48f);
        }

        Button close = blue != null
            ? NewFlatButton("Got It Button", card.rectTransform, blue, "Got it", 0f, 52f)
            : NewChipButton("Got It Button", card.rectTransform, rounded, "Got it", 0f, 52f, 0f);
        BottomStretch((RectTransform)close.transform, 22f, 52f, 24f);
        UnityEventTools.AddBoolPersistentListener(close.onClick, new UnityAction<bool>(root.gameObject.SetActive), false);

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    //=========================================================
    // Controls
    //=========================================================

    /// <summary>Button using a 9-sliced flat sprite from the button pack. Width 0 = set by layout/anchors.</summary>
    private static Button NewFlatButton(string name, Transform parent, Sprite sprite, string label, float width, float height)
    {
        RectTransform rt = NewRect(name, parent);
        rt.sizeDelta = new Vector2(width, height);
        Image image = rt.gameObject.AddComponent<Image>();
        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = NewText("Label", rt, label, 15f, true, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        StyleAsFlatButton(button, sprite, height);
        if (width > 0f)
            AddLayoutSize(rt.gameObject, width, height);
        return button;
    }

    /// <summary>Re-skins an existing button with a flat pack sprite and white label.</summary>
    private static void StyleAsFlatButton(Button button, Sprite sprite, float height)
    {
        Image image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.color = Color.white;
        // Scale the sprite's borders so the corners/shadow suit this button height.
        image.pixelsPerUnitMultiplier = ButtonSpriteHeight / height * 0.92f;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            // The sprite's face sits above its darker base: nudge the text up to centre it on the face.
            RectTransform lt = label.rectTransform;
            lt.anchorMin = Vector2.zero;
            lt.anchorMax = Vector2.one;
            lt.offsetMin = new Vector2(8f, height * 0.12f);
            lt.offsetMax = new Vector2(-8f, 0f);
        }
    }

    /// <summary>Dark translucent rounded button (legible over the camera feed).</summary>
    private static Button NewChipButton(string name, Transform parent, Sprite rounded, string label, float width, float height, float labelInset)
    {
        Image image = NewImage(name, parent, Alpha(Navy, 0.9f), rounded, height * 0.5f);
        image.rectTransform.sizeDelta = new Vector2(width, height);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        TextMeshProUGUI text = NewText("Label", image.rectTransform, label, 15f, true, Color.white,
            labelInset > 0f ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(labelInset > 0f ? labelInset : 8f, 0f);
        text.rectTransform.offsetMax = new Vector2(-8f, 0f);
        if (width > 0f)
            AddLayoutSize(image.gameObject, width, height);
        return button;
    }

    private static void AddLayoutSize(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
            element = go.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
    }

    private static ScrollRect NewHorizontalScroll(string name, RectTransform parent, float top, float height, int padding, float spacing, out RectTransform content)
    {
        RectTransform rt = NewRect(name, parent);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(0f, height);
        Image hit = rt.gameObject.AddComponent<Image>();
        hit.color = Color.clear; // lets a drag on empty space scroll the row

        RectTransform viewport = NewRect("Viewport", rt);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, 0, 0);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = rt.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = 20f;
        return scroll;
    }

    //=========================================================
    // Layout helpers
    //=========================================================

    private static void TopStretch(RectTransform rt, float top, float height, float margin)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -top);
        rt.sizeDelta = new Vector2(-2f * margin, height);
    }

    private static void BottomStretch(RectTransform rt, float bottom, float height, float margin)
    {
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottom);
        rt.sizeDelta = new Vector2(-2f * margin, height);
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        while (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
            child = parent.Find(name);
        }
    }

    //=========================================================
    // Assets
    //=========================================================

    private static Sprite LoadButtonSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[CatalogUI] Button sprite not found: " + path);
            return null;
        }

        if (importer.textureType != TextureImporterType.Sprite ||
            importer.spriteImportMode != SpriteImportMode.Single ||
            importer.spriteBorder != ButtonBorder ||
            importer.mipmapEnabled)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = ButtonBorder;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>A 4 x 256 white sprite fading from transparent (top) to opaque (bottom), tinted by the Image colour.</summary>
    private static Sprite GetOrCreateGradientSprite()
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath);
        if (existing != null)
            return existing;

        const int height = 256;
        Texture2D texture = new Texture2D(4, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[4 * height];
        for (int y = 0; y < height; y++)
        {
            // y = 0 is the bottom row. Clear for the top 35 %, then ease to opaque.
            float t = Mathf.InverseLerp(0.65f, 0.05f, y / (float)(height - 1));
            byte a = (byte)(Mathf.SmoothStep(0f, 1f, t) * 255f);
            for (int x = 0; x < 4; x++)
                pixels[y * 4 + x] = new Color32(255, 255, 255, a);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        Directory.CreateDirectory(Path.GetDirectoryName(GradientPath));
        File.WriteAllBytes(GradientPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(GradientPath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(GradientPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(GradientPath);
    }
}

/// <summary>
/// Renders the built UI to PNGs for review: switches the overlay canvases to
/// camera space for one frame, over a photo standing in for the camera feed.
/// The scene is reopened afterwards without saving, so nothing changes.
/// </summary>
internal static class UIPreview
{
    private const int Width = 780;
    private const int Height = 1600;
    private const string FakeFeed = "Assets/Images/pngtree-dark-blue-wall-living-room-with-wooden-chair-on-flooring-3d-image_3873910.jpg";

    public static void RenderMainScene(string scenePath, string folder)
    {
        EditorSceneManager.OpenScene(scenePath);
        FurnitureCatalogPanel panel = Object.FindFirstObjectByType<FurnitureCatalogPanel>(FindObjectsInactive.Include);
        TouchInteractionManager manager = Object.FindFirstObjectByType<TouchInteractionManager>(FindObjectsInactive.Include);
        ColorPickerPanel picker = Object.FindFirstObjectByType<ColorPickerPanel>(FindObjectsInactive.Include);
        ScanningStatusUI status = Object.FindFirstObjectByType<ScanningStatusUI>(FindObjectsInactive.Include);

        // Simulate the running app: catalogue built, a card being placed, a piece selected.
        panel.gameObject.SetActive(true);
        typeof(FurnitureCatalogPanel).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null);
        if (status != null && status.statusText != null)
            status.statusText.text = "Drag to move  •  pinch to resize  •  twist to rotate";

        if (manager.controlsPanel != null)
            manager.controlsPanel.SetActive(true);
        if (picker != null && picker.openButton != null)
        {
            picker.openButton.gameObject.SetActive(true);
            if (picker.openButtonSwatch != null)
                picker.openButtonSwatch.color = new Color32(142, 98, 61, 255);
        }

        Transform firstCard = panel.cardContent.Find("Card 1");
        if (firstCard != null && firstCard.Find("Selected") != null)
            firstCard.Find("Selected").gameObject.SetActive(true);

        Render(Path.Combine(folder, "ui_main_sets.png"), true);

        panel.ShowCategory(1);
        Render(Path.Combine(folder, "ui_main_living.png"), true);

        panel.ShowCategory(5);
        Render(Path.Combine(folder, "ui_main_cafe.png"), true);

        if (picker != null && picker.sheet != null)
        {
            picker.sheet.SetActive(true);
            Render(Path.Combine(folder, "ui_main_color.png"), true);
        }
        EditorSceneManager.OpenScene(scenePath); // discard preview-only changes
    }

    public static void RenderMainMenu(string scenePath, string folder)
    {
        EditorSceneManager.OpenScene(scenePath);
        Render(Path.Combine(folder, "ui_menu.png"), false);
        MenuManager menu = Object.FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        if (menu != null && menu.instructionsPanel != null)
        {
            menu.instructionsPanel.SetActive(true);
            Render(Path.Combine(folder, "ui_menu_howto.png"), false);
        }
        EditorSceneManager.OpenScene(scenePath);
    }

    private static void Render(string file, bool fakeCameraFeed)
    {
        GameObject cameraObject = new GameObject("Preview Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        camera.cullingMask = 1 << LayerMask.NameToLayer("UI");
        camera.orthographic = true;
        RenderTexture target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        target.antiAliasing = 4;
        camera.targetTexture = target;

        GameObject feed = null;
        if (fakeCameraFeed)
        {
            feed = new GameObject("Fake Feed", typeof(RectTransform), typeof(Canvas));
            feed.layer = LayerMask.NameToLayer("UI");
            Canvas feedCanvas = feed.GetComponent<Canvas>();
            feedCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            feedCanvas.worldCamera = camera;
            feedCanvas.planeDistance = 5f;
            feedCanvas.sortingOrder = -100;
            GameObject photo = new GameObject("Photo", typeof(RectTransform), typeof(RawImage));
            photo.layer = feed.layer;
            photo.transform.SetParent(feed.transform, false);
            RawImage raw = photo.GetComponent<RawImage>();
            Texture2D photoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FakeFeed);
            raw.texture = photoTexture;
            if (photoTexture != null)
            {
                // Crop the landscape photo to fill the portrait frame.
                AspectRatioFitter fitter = photo.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = photoTexture.width / (float)photoTexture.height;
            }
        }

        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        RenderMode[] modes = new RenderMode[canvases.Length];
        for (int i = 0; i < canvases.Length; i++)
        {
            modes[i] = canvases[i].renderMode;
            if (!canvases[i].isRootCanvas || canvases[i].gameObject == feed)
                continue;
            canvases[i].renderMode = RenderMode.ScreenSpaceCamera;
            canvases[i].worldCamera = camera;
            canvases[i].planeDistance = 1f;
        }

        Canvas.ForceUpdateCanvases();
        foreach (Canvas c in canvases)
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)c.transform);
        Canvas.ForceUpdateCanvases();
        camera.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        image.Apply();
        RenderTexture.active = previous;
        File.WriteAllBytes(file, image.EncodeToPNG());

        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].gameObject != feed)
                canvases[i].renderMode = modes[i];
        }
        Object.DestroyImmediate(image);
        camera.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(cameraObject);
        if (feed != null)
            Object.DestroyImmediate(feed);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The Capture feature. One tap:
///  1. hides the app's UI, the selection ring and the floor guides, and grabs
///     a clean photo of the AR view;
///  2. lays out a portrait "spec sheet" (photo on top, then one row per placed
///     piece: thumbnail, name, type, material, colour, live size W x D x H,
///     estimated weight, size %), rendered off-screen at full resolution;
///  3. saves it to the Gallery (Pictures/AR Furniture Designer) and shows the
///     "Screenshot saved" screen with Keep designing / Back to start.
///
/// The spec sheet lives on its own canvas and camera far below the scene, so
/// it never appears in the AR view. Built and wired by
/// Tools > AR Furniture Designer > Create Catalog UI.
/// </summary>
public class SpecSheetCapture : MonoBehaviour
{
    [Serializable]
    public class SpecRow
    {
        public GameObject root;
        public Image thumbnail;
        public TMP_Text title;
        public Image swatch;
        public TMP_Text details;
        public TMP_Text size;
        public TMP_Text weight;
    }

    [Header("Logic")]
    public TouchInteractionManager interactionManager;
    public Button captureButton;

    [Header("Spec sheet (rendered off-screen)")]
    public GameObject sheetRoot;
    public Camera sheetCamera;
    public RawImage photo;
    public AspectRatioFitter photoFitter;
    public TMP_Text dateLabel;
    public TMP_Text summaryLabel;
    public TMP_Text moreLabel;
    public SpecRow[] rows;
    [Min(256)] public int outputWidth = 1080;
    [Min(256)] public int outputHeight = 1920;
    [Range(50, 100)] public int jpegQuality = 92;

    [Header("Saved screen")]
    public GameObject savedScreen;
    public RawImage savedPreview;
    public AspectRatioFitter savedPreviewFitter;
    public TMP_Text savedTitle;
    public TMP_Text savedMessage;
    public Button keepDesigningButton;
    public Button backToStartButton;

    [Header("Saving")]
    public string album = "AR Furniture Designer";
    public string menuScene = "MainMenu";

    private readonly List<GameObject> ordered = new List<GameObject>();
    private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
    private Texture2D lastSheet;
    private bool busy;

    private void Awake()
    {
        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<TouchInteractionManager>();

        if (captureButton != null) captureButton.onClick.AddListener(Capture);
        if (keepDesigningButton != null) keepDesigningButton.onClick.AddListener(CloseSavedScreen);
        if (backToStartButton != null) backToStartButton.onClick.AddListener(BackToStart);

        if (sheetRoot != null) sheetRoot.SetActive(false);
        if (savedScreen != null) savedScreen.SetActive(false);
        if (sheetCamera != null) sheetCamera.enabled = false; // renders only when asked
    }

    private void Update()
    {
        // Capture needs at least one placed piece. Selectable.interactable
        // ignores unchanged values, so setting it every frame is free.
        if (captureButton != null && interactionManager != null)
            captureButton.interactable = !busy && interactionManager.PlacedFurniture.Count > 0;
    }

    private void OnDestroy()
    {
        if (lastSheet != null)
            Destroy(lastSheet);
    }

    //=========================================================
    // Capture
    //=========================================================

    public void Capture()
    {
        if (!busy && interactionManager != null && interactionManager.PlacedFurniture.Count > 0)
            StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        busy = true;

        // A piece still waiting to be placed would appear as a floating preview.
        if (interactionManager.IsPlacementPending)
            interactionManager.DeleteSelected();

        SelectableFurniture selected = interactionManager.CurrentSelection;
        FloorPlaneIndicator indicator = interactionManager.GetComponent<FloorPlaneIndicator>();
        PlaneTrackingVisualizer planes = interactionManager.GetComponent<PlaneTrackingVisualizer>();

        SetCleanView(true, selected, indicator, planes);
        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
        SetCleanView(false, selected, indicator, planes);

        Texture2D sheet = null;
        bool saved = false;
        string error = null;
        try
        {
            sheet = RenderSheet(shot);
            byte[] jpeg = sheet.EncodeToJPG(jpegQuality);
            string fileName = "ARFD_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".jpg";
            saved = GallerySaver.SaveJpeg(jpeg, fileName, album, out error);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SpecSheetCapture] " + e);
            error = "Something went wrong while building the image.";
        }
        finally
        {
            Destroy(shot);
        }

        ShowSavedScreen(sheet, saved, error);
        busy = false;
    }

    /// <summary>Hides (or restores) everything that is not part of the room: UI, selection ring, floor guides.</summary>
    private void SetCleanView(bool clean, SelectableFurniture selected, FloorPlaneIndicator indicator, PlaneTrackingVisualizer planes)
    {
        if (clean)
        {
            hiddenCanvases.Clear();
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (canvas.isRootCanvas && canvas.enabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvas.enabled = false;
                    hiddenCanvases.Add(canvas);
                }
            }
        }
        else
        {
            foreach (Canvas canvas in hiddenCanvases)
            {
                if (canvas != null)
                    canvas.enabled = true;
            }
            hiddenCanvases.Clear();
        }

        if (selected != null) selected.SetHighlightVisible(!clean);
        if (indicator != null) indicator.SetVisible(!clean);
        if (planes != null) planes.SetVisible(!clean);
    }

    //=========================================================
    // Spec sheet
    //=========================================================

    private Texture2D RenderSheet(Texture2D shot)
    {
        sheetRoot.SetActive(true);
        photo.texture = shot;
        if (photoFitter != null)
            photoFitter.aspectRatio = shot.width / (float)shot.height;
        FillDetails();

        RenderTexture target = RenderTexture.GetTemporary(outputWidth, outputHeight, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D result = new Texture2D(outputWidth, outputHeight, TextureFormat.RGB24, false);
        try
        {
            sheetCamera.targetTexture = target;
            Canvas.ForceUpdateCanvases(); // lay out the rows at the target size before rendering
            sheetCamera.Render();

            RenderTexture.active = target;
            result.ReadPixels(new Rect(0, 0, outputWidth, outputHeight), 0, 0);
            result.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
            sheetCamera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            photo.texture = null;
            sheetRoot.SetActive(false);
        }
        return result;
    }

    /// <summary>Writes one row per placed piece (selected piece first), plus the header and totals.</summary>
    public void FillDetails()
    {
        IReadOnlyList<GameObject> placed = interactionManager != null ? interactionManager.PlacedFurniture : null;
        SelectableFurniture selected = interactionManager != null ? interactionManager.CurrentSelection : null;

        ordered.Clear();
        if (selected != null)
            ordered.Add(selected.gameObject);
        if (placed != null)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                if (placed[i] != null && (selected == null || placed[i] != selected.gameObject))
                    ordered.Add(placed[i]);
            }
        }

        float totalKg = 0f;
        int totalPieces = 0;
        foreach (GameObject piece in ordered)
        {
            FurnitureSpec spec = piece.GetComponent<FurnitureSpec>();
            SelectableFurniture furniture = piece.GetComponent<SelectableFurniture>();
            float scale = furniture != null ? furniture.ScaleFactor : 1f;
            if (spec != null)
            {
                totalKg += spec.WeightAtScale(scale);
                totalPieces += Mathf.Max(1, spec.pieceCount);
            }
            else
            {
                totalPieces++;
            }
        }

        for (int i = 0; i < rows.Length; i++)
        {
            bool used = i < ordered.Count;
            rows[i].root.SetActive(used);
            if (used)
                FillRow(rows[i], ordered[i]);
        }

        if (dateLabel != null)
            dateLabel.text = "Room capture  ·  " + DateTime.Now.ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture);
        if (summaryLabel != null)
            summaryLabel.text = string.Format(CultureInfo.InvariantCulture, "{0} {1}  ·  ≈ {2:0} kg",
                totalPieces, totalPieces == 1 ? "piece" : "pieces", totalKg);
        if (moreLabel != null)
        {
            int more = ordered.Count - rows.Length;
            moreLabel.text = more > 0 ? "+ " + more + " more placed " + (more == 1 ? "item" : "items") : string.Empty;
        }
    }

    private static void FillRow(SpecRow row, GameObject piece)
    {
        FurnitureSpec spec = piece.GetComponent<FurnitureSpec>();
        SelectableFurniture furniture = piece.GetComponent<SelectableFurniture>();

        float scale = furniture != null ? furniture.ScaleFactor : 1f;
        // Local bounds are unaffected by rotation, so these are the true W x D x H.
        Vector3 size = furniture != null
            ? Vector3.Scale(furniture.LocalBounds.size, furniture.OriginalScale) * scale
            : Vector3.zero;

        string name = spec != null && !string.IsNullOrEmpty(spec.displayName) ? spec.displayName : piece.name.Replace("(Clone)", "").Trim();
        row.title.text = name;

        // Single pieces: the materials line (the name already says what it is).
        // Room sets: "Dining set · 13 pieces · <main materials>".
        string details = string.Empty;
        if (spec != null && spec.isRoomSet)
            details = spec.type + "  ·  " + spec.pieceCount + " pieces";
        if (spec != null && !string.IsNullOrEmpty(spec.material))
            details += (details.Length > 0 ? "  ·  " : string.Empty) + spec.material;
        if (furniture != null && furniture.HasCustomColor)
            details += (details.Length > 0 ? "  ·  " : string.Empty) + "#" + ColorUtility.ToHtmlStringRGB(furniture.CustomColor);
        row.details.text = details.Length > 0 ? details : (spec != null ? spec.type : "Furniture");

        if (row.swatch != null)
            row.swatch.color = furniture != null ? (Color)furniture.CurrentColor : Color.white;

        row.size.text = string.Format(CultureInfo.InvariantCulture, "{0:0} × {1:0} × {2:0} cm",
            size.x * 100f, size.z * 100f, size.y * 100f);

        float kg = spec != null ? spec.WeightAtScale(scale) : 0f;
        row.weight.text = kg > 0f
            ? string.Format(CultureInfo.InvariantCulture, "≈ {0:0.#} kg  ·  {1:0}% size", kg, scale * 100f)
            : string.Format(CultureInfo.InvariantCulture, "{0:0}% size", scale * 100f);

        if (row.thumbnail != null)
        {
            row.thumbnail.sprite = spec != null ? spec.thumbnail : null;
            row.thumbnail.enabled = row.thumbnail.sprite != null;
        }
    }

    //=========================================================
    // Saved screen
    //=========================================================

    private void ShowSavedScreen(Texture2D sheet, bool saved, string error)
    {
        if (lastSheet != null)
            Destroy(lastSheet);
        lastSheet = sheet;

        if (savedScreen == null)
            return;

        if (savedPreview != null)
        {
            savedPreview.texture = sheet;
            savedPreview.enabled = sheet != null;
            if (savedPreviewFitter != null && sheet != null)
                savedPreviewFitter.aspectRatio = sheet.width / (float)sheet.height;
        }
        if (savedTitle != null)
            savedTitle.text = saved ? "Screenshot saved" : "Couldn't save screenshot";
        if (savedMessage != null)
            savedMessage.text = saved
                ? "Saved to Gallery  ›  " + album
                : (error ?? "Unknown error.") + " Your design is still here. Try again.";
        savedScreen.SetActive(true);
    }

    public void CloseSavedScreen()
    {
        if (savedScreen != null)
            savedScreen.SetActive(false);
        if (savedPreview != null)
            savedPreview.texture = null;
        if (lastSheet != null)
        {
            Destroy(lastSheet);
            lastSheet = null;
        }
    }

    public void BackToStart()
    {
        SceneManager.LoadScene(menuScene);
    }
}

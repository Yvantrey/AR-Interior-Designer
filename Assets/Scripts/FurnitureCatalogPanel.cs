using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The furniture catalogue bottom sheet: a row of room tabs (Living Room,
/// Bedroom, ...) and a horizontally scrolling row of item cards with
/// thumbnails. Tapping a card starts placing that item; tapping it again
/// cancels. The sheet can be collapsed to its header to see more of the room.
///
/// Build it with Tools > AR Furniture Designer > Create Catalog UI, which
/// creates the hierarchy below and wires every field. Tabs and cards are
/// cloned from the inactive templates once, in Awake, and reused afterwards.
/// </summary>
public class FurnitureCatalogPanel : MonoBehaviour
{
    [Header("Data")]
    public FurnitureCatalog catalog;
    public TouchInteractionManager interactionManager;

    [Header("Sheet")]
    [Tooltip("The sliding sheet. Collapsing moves it down so only the header strip stays visible.")]
    public RectTransform sheet;
    [Tooltip("Height of the part of the sheet that stays visible when collapsed.")]
    [Min(0f)] public float collapsedVisibleHeight = 56f;
    [Range(4f, 40f)] public float slideSharpness = 18f;
    public Button collapseButton;
    public TMP_Text collapseLabel;
    public TMP_Text countLabel;

    [Header("Tabs (template children: Label, Underline)")]
    public RectTransform tabContent;
    public Button tabTemplate;
    public Color tabActiveColor = new Color(1f, 1f, 1f, 1f);
    public Color tabInactiveColor = new Color(0.725f, 0.816f, 0.843f, 0.75f); // muted #B9D0D7

    [Header("Cards (template children: Selected, Thumbnail, Name, Subtitle)")]
    public ScrollRect cardScroll;
    public RectTransform cardContent;
    public Button cardTemplate;

    [Header("Action bar (shown while a piece is selected or being placed)")]
    [Tooltip("Label of the Delete button; reads \"Cancel\" while a piece is being placed.")]
    public TMP_Text deleteLabel;
    [Tooltip("Removes every placed piece. Needs a second tap within a few seconds to confirm.")]
    public Button clearAllButton;
    public TMP_Text clearAllLabel;
    [Min(0.5f)] public float clearAllConfirmSeconds = 3f;

    private struct Tab
    {
        public Button button;
        public TMP_Text label;
        public Graphic underline;
    }

    private struct Card
    {
        public Button button;
        public GameObject selected;
        public Image thumbnail;
        public TMP_Text title;
        public TMP_Text subtitle;
    }

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<Card> cards = new List<Card>();
    private int activeCategory = -1;
    private int pendingCard = -1;
    private bool collapsed;
    private bool lastPlacementPending;
    private float expandedY;
    private float clearAllArmedUntil = -1f;

    public bool IsCollapsed => collapsed;

    private void Awake()
    {
        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<TouchInteractionManager>();

        if (sheet != null)
            expandedY = sheet.anchoredPosition.y;

        if (collapseButton != null)
            collapseButton.onClick.AddListener(ToggleCollapsed);
        if (clearAllButton != null)
            clearAllButton.onClick.AddListener(OnClearAllPressed);

        BuildTabs();
        BuildCardPool();
        ShowCategory(0);
        UpdateCollapseLabel();
        UpdateDeleteLabel(false);
    }

    private void Update()
    {
        if (sheet != null)
        {
            float targetY = collapsed ? expandedY - (sheet.rect.height - collapsedVisibleHeight - SheetOverhang()) : expandedY;
            Vector2 position = sheet.anchoredPosition;
            if (Mathf.Abs(position.y - targetY) > 0.01f)
            {
                // Frame-rate independent exponential ease toward the target.
                position.y = Mathf.Lerp(position.y, targetY, 1f - Mathf.Exp(-slideSharpness * Time.unscaledDeltaTime));
                if (Mathf.Abs(position.y - targetY) < 0.5f)
                    position.y = targetY;
                sheet.anchoredPosition = position;
            }
        }

        // The manager ends placement on its own (the user tapped the floor), so
        // poll that one bool and clear the card highlight / button label when it flips.
        bool placementPending = interactionManager != null && interactionManager.IsPlacementPending;
        if (placementPending != lastPlacementPending)
        {
            lastPlacementPending = placementPending;
            if (!placementPending)
                SetPendingCard(-1);
            UpdateDeleteLabel(placementPending);
        }

        if (clearAllArmedUntil > 0f && Time.unscaledTime > clearAllArmedUntil)
            DisarmClearAll();
    }

    private void OnDisable()
    {
        DisarmClearAll();
    }

    //=========================================================
    // Public actions
    //=========================================================

    /// <summary>First tap arms the button ("Tap to confirm"); a second tap within the time limit clears the room.</summary>
    public void OnClearAllPressed()
    {
        if (clearAllArmedUntil < 0f)
        {
            clearAllArmedUntil = Time.unscaledTime + clearAllConfirmSeconds;
            if (clearAllLabel != null)
                clearAllLabel.text = "Tap to confirm";
            return;
        }

        DisarmClearAll();
        if (interactionManager != null)
            interactionManager.ResetLayout();
    }

    private void DisarmClearAll()
    {
        clearAllArmedUntil = -1f;
        if (clearAllLabel != null)
            clearAllLabel.text = "Clear all";
    }

    public void ToggleCollapsed()
    {
        SetCollapsed(!collapsed);
    }

    public void SetCollapsed(bool value)
    {
        collapsed = value;
        UpdateCollapseLabel();
    }

    public void ShowCategory(int index)
    {
        if (catalog == null || index < 0 || index >= catalog.categories.Count)
            return;

        activeCategory = index;
        SetPendingCard(-1);

        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = i == index;
            if (tabs[i].label != null)
                tabs[i].label.color = active ? tabActiveColor : tabInactiveColor;
            if (tabs[i].underline != null)
                tabs[i].underline.enabled = active;
        }

        List<CatalogEntry> entries = catalog.categories[index].entries;
        for (int i = 0; i < cards.Count; i++)
        {
            bool used = i < entries.Count;
            Card card = cards[i];
            card.button.gameObject.SetActive(used);
            if (!used)
                continue;

            CatalogEntry entry = entries[i];
            if (card.title != null) card.title.text = entry.displayName;
            if (card.subtitle != null) card.subtitle.text = entry.subtitle;
            if (card.thumbnail != null)
            {
                card.thumbnail.sprite = entry.thumbnail;
                card.thumbnail.enabled = entry.thumbnail != null;
            }
            card.button.interactable = entry.prefab != null;
        }

        if (countLabel != null)
            countLabel.SetText(entries.Count == 1 ? "{0} item" : "{0} items", entries.Count);

        if (cardScroll != null)
            cardScroll.horizontalNormalizedPosition = 0f;
    }

    //=========================================================
    // Building
    //=========================================================

    private void BuildTabs()
    {
        if (catalog == null || tabTemplate == null || tabContent == null)
            return;

        tabTemplate.gameObject.SetActive(false);
        for (int i = 0; i < catalog.categories.Count; i++)
        {
            Button button = Instantiate(tabTemplate, tabContent);
            button.gameObject.name = "Tab " + catalog.categories[i].displayName;
            button.gameObject.SetActive(true);

            Tab tab = new Tab
            {
                button = button,
                label = FindChild<TMP_Text>(button.transform, "Label"),
                underline = FindChild<Graphic>(button.transform, "Underline"),
            };
            if (tab.label != null)
                tab.label.text = catalog.categories[i].displayName;

            int captured = i; // each tab remembers its own index
            button.onClick.AddListener(() => ShowCategory(captured));
            tabs.Add(tab);
        }
    }

    /// <summary>Creates enough cards for the largest category; categories reuse them.</summary>
    private void BuildCardPool()
    {
        if (catalog == null || cardTemplate == null || cardContent == null)
            return;

        int needed = 0;
        foreach (CatalogCategory category in catalog.categories)
            needed = Mathf.Max(needed, category.entries.Count);

        cardTemplate.gameObject.SetActive(false);
        for (int i = 0; i < needed; i++)
        {
            Button button = Instantiate(cardTemplate, cardContent);
            button.gameObject.name = "Card " + i;

            Card card = new Card
            {
                button = button,
                selected = FindChild<Transform>(button.transform, "Selected")?.gameObject,
                thumbnail = FindChild<Image>(button.transform, "Thumbnail"),
                title = FindChild<TMP_Text>(button.transform, "Name"),
                subtitle = FindChild<TMP_Text>(button.transform, "Subtitle"),
            };
            if (card.selected != null)
                card.selected.SetActive(false);

            int captured = i;
            button.onClick.AddListener(() => OnCardClicked(captured));
            cards.Add(card);
        }
    }

    //=========================================================
    // Card selection
    //=========================================================

    private void OnCardClicked(int index)
    {
        if (interactionManager == null || activeCategory < 0)
            return;

        List<CatalogEntry> entries = catalog.categories[activeCategory].entries;
        if (index < 0 || index >= entries.Count || entries[index].prefab == null)
            return;

        // Tapping the card that is already being placed cancels placement.
        if (index == pendingCard && interactionManager.IsPlacementPending)
        {
            interactionManager.DeleteSelected();
            SetPendingCard(-1);
            return;
        }

        interactionManager.SetFurnitureToPlace(entries[index].prefab);
        SetPendingCard(interactionManager.IsPlacementPending ? index : -1);
    }

    private void SetPendingCard(int index)
    {
        if (pendingCard >= 0 && pendingCard < cards.Count && cards[pendingCard].selected != null)
            cards[pendingCard].selected.SetActive(false);

        pendingCard = index;

        if (pendingCard >= 0 && pendingCard < cards.Count && cards[pendingCard].selected != null)
            cards[pendingCard].selected.SetActive(true);
    }

    //=========================================================
    // Helpers
    //=========================================================

    private void UpdateCollapseLabel()
    {
        if (collapseLabel != null)
            collapseLabel.text = collapsed ? "Show" : "Hide";
    }

    private void UpdateDeleteLabel(bool placementPending)
    {
        if (deleteLabel != null)
            deleteLabel.text = placementPending ? "Cancel" : "Delete";
    }

    /// <summary>
    /// The sheet hangs slightly below the screen so only its top corners look
    /// rounded; that hidden strip must not count as visible when collapsing.
    /// </summary>
    private float SheetOverhang()
    {
        return Mathf.Max(0f, -expandedY);
    }

    private static T FindChild<T>(Transform root, string name) where T : Component
    {
        Transform child = root.Find(name);
        return child != null ? child.GetComponent<T>() : null;
    }
}

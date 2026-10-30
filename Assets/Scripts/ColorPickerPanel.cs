using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bottom sheet that recolours the selected furniture with R, G, B sliders
/// (0-255) or a HEX code. The furniture's SelectableFurniture holds the colour;
/// this panel only reads and writes it.
///
/// Build the UI with Tools > AR Furniture Designer > Create Color Picker UI,
/// which creates every element below and wires these fields for you.
/// </summary>
public class ColorPickerPanel : MonoBehaviour
{
    [Header("Logic")]
    public TouchInteractionManager interactionManager;

    [Header("Open button (only visible while a piece is selected)")]
    public Button openButton;
    [Tooltip("Small dot inside the open button that shows the selected piece's colour.")]
    public Image openButtonSwatch;

    [Header("Sheet")]
    public GameObject sheet;
    public Image swatch;
    [Tooltip("Shows e.g. RGB(142, 98, 61)  /  #8E623D")]
    public TMP_Text valueLabel;
    public TMP_InputField hexInput;

    [Header("Sliders (0-255)")]
    public Slider redSlider;
    public Slider greenSlider;
    public Slider blueSlider;
    public TMP_Text redValue;
    public TMP_Text greenValue;
    public TMP_Text blueValue;

    [Header("Buttons")]
    public Button resetButton;
    public Button doneButton;

    // The HEX field needs a string, which allocates; refresh it only once the
    // sliders have been still for this long instead of on every tick.
    private const float HexFieldRefreshDelay = 0.15f;

    private readonly char[] valueBuffer = new char[48];
    private SelectableFurniture target;
    private bool hexFieldDirty;
    private float hexFieldDirtyTime;

    public bool IsOpen => sheet != null && sheet.activeSelf;

    private void Awake()
    {
        if (interactionManager == null)
            interactionManager = FindFirstObjectByType<TouchInteractionManager>();

        SetupSlider(redSlider);
        SetupSlider(greenSlider);
        SetupSlider(blueSlider);

        if (hexInput != null)
        {
            hexInput.characterLimit = 7;
            hexInput.lineType = TMP_InputField.LineType.SingleLine;
            hexInput.keyboardType = TouchScreenKeyboardType.ASCIICapable;
            hexInput.onValidateInput = ValidateHexChar;
            hexInput.onValueChanged.AddListener(OnHexTyped);
            hexInput.onEndEdit.AddListener(OnHexEndEdit);
        }

        if (openButton != null) openButton.onClick.AddListener(Toggle);
        if (resetButton != null) resetButton.onClick.AddListener(ResetColor);
        if (doneButton != null) doneButton.onClick.AddListener(Close);

        if (sheet != null)
            sheet.SetActive(false);
    }

    private void OnEnable()
    {
        if (interactionManager != null)
        {
            interactionManager.SelectionChanged += OnSelectionChanged;
            OnSelectionChanged(interactionManager.CurrentSelection);
        }
        else
        {
            OnSelectionChanged(null);
        }
    }

    private void OnDisable()
    {
        if (interactionManager != null)
            interactionManager.SelectionChanged -= OnSelectionChanged;
    }

    private void Update()
    {
        if (!hexFieldDirty || target == null || hexInput == null)
            return;

        if (hexInput.isFocused)
            return; // never overwrite what the user is typing

        if (Time.unscaledTime - hexFieldDirtyTime >= HexFieldRefreshDelay)
        {
            hexInput.SetTextWithoutNotify(ColorFormat.ToHex(target.CurrentColor));
            hexFieldDirty = false;
        }
    }

    //=========================================================
    // Public actions (also usable from Button OnClick in the Inspector)
    //=========================================================

    public void Open()
    {
        if (target == null || sheet == null)
            return;

        sheet.SetActive(true);
        RefreshFromTarget();
    }

    public void Close()
    {
        if (hexInput != null && hexInput.isFocused)
            hexInput.DeactivateInputField();

        if (sheet != null)
            sheet.SetActive(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void ResetColor()
    {
        if (target == null)
            return;

        target.ResetColor();
        RefreshFromTarget();
    }

    //=========================================================
    // Events
    //=========================================================

    private void OnSelectionChanged(SelectableFurniture selection)
    {
        target = selection;

        if (openButton != null)
            openButton.gameObject.SetActive(selection != null);

        if (selection == null)
        {
            Close();
            return;
        }

        UpdateOpenButtonSwatch(selection.CurrentColor);
        if (IsOpen)
            RefreshFromTarget(); // switching pieces keeps the sheet open
    }

    private void OnSliderChanged(float _)
    {
        if (target == null)
            return;

        Color32 color = new Color32(
            (byte)Mathf.RoundToInt(redSlider.value),
            (byte)Mathf.RoundToInt(greenSlider.value),
            (byte)Mathf.RoundToInt(blueSlider.value),
            255);

        target.SetCustomColor(color);
        UpdateReadouts(color);

        hexFieldDirty = true;
        hexFieldDirtyTime = Time.unscaledTime;
    }

    private void OnHexTyped(string text)
    {
        // Apply live as soon as a full 6-digit code has been typed.
        if (target == null || !ColorFormat.TryParseHex(text, out Color32 color))
            return;

        target.SetCustomColor(color);
        SetSlidersWithoutNotify(color);
        UpdateReadouts(color);
        hexFieldDirty = false;
    }

    private void OnHexEndEdit(string text)
    {
        if (target == null)
            return;

        // Valid: normalise to "#RRGGBB". Invalid or incomplete: put the current colour back.
        if (ColorFormat.TryParseHex(text, out Color32 color))
            target.SetCustomColor(color);

        hexInput.SetTextWithoutNotify(ColorFormat.ToHex(target.CurrentColor));
        hexFieldDirty = false;
        RefreshFromTarget();
    }

    private char ValidateHexChar(string text, int charIndex, char addedChar)
    {
        if (addedChar == '#')
            return charIndex == 0 && !text.Contains("#") ? '#' : '\0';

        return ColorFormat.IsHexChar(addedChar) ? char.ToUpperInvariant(addedChar) : '\0';
    }

    //=========================================================
    // Display
    //=========================================================

    private void RefreshFromTarget()
    {
        if (target == null)
            return;

        Color32 color = target.CurrentColor;
        SetSlidersWithoutNotify(color);
        UpdateReadouts(color);

        if (hexInput != null && !hexInput.isFocused)
            hexInput.SetTextWithoutNotify(ColorFormat.ToHex(color));
        hexFieldDirty = false;
    }

    private void SetSlidersWithoutNotify(Color32 color)
    {
        if (redSlider != null) redSlider.SetValueWithoutNotify(color.r);
        if (greenSlider != null) greenSlider.SetValueWithoutNotify(color.g);
        if (blueSlider != null) blueSlider.SetValueWithoutNotify(color.b);
    }

    /// <summary>Allocation-free refresh of the swatch and number labels.</summary>
    private void UpdateReadouts(Color32 color)
    {
        if (swatch != null)
            swatch.color = color;

        if (redValue != null) redValue.SetText("{0}", color.r);
        if (greenValue != null) greenValue.SetText("{0}", color.g);
        if (blueValue != null) blueValue.SetText("{0}", color.b);

        if (valueLabel != null)
        {
            int length = ColorFormat.WriteRgbAndHex(valueBuffer, color);
            valueLabel.SetCharArray(valueBuffer, 0, length);
        }

        UpdateOpenButtonSwatch(color);
    }

    private void UpdateOpenButtonSwatch(Color32 color)
    {
        if (openButtonSwatch != null)
            openButtonSwatch.color = color;
    }

    private void SetupSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.minValue = 0f;
        slider.maxValue = 255f;
        slider.wholeNumbers = true;
        slider.onValueChanged.AddListener(OnSliderChanged);
    }
}

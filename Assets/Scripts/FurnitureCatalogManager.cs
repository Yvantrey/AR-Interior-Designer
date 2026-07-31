using UnityEngine;
using UnityEngine.UI;
using TMPro;


[System.Serializable]
public class FurnitureCatalogItem
{
    public string furnitureName;
    public GameObject prefab;
    public Sprite icon;
}


public class FurnitureCatalogManager : MonoBehaviour
{
    [Header("Catalog Data (add your furniture here)")]
    public FurnitureCatalogItem[] catalogItems;

    [Header("UI References")]
    [Tooltip("The 'Content' object inside your Scroll View")]
    public Transform catalogButtonContainer;

    [Tooltip("A prefab Button that has a child Image called 'Icon' and a child Text called 'Label'")]
    public GameObject catalogButtonPrefab;

    [Header("Manager Reference")]
    public TouchInteractionManager interactionManager;

    void Start()
    {
        BuildCatalogUI();
    }

    private void BuildCatalogUI()
    {
        foreach (var item in catalogItems)
        {
            GameObject buttonObj = Instantiate(catalogButtonPrefab, catalogButtonContainer);

            Transform iconTransform = buttonObj.transform.Find("Icon");
            if (iconTransform != null)
            {
                Image icon = iconTransform.GetComponent<Image>();
                if (icon != null) icon.sprite = item.icon;
            }

            Transform labelTransform = buttonObj.transform.Find("Label");
            if (labelTransform != null)
            {
                TMP_Text tmpLabel = labelTransform.GetComponent<TMP_Text>();
                if (tmpLabel != null)
                {
                    tmpLabel.text = item.furnitureName;
                }
                else
                {
                    Text label = labelTransform.GetComponent<Text>();
                    if (label != null) label.text = item.furnitureName;
                }
            }

            Button button = buttonObj.GetComponent<Button>();
            GameObject prefabRef = item.prefab; // captured so each button remembers its own furniture
            button.onClick.AddListener(() => interactionManager.SetFurnitureToPlace(prefabRef));
        }
    }
}
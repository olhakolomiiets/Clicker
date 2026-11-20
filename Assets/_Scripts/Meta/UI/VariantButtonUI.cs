 using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;
using Lean.Localization;

public class VariantButtonUI : MonoBehaviour
{
    public Image icon;
    public Image diamondIcon;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI itemName;
    private ItemVariant itemData;
    private int index;
    public int Index => index;
    private MetaVariantItemController itemController;
    private bool isActive;
    private bool isBought;
    public bool IsBought => isBought;

    private double price;
    public double Price => price;
    [SerializeField] private Button button;
    public GameObject BuyButtonGameObject => button != null ? button.gameObject : null;

    public event Action<double> OnBuyButtonClicked;
    private Func<double, bool> _tryPurchase;

    private void OnEnable()
    {
        LeanLocalization.OnLocalizationChanged += UpdateLanguage;
        UpdateLanguage();
    }

    private void OnDisable()
    {
        LeanLocalization.OnLocalizationChanged -= UpdateLanguage;
    }

    public void Init(
        ItemVariant variant,
        int idx,
        MetaVariantItemController ctrl,
        bool active,
        bool bought,
        Func<double, bool> tryPurchase = null)
    {
        itemData = variant;
        index = idx;
        itemController = ctrl;
        icon.sprite = variant.ItemIcon;
        price = variant.Price;
        isActive = active;
        isBought = bought;
        _tryPurchase = tryPurchase;

        if (!isBought)
        {
            priceText.text = variant.Price.ToString();
        }
        if (isBought && !isActive)
        {
            diamondIcon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Select");
        }
        if (isActive)
        {
            diamondIcon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Selected");
        }

        UpdateLanguage();
        button.onClick.AddListener(HandleBuyButton);

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! VariantButtonUI /// Init /// index " + index);
    }

    public void ToggleBuyButton(bool val)
    {  
        if (!isBought)
        {
            button.interactable = val;
        }
    }

    public void UpdateButtonText(bool active, bool bought)
    {  
        isActive = active;
        isBought = bought;
        if (isBought && !isActive)
        {
            diamondIcon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Select");
        }
        if (isActive)
        {
            diamondIcon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Selected");
        }
    }

    // public void UpdateState(int index)
    // {
    //     itemController.SelectVariant(index);
    // }

    public void UpdateLanguage()
    {
        if (itemData == null)
            return;

        string key = itemData.VariantName switch
        {
            "Decoration_1" => "meta_variant_decoration_01",
            "Decoration_2" => "meta_variant_decoration_02",
            "Decoration_3" => "meta_variant_decoration_03",
            "Decoration_4" => "meta_variant_decoration_04",
            "Decoration_5" => "meta_variant_decoration_05",
            "Decoration_6" => "meta_variant_decoration_06",
            "Decoration_7" => "meta_variant_decoration_07",
            "Decoration_8" => "meta_variant_decoration_08",
            "Decoration_9" => "meta_variant_decoration_09",
            "Decoration_10" => "meta_variant_decoration_10",
            "Decoration_11" => "meta_variant_decoration_11",
            "Decoration_12" => "meta_variant_decoration_12",
            "Decoration_13" => "meta_variant_decoration_13",
            "Decoration_14" => "meta_variant_decoration_14",
            "Decoration_15" => "meta_variant_decoration_15",
            "Decoration_16" => "meta_variant_decoration_16",
            "Decoration_17" => "meta_variant_decoration_17",
            "Decoration_19" => "meta_variant_decoration_19",
            "Decoration_20" => "meta_variant_decoration_20",
            "Decoration_21" => "meta_variant_decoration_21",
            "Decoration_22" => "meta_variant_decoration_22",
            "Decoration_23" => "meta_variant_decoration_23",
            "Decoration_24" => "meta_variant_decoration_24",
            "Decoration_25" => "meta_variant_decoration_25",
            "Decoration_26" => "meta_variant_decoration_26",
            "Decoration_27" => "meta_variant_decoration_27",
            "Decoration_28" => "meta_variant_decoration_28",
            "Snail House" => "meta_variant_house_01",
            "Umbrella" => "meta_variant_house_02",
            "Panopticon" => "meta_variant_house_03",
            "TV" => "meta_variant_house_04",
            "CuboDom" => "meta_variant_house_05",
            "Regular POD for Nomads" => "meta_variant_house_06",
            "Sphere" => "meta_variant_house_07",
            "Portal" => "meta_variant_house_08",
            "LWS_1" => "meta_variant_weird_object_01",
            "LWS_2" => "meta_variant_weird_object_02",
            "LWS_3" => "meta_variant_weird_object_03",
            "LWS_4" => "meta_variant_weird_object_04",
            "LWS_5" => "meta_variant_weird_object_05",
            "LWS_6" => "meta_variant_weird_object_06",
            "LWS_7" => "meta_variant_weird_object_07",
            "LWS_8" => "meta_variant_weird_object_08",
            "LWS_9" => "meta_variant_weird_object_09",
            "LWS_10" => "meta_variant_weird_object_10",
            "LWS_11" => "meta_variant_weird_object_11",
            "LWS_12" => "meta_variant_weird_object_12",
            "Plant_1" => "meta_variant_plant_01",
            "Plant_2" => "meta_variant_plant_02",
            "Plant_3" => "meta_variant_plant_03",
            "Plant_4" => "meta_variant_plant_04",
            "Plant_5" => "meta_variant_plant_05",
            "Plant_6" => "meta_variant_plant_06",
            "Plant_7" => "meta_variant_plant_07",
            "Plant_8" => "meta_variant_plant_08",
            "Plant_9" => "meta_variant_plant_09",
            "Plant_10" => "meta_variant_plant_10",
            "Plant_11" => "meta_variant_plant_11",
            "Plant_12" => "meta_variant_plant_12",
            "Plant_13" => "meta_variant_plant_13",
            "Plant_14" => "meta_variant_plant_14",
            "Plant_15" => "meta_variant_plant_15",
            "Plant_16" => "meta_variant_plant_16",
            "Plant_17" => "meta_variant_plant_17",
            "Plant_18" => "meta_variant_plant_18",
            "Transport_1" => "meta_variant_transport_01",
            "Transport_2" => "meta_variant_transport_02",
            "Transport_3" => "meta_variant_transport_03",
            "Transport_4" => "meta_variant_transport_04",
            "Transport_5" => "meta_variant_transport_05",
            "Transport_6" => "meta_variant_transport_06",
            "Transport_7" => "meta_variant_transport_07",
            "Transport_8" => "meta_variant_transport_08",
            "Transport_9" => "meta_variant_transport_09",
            "Transport_10" => "meta_variant_transport_10",
            "Tree_1" => "meta_variant_tree_01",
            "Tree_2" => "meta_variant_tree_02",
            "Tree_3" => "meta_variant_tree_03",
            "Tree_4" => "meta_variant_tree_04",
            "Tree_5" => "meta_variant_tree_05",
            "Tree_6" => "meta_variant_tree_06",
            "Tree_7" => "meta_variant_tree_07",
            "Tree_8" => "meta_variant_tree_08",
            "Tree_9" => "meta_variant_tree_09",
            "Tree_10" => "meta_variant_tree_10",
            "Tree_11" => "meta_variant_tree_11",
            "Tree_12" => "meta_variant_tree_12",
            _ => itemData.VariantName
        };
        itemName.text = LeanLocalization.GetTranslationText(key, itemData.VariantName);
        UpdateButtonText(isActive, isBought);
    }

    // void OnClick()
    // {
    //     itemController.SelectVariant(index);

    //     foreach (var btn in transform.parent.GetComponentsInChildren<VariantButtonUI>())
    //         btn.UpdateInteractable();
    // }

    private void HandleBuyButton()
    {
        if (itemController.IsVariantBought(index) == false)
        {
            OnBuyButtonClicked?.Invoke(price);

            if (_tryPurchase == null || _tryPurchase(price))
                itemController.OnBuyVariant(index);
        }

        if (itemController.IsVariantBought(index) == true && itemController.IsVariantActive(index) == false)
        {
            itemController.SelectVariant(index);
        }
        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! VariantButtonUI /// HandleBuyButton /// index " + index);
    }
    
}

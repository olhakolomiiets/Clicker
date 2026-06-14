 using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;
using Lean.Localization;

public class VariantButtonUI : MonoBehaviour
{
    public Image icon;
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
        itemName.text = variant.VariantName;
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
            icon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Select");
        }
        if (isActive)
        {
            icon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Selected");
        }

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
            icon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Select");
        }
        if (isActive)
        {
            icon.gameObject.SetActive(false);
            priceText.text = LeanLocalization.GetTranslationText("Selected");
        }
    }

    // public void UpdateState(int index)
    // {
    //     itemController.SelectVariant(index);
    // }

    public void UpdateLanguage()
    {
        // if (_purchaseInfo.isActiveAndEnabled)
        // {
        //     _purchaseInfoText.text = $"{LeanLocalization.GetTranslationText("Unlock")} {LeanLocalization.GetTranslationText(_translationText)}";
        // }
        // _itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
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

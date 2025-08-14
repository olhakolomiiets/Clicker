using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

public class VariantButtonUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI priceText;
    public TextMeshProUGUI itemName;
    private ItemVariant itemData;
    private int index;
    private MetaVariantItemController itemController;
    [SerializeField] private Button button;

    public event Action OnBuyButtonClicked;

    private void Awake()
    {
        
    }

    public void Init(ItemVariant variant, int idx, MetaVariantItemController ctrl)
    {
        itemData = variant;
        index = idx;
        itemController = ctrl;
        icon.sprite = variant.ItemIcon;
        itemName.text = variant.VariantName;
        priceText.text = variant.Price.ToString();
        UpdateInteractable();

        button.onClick.AddListener(HandleBuyButton);
        //button.onClick.AddListener(OnClick);
    }

    public void ToggleBuyButton(bool val)
        => button.interactable = val;

    public void UpdateInteractable()
    {
        //bool bought = SaveSystem.IsVariantBought(controller.gameObject.name, index);
        //bool canAfford = CurrencyManager.Instance.Coins >= data.price;
        //GetComponent<Button>().interactable = bought || canAfford;
        // ����� ������ ����/������ � ����������� �� bought/canAfford
    }

    public void UpdateState(int index)
    {
        itemController.SelectVariant(index);
    }

    public void UpdateLanguage()
    {
        // if (_purchaseInfo.isActiveAndEnabled)
        // {
        //     _purchaseInfoText.text = $"{LeanLocalization.GetTranslationText("Unlock")} {LeanLocalization.GetTranslationText(_translationText)}";
        // }
        // _itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
    }

    void OnClick()
    {
        itemController.SelectVariant(index);

        foreach (var btn in transform.parent.GetComponentsInChildren<VariantButtonUI>())
            btn.UpdateInteractable();
    }

    private void HandleBuyButton() => OnBuyButtonClicked?.Invoke();
}

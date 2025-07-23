using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VariantButtonUI : MonoBehaviour
{
    public Image icon;
    public TextMeshProUGUI priceText;
    private BuildingVariantSO data;
    private int index;
    private BuildingController controller;
    [SerializeField] private Button button;

    public void Init(BuildingVariantSO variant, int idx, BuildingController ctrl)
    {
        data = variant;
        index = idx;
        controller = ctrl;
        icon.sprite = variant.icon;
        priceText.text = variant.price.ToString();
        UpdateInteractable();
        button.onClick.AddListener(OnClick);
    }

    void UpdateInteractable()
    {
        //bool bought = SaveSystem.IsVariantBought(controller.gameObject.name, index);
        //bool canAfford = CurrencyManager.Instance.Coins >= data.price;
        //GetComponent<Button>().interactable = bought || canAfford;
        // Можно менять цвет/иконку в зависимости от bought/canAfford
    }

    void OnClick()
    {
        //bool bought = SaveSystem.IsVariantBought(controller.gameObject.name, index);
        //if (!bought)
        //{
        //    if (CurrencyManager.Instance.Spend(data.price))
        //    {
        //        SaveSystem.MarkVariantBought(controller.gameObject.name, index);
        //    }
        //    else return; // не хватает монет
        //}
        controller.SelectVariant(index);
        // Обновить все кнопки в панели:
        foreach (var btn in transform.parent.GetComponentsInChildren<VariantButtonUI>())
            btn.UpdateInteractable();
    }
}

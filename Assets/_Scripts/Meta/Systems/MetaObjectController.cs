using Lean.Localization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaObjectController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _itemTitle;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _itemCount;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private Image _itemImage;

    private string _translationText;

    public GameObject BuyButtonGameObject => _buyButton != null ? _buyButton.gameObject : null;

    public event Action OnObjectAddButtonClicked;

    public void PrepareVariantObject(Sprite icon, string translationText)
    {
        _itemImage.sprite = icon;
        _translationText = translationText;
        UpdateLanguage();
    }


    public void PrepareUpgradeObject(Sprite icon, string translationText)
    {
        _itemImage.sprite = icon;
        _translationText = translationText;
        UpdateLanguage();
    }

    private void OnEnable()
    {
        _buyButton.onClick.AddListener(HandleBuyButton);
        LeanLocalization.OnLocalizationChanged += UpdateLanguage;
        UpdateLanguage();
    }

    private void HandleBuyButton()
    {
        OnObjectAddButtonClicked?.Invoke();
        //_buyButton.gameObject.SetActive(false);
    }

    public void SetBuyPrice(double price)
    {
        _buyButtonText.text = $"{price.ToString("N0")}";
    }

    public void SetItemCount(int count, int maxCount)
        => _itemCount.text = $"{count} / {maxCount}";

    public void ToggleBuyButton(bool val)
        => _buyButton.interactable = val;

    public void DisableBuyPanel(bool val) => _buyButton.interactable = val;

    public void UpdateLanguage()
    {
        if (string.IsNullOrEmpty(_translationText))
            return;

        string key = _translationText switch
        {
            "Decorations" => "meta_category_decorations",
            "Large Weird Stuff" => "meta_category_large_weird_stuff",
            "Transport" => "meta_category_transport",
            _ => _translationText
        };
        _itemTitle.text = LeanLocalization.GetTranslationText(key, _translationText);
    }

    private void OnDisable()
    {
        LeanLocalization.OnLocalizationChanged -= UpdateLanguage;
        _buyButton.onClick.RemoveListener(HandleBuyButton);
    }
}

using Lean.Localization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaUpgradeObjectController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _itemTitle;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Image _diamondIcon;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private Image _itemImage;

    private double _currentBuyPrice;
    private string _translationText;

    public GameObject BuyButtonGameObject => _buyButton != null ? _buyButton.gameObject : null;

    public event Action OnObjectAddButtonClicked;

    private void Awake()
    {
        ToggleBuyButton(false);
    }
    public void PrepareVariantObject(Sprite icon, string translationText)
    {
        _itemImage.sprite = icon;
        _itemTitle.text = translationText;
        _translationText = translationText;
    }

    public void PrepareUpgradeObject(Sprite icon, string translationText)
    {
        _itemImage.sprite = icon;
        _itemTitle.text = translationText;
        _translationText = translationText;
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
    }

    public void SetBuyPrice(double price)
    {
        _currentBuyPrice = price;

        bool hasPrice = price > 0d;

        _diamondIcon.gameObject.SetActive(hasPrice);

        _buyButtonText.text = hasPrice
            ? $"{price:G}"
            : LeanLocalization.GetTranslationText("upgrade_add", "Add");
    }

    public void ToggleBuyButton(bool val)
        => _buyButton.interactable = val;

    public void DisableBuyPanel(bool val)
    {
        _buyButton.gameObject.SetActive(val);
        _buyButton.interactable = val;
    }
    public void UpdateLanguage()
    {
        SetBuyPrice(_currentBuyPrice);
    }

    private void OnDisable()
    {
        LeanLocalization.OnLocalizationChanged -= UpdateLanguage;
        _buyButton.onClick.RemoveListener(HandleBuyButton);
    }
}

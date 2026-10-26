using Lean.Localization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaUpgradeObjectController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _itemTitle;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private Image _itemImage;

    private string _translationText;

    public GameObject BuyButtonGameObject => _buyButton != null ? _buyButton.gameObject : null;

    public event Action OnObjectAddButtonClicked;

    private void Awake()
    {
        _buyButton.onClick.AddListener(HandleBuyButton);
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

    private void Start()
    {
        //_itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
    }

    private void HandleBuyButton()
    {
        OnObjectAddButtonClicked?.Invoke();
    }

    public void SetBuyPrice(double price)
    {
        _buyButtonText.text = price == 0d
            ? "ADD"
            : $"ADD {price:G}";
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
        //_itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
    }

    private void OnDisable()
    {
        _buyButton.onClick.RemoveListener(HandleBuyButton);
    }
}

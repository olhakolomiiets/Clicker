using Lean.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeItemController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _itemTitle;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private Image _itemImage;

    private string _translationText;

    public event Action OnUpgradeItemBuyButtonClicked;

    private void Awake()
    {
        _buyButton.onClick.AddListener(HandleBuyButton);
        ToggleBuyButton(false);
    }

    public void Prepare(Sprite icon, string translationText)
    {
        _itemImage.sprite = icon;
        _translationText = translationText;
        _itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
    }

    private void HandleBuyButton()
    {
        OnUpgradeItemBuyButtonClicked?.Invoke();
        _buyButton.gameObject.SetActive(false);
    }

    public void SetBuyPrice(double price)
    {
        _buyButtonText.text = $"{price.ToString("N0")}";
    }

    public void ToggleBuyButton(bool val)
        => _buyButton.interactable = val;

    public void DisableBuyPanel(bool val) => _buyButton.gameObject.SetActive(val);

    public void UpdateLanguage()
    {
        _itemTitle.text = LeanLocalization.GetTranslationText(_translationText);
    }

    private void OnDisable()
    {
        _buyButton.onClick.RemoveListener(HandleBuyButton);
    }
}

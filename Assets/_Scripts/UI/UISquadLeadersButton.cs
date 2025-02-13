using Lean.Localization;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISquadLeadersButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _priceText;
    [SerializeField] private TextMeshProUGUI _managerTitle;
    [SerializeField] private Button _buyButton;
    [SerializeField] private Sprite _puchasedSprite;
    [SerializeField] private Image _currency;

    public event Action OnClicked;
    private string _translationText;

    private void Awake()
    {
        _buyButton.onClick.AddListener(() => OnClicked?.Invoke());
    }
    public void SetValue(float value, Sprite currency)
    {
        _priceText.text = value.ToString("N0");
        _currency.sprite = currency;
    }

    public void ToggleActive(bool active) 
        => _buyButton.interactable = active;

    internal void SetPurchasedImage()
    {
        _buyButton.GetComponent<Image>().sprite = _puchasedSprite;
        ToggleActive(false);
    }
}

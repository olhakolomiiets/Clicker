using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopItemController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _infoText;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _buyButtonText;
    [SerializeField] private Image _itemImage;

    private void Awake()
    {
        ToggleBuyButton(false);
        ToggleIncome(false);
    }

    public void Prepare(Sprite icon)
    {
        _itemImage.sprite = icon;
    }

    public void SetBuyPrice(double price)
    {
        _buyButtonText.text = $"{price.ToString("F2")} $";
    }

    public void ToggleBuyButton(bool val)
        => _buyButton.interactable = val;


    public void ToggleIncome(bool val)
        => _infoText.gameObject.SetActive(val);
    public void SetIncome(double score)
    {
        _infoText.text = $"{score.ToString("F2")} $";
    }

    private void OnDisable()
    {

    }
}

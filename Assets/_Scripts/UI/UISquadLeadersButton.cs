using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISquadLeadersButton : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI _priceText;
    [SerializeField]
    private Button _buyButton;
    [SerializeField]
    private Sprite _puchasedSprite;

    public event Action OnClicked;

    private void Awake()
    {
        _buyButton.onClick.AddListener(() => OnClicked?.Invoke());
    }
    public void SetValue(string value)
    {
        _priceText.text = value;
    }

    public void ToggleActive(bool active) 
        => _buyButton.interactable = active;

    internal void SetPurchasedImage()
    {
        _buyButton.GetComponent<Image>().sprite = _puchasedSprite;
        ToggleActive(false);
    }
}

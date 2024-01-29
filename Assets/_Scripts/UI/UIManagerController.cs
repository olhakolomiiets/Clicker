using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManagerController : MonoBehaviour
{
    [SerializeField] private GameObject _managerButton;       
    [SerializeField] private GameObject _buyManagersPanel;
    [SerializeField] private GameObject _buyManagerButtonPrefab;
    [SerializeField] private UISquadLeadersButton _buyButton;

    public event Action<int> OnManagerPurchased;

    private void Awake()
    {
        _buyManagersPanel = GameObject.FindGameObjectWithTag("ManagersPanel");
    }

    public void AddButton(int index, float price)
    {
        GameObject buttonObject = Instantiate(_buyManagerButtonPrefab, _buyManagersPanel.transform);
        _buyButton = buttonObject.GetComponent<UISquadLeadersButton>();

        _buyButton.SetValue(price.ToString());
        int i = index;
        _buyButton.OnClicked += () => OnManagerPurchased?.Invoke(i);
        _buyButton.ToggleActive(false);

        _buyButton.gameObject.SetActive(false);
    }
    public void ToggleManagerPanel()
    { 
        _buyButton.gameObject.SetActive(!_buyButton.gameObject.activeSelf);

        if (_buyButton.gameObject.activeSelf == false)
            return;
    }

    public void ToggleButton(int index, bool val)
        => _buyButton.ToggleActive(val);

    internal void SetButtonPurchased(int index, bool val)
    {
        if(val)
        {
            _buyButton.SetPurchasedImage();
            _managerButton.SetActive(false);
            _buyButton.gameObject.SetActive(false);
        }

    }
}

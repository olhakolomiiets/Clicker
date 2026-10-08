using MoreMountains.Feedbacks;
using MoreMountains.Tools;
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
    [SerializeField] private RectTransform _panelTransform;
    private int _managerIndex;

    public event Action<int> OnManagerPurchased;
    public event Action<int> OnManagerPanelOpened;
    public GameObject ManagerButtonTarget => _managerButton != null
        ? _managerButton.GetComponentInChildren<Button>(true)?.gameObject
        : null;
    public GameObject ManagerBuyButtonTarget => _buyButton != null
        ? _buyButton.ButtonTarget
        : null;

    private void Awake()
    {
        _buyManagersPanel = GameObject.FindGameObjectWithTag("ManagersPanel");
    }

    public void AddButton(int index, float price, Sprite currency)
    {
        GameObject buttonObject = Instantiate(_buyManagerButtonPrefab, _buyManagersPanel.transform);
        _buyButton = buttonObject.GetComponent<UISquadLeadersButton>();

        _buyButton.SetValue(price, currency);
        int i = index;
        _buyButton.OnClicked += () => OnManagerPurchased?.Invoke(i);
        _managerIndex = index;
        _buyButton.ToggleActive(false);

        _buyButton.gameObject.SetActive(false);
    }
    public void ToggleManagerPanel()
    { 
        _buyButton.gameObject.SetActive(!_buyButton.gameObject.activeSelf);

        if (_buyButton.gameObject.activeSelf == false)
            return;

        OnManagerPanelOpened?.Invoke(_managerIndex);
    }

    public void ToggleButton(int index, bool val)
        => _buyButton.ToggleActive(val);

    internal void SetButtonPurchased(int index, bool val)
    {
        if(val)
        {
            _buyButton.SetPurchasedImage();
            _managerButton.SetActive(false);
            //_panelTransform.sizeDelta = new Vector2(980, 185);
            _buyButton.gameObject.SetActive(false);

        }

    }
}

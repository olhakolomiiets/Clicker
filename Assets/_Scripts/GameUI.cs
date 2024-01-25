using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the information exchange between UI and other scripts
/// </summary>
public class GameUI : MonoBehaviour
{
    [SerializeField]
    private GameObject _uiItemPrefab;
    [SerializeField]
    private Transform _uiItemParent;
    
    private UIManagerController _managerController;

    [SerializeField] private List<UIManagerController> _managerControllers = new();

    [SerializeField] private List<ItemController> _uiItemsList = new();


    [SerializeField] private List<ObjectActivator> _objectActivator = new(); 
    [SerializeField]
    private ScorePanel _scorePanel;

    [SerializeField] private RectTransform _scrollItemGroup;
    private float _scrollItemGroupHeight;

    public event Action<int> OnProgressButtonClicked, OnWorkFinished, OnBuyButonClicked, OnPurchaseItemFirstTime, OnManagerPurchased;

    public void PrepareUI(List<ItemData> data)
    {
        _uiItemsList.Clear();
        _managerControllers.Clear();
        for (int i = 0; i < data.Count; i++)
        {
            ItemController itemController
                = Instantiate(_uiItemPrefab, _uiItemParent).GetComponent<ItemController>();            
            itemController.Prepare(data[i].ItemImage);
            _uiItemsList.Add(itemController);

            _managerController = itemController.GetComponent<UIManagerController>();
            _managerControllers.Add(_managerController);

            _objectActivator[i].itemController = itemController;

            _managerControllers[i].AddButton(i, data[i].ManagerPrice);

            ConnectEvents(i, itemController);

            _scrollItemGroupHeight = 220 * data.Count;
            _scrollItemGroup.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

            _managerControllers[i].OnManagerPurchased += PurchaseManager;
        }        
    }

    private void PurchaseManager(int index)
    {
        OnManagerPurchased?.Invoke(index);
    }


    private void ConnectEvents(int i, ItemController itemController)
    {
        itemController.OnProgressButtonClicked += () => OnProgressButtonClicked?.Invoke(i);
        itemController.OnWorkFinished += () => OnWorkFinished?.Invoke(i);
        itemController.OnBuyButtonClicked += () => OnBuyButonClicked?.Invoke(i);
        itemController.OnFirstActivation += () => OnPurchaseItemFirstTime?.Invoke(i);
    }

    public void UpdateManagerAvailability(int index, bool val)
    {
        _managerControllers[index].ToggleButton(index, val);
    }

    public void StartWorkOnItem(int index, float delay)
    {
        _uiItemsList[index].StartWork(delay);
    }

    public void ToggleItemActiveState(int index, bool val)
    {
        _uiItemsList[index].ToggleActivation(val);
    }

    public void UpdateUI(int index, GameData gameData)
    {
        _managerControllers[index].SetButtonPurchased(index, gameData.Managers[index]);
       

        _uiItemsList[index].SetIncome(gameData.ItemDataList[index].ItemIncome(gameData.ItemCount[index], gameData.ItemBonusMultiplayer[index]));
        _uiItemsList[index].SetBuyPrice(gameData.ItemDataList[index].ItemUpgradePrice(gameData.ItemCount[index]));
        _uiItemsList[index].SetItemCount(gameData.ItemCount[index], gameData.ItemDataList[index].MaxCount(gameData.ItemBonusMultiplayer[index], gameData.ItemMaxCountHelper[index]));
        _scorePanel.SetScore(gameData.Money);
        _uiItemsList[index].ToggleBuyButton(gameData.Money >= gameData.ItemDataList[index].ItemUpgradePrice(gameData.ItemCount[index]));
    }

    internal void ActivateItem(int index)
    {
        _uiItemsList[index].ActivateButton();
    }
}

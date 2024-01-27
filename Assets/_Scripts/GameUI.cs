using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles the information exchange between UI and other scripts
/// </summary>
public class GameUI : MonoBehaviour
{
    [Header("Score Panel")]
    [SerializeField] private ScorePanel _coins;
    [SerializeField] private ScorePanel _diamonds;

    [Header("Creation Store")]
    [SerializeField] private GameObject _uiItemPrefab;
    [SerializeField] private RectTransform _uiItemParent;
    [SerializeField] private List<ObjectActivator> _objectActivator = new();        
    private List<UIManagerController> _managerControllers = new();
    private List<ItemController> _uiCreationItemsList = new();

    //[Header("Upgrade Store")]

    //[Header("Shop")]


    //private UIManagerController _managerController;

    public event Action<int> OnProgressButtonClicked, OnWorkFinished, OnBuyButonClicked, OnPurchaseItemFirstTime, OnManagerPurchased;

    public void PrepareUI(List<ItemData> data)
    {
        _uiCreationItemsList.Clear();
        _managerControllers.Clear();
        for (int i = 0; i < data.Count; i++)
        {
            ItemController itemController = Instantiate(_uiItemPrefab, _uiItemParent).GetComponent<ItemController>();            
            itemController.Prepare(data[i].ItemImage);
            _uiCreationItemsList.Add(itemController);

            UIManagerController _managerController = itemController.GetComponent<UIManagerController>();
            _managerControllers.Add(_managerController);

            _objectActivator[i].itemController = itemController;

            _managerControllers[i].AddButton(i, data[i].ManagerPrice);

            ConnectEvents(i, itemController);

            float _scrollItemGroupHeight = 250 * data.Count;
            _uiItemParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

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
        _uiCreationItemsList[index].StartWork(delay);
    }

    public void ToggleItemActiveState(int index, bool val)
    {
        _uiCreationItemsList[index].ToggleActivation(val);
    }

    public void UpdateUI(int index, GameData gameData)
    {
        _managerControllers[index].SetButtonPurchased(index, gameData.Managers[index]);
        _uiCreationItemsList[index].SetIncome(gameData.ItemDataList[index].ItemIncome(gameData.ItemCount[index], gameData.ItemBonusMultiplayer[index]));
        _uiCreationItemsList[index].SetBuyPrice(gameData.ItemDataList[index].ItemUpgradePrice(gameData.ItemCount[index]));

        _uiCreationItemsList[index].SetItemCount(gameData.ItemCount[index], gameData.ItemDataList[index].MaxCount(gameData.ItemBonusMultiplayer[index], gameData.ItemMaxCountHelper[index]));
        _coins.SetScore(gameData.Money);

        _diamonds.SetDiamondsScore(gameData.Diamonds);

        _uiCreationItemsList[index].ToggleBuyButton(gameData.Money >= gameData.ItemDataList[index].ItemUpgradePrice(gameData.ItemCount[index]));
    }

    internal void ActivateItem(int index)
    {
        _uiCreationItemsList[index].ActivateButton();
    }
}

using System.Collections.Generic;
using UnityEngine;

public class StandardPlanetMode : MonoBehaviour, IPlanetMode
{
    [SerializeField] private GameUI _gameUI;
    [SerializeField] private GameRules _gameRules;

    [SerializeField] private List<ItemData> _creationItemsDataList;
    [SerializeField] private List<int> _creationItemsCount = new();

    [SerializeField] private List<UpgradeItemData> _upgradeItemsDataList;
    [SerializeField] private List<int> _upgradeItemCount = new();

    [SerializeField] private CurrencyUI _currencyUI;

    private GameData _gameData;
    private GeneralGameData _generalGameData;

    public void Initialize(GeneralGameData generalGameData)
    {
        _generalGameData = generalGameData;

        PrepareGameData();
        PrepareUI();

        _gameRules.PrepareGameData(_gameData, _generalGameData);
    }

    public void UpdateData()
    {
        _gameRules.SendDataUpdate();
    }

    public void Load(string data)
    {
        if (!string.IsNullOrEmpty(data))
            _gameRules.LoadPlanet(data);

        _creationItemsCount = _gameData.ItemCount;
        _upgradeItemCount = _gameData.UpgradeItemCount;

        _gameUI.ActivatePurchasedCreationObject(_creationItemsCount);
        _gameUI.ActivatePurchasedUpgradeObject(_upgradeItemCount);
    }

    public string Save()
    {
        return _gameData.GetSaveData();
    }

    private void PrepareGameData()
    {
        _gameData = new GameData
        {
            ItemDataList = _creationItemsDataList,
            UpgradeItemDataList = _upgradeItemsDataList
        };
    }

    private void PrepareUI()
    {
        _gameUI.PrepareCreationUI(_creationItemsDataList);
        _gameUI.PrepareUpgradeUI(_upgradeItemsDataList);

        _gameUI.OnProgressButtonClicked += _gameRules.HandleStartItemProgress;
        _gameUI.OnWorkFinished += _gameRules.IncreaseScore;
        _gameUI.OnWorkFinished += _gameRules.HandleManager;
        _gameUI.OnUpgradeItemPurchased += _gameRules.HandleDiamondsUpgrade;
        _gameUI.OnBuyButonClicked += _gameRules.HandleUpgrade;
        _gameUI.OnActivationPremium += _gameRules.HandlePremiumManager;
        _gameUI.OnPurchaseItemFirstTime += _gameRules.PurchaseItemFirstTime;
        _gameUI.OnManagerPurchased += _gameRules.HandleManagerPurchased;

        _gameRules.OnModifyManagerAvailability += _gameUI.UpdateManagerAvailability;
        _gameRules.OnActivateItem += _gameUI.ActivateItem;
        _gameRules.OnStartWorkOnItem += _gameUI.StartWorkOnItem;
        _gameRules.OnToggleItemActivationState += _gameUI.ToggleItemActiveState;
        _gameRules.OnUpdateData += _gameUI.UpdateUI;
        _gameRules.OnUpdateUpgradeData += _gameUI.UpdateUpgradeUI;

        _gameRules.OnUpdateGameData += _currencyUI.UpdateCurrency;
    }

    public void AddPurchasedPack(double coins, double diamonds)
    {
        _gameRules.GetPurchasedProduct(coins, diamonds);
    }

    public void AddPurchasedDiamonds(double diamonds)
    {
        _gameRules.GetPurchasedProduct(diamonds);
    }

    public void ActivatePurchasedBooster()
    {
        _gameRules.GetPurchasedBooster();
    }
}
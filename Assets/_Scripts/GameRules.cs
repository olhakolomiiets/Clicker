using System;
using UnityEngine;
using Firebase.Analytics;

public class GameRules : MonoBehaviour
{
    private GameData _currentGameData;
    private GeneralGameData _currentGeneralData;

    public event Action<int, bool> OnModifyManagerAvailability, OnToggleItemActivationState;
    public event Action<int, float> OnStartWorkOnItem, OnStartWorkOnUpgradeItem;
    public event Action<int> OnActivateItem, OnActivateUpgradeItem, OnAutomateItem, OnActivatePassiveIncome;
    public event Action<int, double> OnItemIncomeEarned;
    public event Action<int> OnManagerPurchasedConfirmed, OnItemFirstPurchaseConfirmed, OnItemUpgradedConfirmed;
    public event Action<int, GameData, GeneralGameData> OnUpdateData, OnPerformAction, OnUpdateUpgradeData;
    public event Action<GeneralGameData, GameData> OnUpdateGameData;
    public event Action OnDataUpdated, OnRewardRecived;
    public event Action<int, int> OnManagerAvailability, OnManagerNonAvailability, OnItemNotReadyToBuy, OnItemReadyToBuy;

    public double CurrentMoney => _currentGameData != null ? _currentGameData.Money : 0d;

    /// <summary>
    /// True once <see cref="PrepareGameData"/> has assigned the active planet data.
    /// Tutorial controllers use this to wait until item/economy state is available.
    /// </summary>
    public bool IsPrepared => _currentGameData != null;

    /// <summary>Number of Creation objects of the given index the player currently owns.</summary>
    public int GetItemCount(int index)
    {
        if (_currentGameData == null || _currentGameData.ItemCount == null)
            return 0;

        return index >= 0 && index < _currentGameData.ItemCount.Count ? _currentGameData.ItemCount[index] : 0;
    }

    /// <summary>True when at least one object of the given Creation index is owned.</summary>
    public bool IsItemOwned(int index) => GetItemCount(index) > 0;

    /// <summary>True when the automation manager for the given Creation index is purchased.</summary>
    public bool IsManagerPurchased(int index)
    {
        if (_currentGameData == null || _currentGameData.Managers == null)
            return false;

        return index >= 0 && index < _currentGameData.Managers.Count && _currentGameData.Managers[index];
    }

    /// <summary>Cost (coins) to buy the next copy of the given Creation index at its current owned count.</summary>
    public double GetItemUpgradeCost(int index)
    {
        if (_currentGameData == null || _currentGameData.ItemDataList == null)
            return 0d;

        return index >= 0 && index < _currentGameData.ItemDataList.Count
            ? _currentGameData.ItemDataList[index].ItemUpgradePrice(GetItemCount(index))
            : 0d;
    }

    /// <summary>Cost (coins) of the automation manager for the given Creation index.</summary>
    public double GetManagerPrice(int index)
    {
        if (_currentGameData == null || _currentGameData.ItemDataList == null)
            return 0d;

        return index >= 0 && index < _currentGameData.ItemDataList.Count
            ? _currentGameData.ItemDataList[index].ManagerPrice
            : 0d;
    }

    /// <summary>True when the given Creation item uses the premium currency path.</summary>
    public bool IsItemPremium(int index)
    {
        if (_currentGameData == null || _currentGameData.ItemDataList == null)
            return false;

        return index >= 0 && index < _currentGameData.ItemDataList.Count &&
               _currentGameData.ItemDataList[index].IsPremium;
    }

    private int timeAfterExit;
    private int itemIndex;
    private bool isTipShown;

    public double _totalScore;

    [SerializeField] private double moneyForTreeHint;
    [SerializeField] private double moneyForManagerHint;
    [SerializeField] private double moneyForUpgradeHint;
    [SerializeField] private string planetKey;
    [SerializeField] private int requiredObjects = 6;

    [Header("Currency")]
    [SerializeField] private string currencyCode;
    private string diamondCode = "DI";
    private System.Action _savePassiveIncomeReward;

    private bool _hasPassiveIncomeQuote;
    private double _passiveX1;
    private double _passiveX2;
    private double _passiveX3;
    private double _passiveX3Price;

    public void SetPassiveIncomeSaver(System.Action saver)
    {
        _savePassiveIncomeReward = saver;
    }

    public void PrepareGameData(GameData gameData, GeneralGameData generalData)
    {
        _currentGameData = gameData;
        _currentGeneralData = generalData;

        for (int i = 0; i < gameData.ItemDataList.Count; i++)
        {
            _currentGameData.ItemCount.Add(i == 0 ? 1 : 0);
            _currentGameData.ItemBonusMultiplayer.Add(1);
            _currentGameData.ItemMaxCountHelper.Add(0);
            _currentGameData.Managers.Add(false);
        }

        for (int i = 0; i < gameData.UpgradeItemDataList.Count; i++)
        {
            _currentGameData.UpgradeItemCount.Add(0);
        }

        SendDataUpdate();
        OnActivateItem?.Invoke(0);
    }

    public void PrepareGeneralData(GeneralGameData generalGameData)
    {
        _currentGeneralData = generalGameData;
    }

    public string GetSaveData()
    {
        return _currentGameData.GetSaveData();
    }

    public void LoadPlanet(string gameDataSave)
    {
        if (string.IsNullOrEmpty(gameDataSave))
            return;

        _currentGameData.SetData(gameDataSave);

        for (int i = 0; i < _currentGameData.ItemDataList.Count; i++)
        {
            if (_currentGameData.ItemCount[i] > 0)
                ActivateItem(i);

            HandleManager(i);
        }

        OnUpdateGameData?.Invoke(_currentGeneralData, _currentGameData);
        SendDataUpdate();
    }

    public void LoadGame(string gameDataSave)
    {
        if (string.IsNullOrEmpty(gameDataSave))
            return;

        _currentGeneralData.SetData(gameDataSave);

        OnUpdateGameData?.Invoke(_currentGeneralData, _currentGameData);

        if (_currentGeneralData.ExitTime != null)
        {
            long tempExitTime = Convert.ToInt64(_currentGeneralData.ExitTime);

            DateTime exitTime = DateTime.FromBinary(tempExitTime);
            DateTime currentTime = DateTime.Now;
            TimeSpan difference = currentTime.Subtract(exitTime);

            timeAfterExit = (int)difference.TotalSeconds;

            OnActivatePassiveIncome?.Invoke(timeAfterExit);
        }

        SendDataUpdate();
    }

    public void SendDataUpdate()
    {
        for (int i = 0; i < _currentGameData.ItemDataList.Count; i++)
        {
            UnlockOtherItems(i);
            UnlockManagers(i);
            CheckBonusMultiplier(i);
            CheckBoosterMultiplier(i);

            OnUpdateData?.Invoke(i, _currentGameData, _currentGeneralData);
        }

        CalculateMoneyPerSec();

        for (int i = 0; i < _currentGameData.UpgradeItemDataList.Count; i++)
        {
            OnUpdateUpgradeData?.Invoke(i, _currentGameData, _currentGeneralData);
        }

        OnUpdateGameData?.Invoke(_currentGeneralData, _currentGameData);

        OnDataUpdated?.Invoke();
    }

    public void HandleManagerPurchased(int index)
    {
        if (_currentGameData.Managers[index])
            return;

        _currentGameData.Money -= _currentGameData.ItemDataList[index].ManagerPrice;
        _currentGameData.Managers[index] = true;

        OnManagerNonAvailability?.Invoke(index, 1);
        isTipShown = false;

        HandleManager(index);

        _currentGameData.IsManagerPurchased += 1;
        Debug.Log($"[PlanetTutorialTrace] manager purchase confirmed index={index}", this);
        OnManagerPurchasedConfirmed?.Invoke(index);

        FirebaseAnalytics.LogEvent("auto_purchased");
    }

    public void HandlePremiumManager(int index)
    {
        if (_currentGameData.Managers[index])
            return;

        _currentGameData.Managers[index] = true;
        ActivateManagerFor(index);
    }

    private void ActivateManagerFor(int index)
    {
        AutomateTask(index);
        SendDataUpdate();
    }

    public void HandleManager(int index)
    {
        if (_currentGameData.Managers[index])
            AutomateTask(index);
    }

    private void AutomateTask(int index)
    {
        HandleStartItemProgress(index);
    }

    public void PurchaseItemFirstTime(int index)
    {
        _currentGameData.Money -= _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        _currentGameData.ItemCount[index] = 1;

        OnItemNotReadyToBuy?.Invoke(index, 0);
        isTipShown = false;

        ActivateItem(index);
        Debug.Log($"[PlanetTutorialTrace] first item purchase confirmed index={index}", this);
        OnItemFirstPurchaseConfirmed?.Invoke(index);

        FirebaseAnalytics.LogEvent("creation_category_purchased");
    }

    private void ActivateItem(int index)
    {
        OnActivateItem?.Invoke(index);
        SendDataUpdate();
    }

    public void IncreaseScore(int index)
    {
        if (_currentGameData.ItemDataList[index].IsPremium)
        {
            ItemData itemData = _currentGameData.ItemDataList[index];

            double income = itemData.DiamondsIncome(
                _currentGameData.ItemCount[index]);

            double metaMultiplier =
                _currentGeneralData?.MetaUpgradeEffects?.GetIncomeMultiplier(itemData) ?? 1d;

            _currentGeneralData.Diamonds += income * metaMultiplier;
        }
        else
        {
            ItemData itemData = _currentGameData.ItemDataList[index];

            double income = itemData.ItemIncome(
                _currentGameData.ItemCount[index],
                _currentGameData.ItemBonusMultiplayer[index]);

            double metaMultiplier =
                _currentGeneralData?.MetaUpgradeEffects?.GetIncomeMultiplier(itemData) ?? 1d;

            income *= metaMultiplier;

            _currentGameData.Money += income;
            _currentGeneralData.TotalScore += income;
            _totalScore = _currentGeneralData.TotalScore;
            //Debug.Log($"[PlanetTutorialTrace] income confirmed index={index} income={income} money={_currentGameData.Money}", this);
            OnItemIncomeEarned?.Invoke(index, income);
        }

        SendDataUpdate();
    }

    public void HandleStartItemProgress(int index)
    {
        OnPerformAction?.Invoke(index, _currentGameData, _currentGeneralData);

        ItemData itemData = _currentGameData.ItemDataList[index];

        double timeMultiplier =
            _currentGeneralData?.MetaUpgradeEffects?.GetTimeMultiplier(itemData) ?? 1d;

        float effectiveDelay = Mathf.Max(
            0.1f,
            (float)(itemData.Delay * timeMultiplier)
        );

        OnStartWorkOnItem?.Invoke(index, effectiveDelay);
    }

    public void HandleUpgrade(int index)
    {
        _currentGameData.Money -= _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        _currentGameData.ItemCount[index] += 1;

        SendDataUpdate();
        //Debug.Log($"[PlanetTutorialTrace] item upgrade confirmed index={index} count={_currentGameData.ItemCount[index]}", this);
        OnItemUpgradedConfirmed?.Invoke(index);
    }

    public void HandleDiamondsUpgrade(int index)
    {
        _currentGeneralData.Diamonds -= _currentGameData.UpgradeItemDataList[index].ItemCost;
        _currentGameData.UpgradeItemCount[index] += 1;

        SendDataUpdate();
    }

    private void CalculateMoneyPerSec()
    {
        _currentGameData.MoneyPerSec = 0;

        for (int i = 0; i < _currentGameData.ItemDataList.Count; i++)
        {
            ItemData itemData = _currentGameData.ItemDataList[i];

            if (itemData.IsPremium)
                continue;

            double incomePerCycle = itemData.ItemIncome(_currentGameData.ItemCount[i], _currentGameData.ItemBonusMultiplayer[i]);

            double timeMultiplier =
                _currentGeneralData?.MetaUpgradeEffects?.GetTimeMultiplier(itemData) ?? 1d;

            float effectiveDelay = Mathf.Max(
                0.1f,
                (float)(itemData.Delay * timeMultiplier)
            );

            double metaMultiplier = itemData.IsPremium
                ? 1d
                : (_currentGeneralData?.MetaUpgradeEffects?.GetIncomeMultiplier(itemData) ?? 1d);

            _currentGameData.MoneyPerSec += incomePerCycle * metaMultiplier / effectiveDelay;
        }
    }

    private void UnlockManagers(int index)
    {
        if (_currentGameData.Managers[index])
            return;

        bool canBuy = _currentGameData.ItemDataList[index].ManagerPrice < _currentGameData.Money;

        OnModifyManagerAvailability?.Invoke(index, canBuy);

        if (canBuy && !isTipShown && !_currentGameData.ItemDataList[index].IsPremium)
        {
            OnManagerAvailability?.Invoke(index, 1);
            isTipShown = true;
        }
        else
        {
            OnManagerNonAvailability?.Invoke(index, 1);
            isTipShown = false;
        }
    }

    private void UnlockOtherItems(int index)
    {
        if (_currentGameData.ItemCount[index] != 0)
            return;

        bool canBuy = _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]) < _currentGameData.Money;

        OnToggleItemActivationState?.Invoke(index, canBuy);

        if (canBuy && !isTipShown)
        {
            itemIndex = index;
            OnItemReadyToBuy?.Invoke(index, 0);
            isTipShown = true;
        }
        else
        {
            OnItemNotReadyToBuy?.Invoke(index, 0);
            isTipShown = false;
        }
    }

    private void CheckBonusMultiplier(int index)
    {
        if (_currentGameData.ItemCount[index] >= _currentGameData.ItemDataList[index].MaxCount(
                _currentGameData.ItemBonusMultiplayer[index],
                _currentGameData.ItemMaxCountHelper[index])
            && _currentGameData.ItemCount[index] < _currentGameData.ItemDataList[index].MaxCountIncrement)
        {
            _currentGameData.ItemBonusMultiplayer[index] *= 2;

            if (_currentGameData.ItemBonusMultiplayer[index] >= _currentGameData.ItemDataList[index].BonusMaxCountThreshold)
                _currentGameData.ItemMaxCountHelper[index] = _currentGameData.ItemDataList[index].MaxCountIncrement;
        }
    }

    private void CheckBoosterMultiplier(int index)
    {
        if (_currentGameData.BoosterMultiplier == 0)
            _currentGameData.BoosterMultiplier = 1;

        if (!_currentGameData.ItemDataList[index].IsPremium)
            _currentGameData.ItemDataList[index].BoosterMultiplier = _currentGameData.BoosterMultiplier;
    }

    public void Get10KMoney()
    {
        _currentGameData.Money += 10000;
        _currentGeneralData.TotalScore += 10000;

        SendDataUpdate();
    }

    public void Get50MMoney()
    {
        _currentGameData.Money += 50000000;
        _currentGeneralData.TotalScore += 50000000;

        SendDataUpdate();
    }

    public void GetDiamonds(int reward)
    {
        _currentGeneralData.Diamonds += reward;
        SendDataUpdate();
    }

    public void GetExtraDiamonds(int reward)
    {
        _currentGeneralData.Diamonds += reward;
        Debug.Log("GameRules /// Add INT " + reward + " Diamonds");
    }

    public void SetPassiveIncomeQuote(
    double x1,
    double x2,
    double x3,
    double x3Price)
    {
        if (_currentGeneralData == null ||
            _currentGeneralData.PendingOfflineSeconds <= 0)
            return;

        if (x1 <= 0d ||
            double.IsNaN(x1) ||
            double.IsInfinity(x1) ||
            double.IsNaN(x2) ||
            double.IsInfinity(x2) ||
            double.IsNaN(x3) ||
            double.IsInfinity(x3) ||
            double.IsNaN(x3Price) ||
            double.IsInfinity(x3Price) ||
            x3Price < 0d)
            return;

        if (x2 != Math.Truncate(x1) * 2d ||
            x3 != Math.Truncate(x1) * 3d)
            return;

        _passiveX1 = x1;
        _passiveX2 = x2;
        _passiveX3 = x3;
        _passiveX3Price = x3Price;

        _hasPassiveIncomeQuote = true;
    }

    public void GetPassiveIncome(double income, double diamonds)
    {
        if (_currentGameData == null ||
            _currentGeneralData == null ||
            _savePassiveIncomeReward == null)
            return;

        if (_currentGeneralData.PendingOfflineSeconds <= 0)
            return;

        if (income <= 0d ||
            double.IsNaN(income) ||
            double.IsInfinity(income) ||
            diamonds < 0d ||
            double.IsNaN(diamonds) ||
            double.IsInfinity(diamonds) ||
            _currentGeneralData.Diamonds < diamonds)
            return;

        if (!_hasPassiveIncomeQuote)
            return;

        bool validReward =
            (diamonds == 0d &&
                (income == _passiveX1 || income == _passiveX2)) ||
            (_passiveX3Price > 0d &&
                diamonds == _passiveX3Price &&
                income == _passiveX3);

        if (!validReward)
            return;

        _currentGameData.Money += income;
        _currentGeneralData.TotalScore += income;
        _totalScore = _currentGeneralData.TotalScore;

        _currentGeneralData.Diamonds -= diamonds;

        _currentGeneralData.ClearPendingOfflineTime();
        _hasPassiveIncomeQuote = false;

        _savePassiveIncomeReward();

        SendDataUpdate();

        FirebaseAnalytics.LogEvent("triple_passive_income_received");
    }

    public void TryActivatePassiveIncome()
    {
        if (_currentGeneralData == null || _currentGameData == null)
            return;

        Debug.Log(
            $"[PassiveIncome] REQUEST | " +
            $"Pending={_currentGeneralData.PendingOfflineSeconds}s | " +
            $"MoneyPerSec={_currentGameData.MoneyPerSec} | " +
            $"Managers={_currentGameData.IsManagerPurchased}"
        );

        OnActivatePassiveIncome?.Invoke(
            _currentGeneralData.PendingOfflineSeconds
        );
    }

    public void UpdatePassiveIncomeTime(double price, int time)
    {
        _currentGeneralData.Diamonds -= price;
        _currentGeneralData.PassiveIncomeTime += time;
        _currentGeneralData.ExtraTimePurchasedCount++;

        SendDataUpdate();

        FirebaseAnalytics.LogEvent("passive_income_time_updated");
    }

    public void GetReward(double reward)
    {
        _currentGameData.Money += reward;
        _currentGeneralData.TotalScore += reward;
        _totalScore = _currentGeneralData.TotalScore;

        SendDataUpdate();

        FirebaseAnalytics.LogEvent("coins_for_ads");
    }

    public void GetDiamonds(double reward)
    {
        _currentGeneralData.Diamonds += reward;

        SendDataUpdate();

        FirebaseAnalytics.LogEvent("diamonds_for_ads");
    }

    public void GetCoinsBooster()
    {
        _currentGameData.BoosterMultiplier = _currentGameData.IsBoosterPurchased ? 3 : 2;

        SendDataUpdate();

        FirebaseAnalytics.LogEvent("booster_for_ads");
        Debug.Log("GameRules /// GetCoinsBooster");
    }

    public void DisableCoinsBooster()
    {
        _currentGameData.BoosterMultiplier -= 1;

        SendDataUpdate();
    }

    public void GetPurchasedProduct(double coins, double diamonds)
    {
        _currentGameData.Money += coins;
        _currentGeneralData.Diamonds += diamonds;

        SendDataUpdate();
    }

    public void GetPurchasedProduct(double diamonds)
    {
        _currentGeneralData.Diamonds += diamonds;

        SendDataUpdate();
    }

    public void GetPurchasedBooster()
    {
        _currentGameData.BoosterMultiplier += _currentGameData.BoosterMultiplier == 0 ? 2 : 1;
        _currentGameData.IsBoosterPurchased = true;

        SendDataUpdate();
    }

    public void HandlePlanet(double planetPrice)
    {
        _currentGameData.Money -= planetPrice;
        _currentGeneralData.ActivePlanet++;

        FirebaseAnalytics.LogEvent(_currentGeneralData.ActivePlanet + "_planet_unlocked");
        Debug.Log($"New planet unlocked: {_currentGeneralData.ActivePlanet}");

        SendDataUpdate();
    }
}

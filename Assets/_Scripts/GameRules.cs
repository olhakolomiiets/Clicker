using System;
using UnityEngine;
using Firebase.Analytics;

public class GameRules : MonoBehaviour
{
    private GameData _currentGameData;
    private GeneralGameData _currentGeneralData;

    public event Action<int, bool> OnModifyManagerAvailability, OnToggleItemActivationState;
    public event Action<int, float> OnStartWorkOnItem, OnStartWorkOnUpgradeItem;
    public event Action<int> OnActivateItem, OnActivateUpgradeItem, OnAutomateItem, OnActivatePassiveIncome, OnUpgradeTutorialStepReady;
    public event Action<int, GameData, GeneralGameData> OnUpdateData, OnPerformAction, OnUpdateUpgradeData;
    public event Action<GeneralGameData, GameData> OnUpdateGameData;
    public event Action OnTutorialStepCompleted, OnTutorialNonCompleted, OnDataUpdated, OnRewardRecived;
    public event Action<int, int> OnTutorialStepReady, OnManagerAvailability, OnManagerNonAvailability, OnItemNotReadyToBuy, OnItemReadyToBuy;

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

        if (PlayerPrefs.GetInt("TutorialCompleted") == 0)
            CheckTutorialStep();

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

        if (PlayerPrefs.GetInt("TutorialStep") == 3)
            OnTutorialStepCompleted?.Invoke();

        OnItemNotReadyToBuy?.Invoke(index, 0);
        isTipShown = false;

        ActivateItem(index);

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
            _currentGeneralData.Diamonds += _currentGameData.ItemDataList[index].DiamondsIncome(_currentGameData.ItemCount[index]);
        }
        else
        {
            double income = _currentGameData.ItemDataList[index].ItemIncome(
                _currentGameData.ItemCount[index],
                _currentGameData.ItemBonusMultiplayer[index]);

            _currentGameData.Money += income;
            _currentGeneralData.TotalScore += income;
            _totalScore = _currentGeneralData.TotalScore;
        }

        SendDataUpdate();
    }

    public void HandleStartItemProgress(int index)
    {
        OnPerformAction?.Invoke(index, _currentGameData, _currentGeneralData);
        OnStartWorkOnItem?.Invoke(index, _currentGameData.ItemDataList[index].Delay);
    }

    public void HandleUpgrade(int index)
    {
        _currentGameData.Money -= _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        _currentGameData.ItemCount[index] += 1;

        SendDataUpdate();
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
            _currentGameData.MoneyPerSec += _currentGameData.ItemDataList[i].ItemIncomePerSec(
                _currentGameData.ItemCount[i],
                _currentGameData.ItemBonusMultiplayer[i]);
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

    public void GetPassiveIncome(double income, double diamonds)
    {
        _currentGameData.Money += income;
        _currentGeneralData.TotalScore += income;
        _totalScore = _currentGeneralData.TotalScore;

        _currentGeneralData.Diamonds -= diamonds;

        SendDataUpdate();

        FirebaseAnalytics.LogEvent("triple_passive_income_received");
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

    private void CheckTutorialStep()
    {
        int tutorialStep = PlayerPrefs.GetInt("TutorialStep");

        if (tutorialStep == 1 && _currentGameData.Money >= 5)
            OnTutorialStepCompleted?.Invoke();

        if ((tutorialStep == 3 && _currentGameData.Money >= moneyForTreeHint)
            || (tutorialStep == 4 && _currentGameData.Money >= moneyForManagerHint)
            || (tutorialStep == 5 && _currentGameData.Money >= moneyForUpgradeHint))
        {
            OnTutorialStepReady?.Invoke(tutorialStep, 1);
        }
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
using System;
using UnityEngine;
using System.Collections.Generic;
using Firebase.Analytics;

public class GameRules : MonoBehaviour
{
    /// <summary>
    /// Reference to the GameData passed by the GameManager
    /// </summary>
    GameData _currentGameData;
    GeneralGameData _currentGeneralData;
    // I want to use events to keep the classes unaware of each other
    public event Action<int, bool> OnModifyManagerAvailability, OnToggleItemActivationState;
    public event Action<int, float> OnStartWorkOnItem, OnStartWorkOnUpgradeItem;
    public event Action<int> OnActivateItem, OnActivateUpgradeItem, OnAutomateItem, OnActivatePassiveIncome, OnUpgradeTutorialStepReady;
    public event Action<int, GameData, GeneralGameData> OnUpdateData, OnPerformAction, OnUpdateUpgradeData;
    public event Action<GeneralGameData, GameData> OnUpdateGameData;
    public event Action OnTutorialStepCompleted, OnTutorialNonCompleted, OnDataUpdated, OnRewardRecived;
    public event Action<int, int> OnTutorialStepReady, OnManagerAvailability, OnManagerNonAvailability, OnItemNotReadyToBuy, OnItemReadyToBuy;
    private int timeAfterExit, itemIndex;
    public double _totalScore;
    [SerializeField] private double moneyForTreeHint;
    [SerializeField] private double moneyForManagerHint;
    [SerializeField] private double moneyForUpgradeHint;
    [SerializeField] private string planetKey;
    [SerializeField] private int requiredObjects = 6;
    private bool isTipShown;


    #region CURRENCY FIELDS
    [SerializeField] private string currencyCode;
    private string diamondCode = "DI";

    #endregion


    /// <summary>
    /// Handles clicking of the Manager purchas button per each Item (index)
    /// </summary>
    /// <param name="index"></param>
    public void HandleManagerPurchased(int index)
    {
        if (_currentGameData.Managers[index])
            return;
        _currentGameData.Money -= _currentGameData.ItemDataList[index].ManagerPrice;
        _currentGameData.Managers[index] = true;
       
        OnManagerNonAvailability?.Invoke(index, 1);
        isTipShown = false;

        Debug.Log($"Purchased a manager for {index}");
        HandleManager(index);
        _currentGameData.IsManagerPurchased += 1;

        FirebaseAnalytics.LogEvent(name: "auto_purchased");
    }

    public void HandlePremiumManager(int index)
    {
        if (_currentGameData.Managers[index])
        {
            return;
        }
        else
        {
            _currentGameData.Managers[index] = true;
            ActivateManagerFor(index);
        }
    }

    /// <summary>
    /// Activates the automation of clicking the button - to implement managers
    /// </summary>
    /// <param name="index"></param>
    private void ActivateManagerFor(int index)
    {
        AutomateTask(index);
        SendDataUpdate();
    }

    /// <summary>
    /// Performs the work of "clicking the button" automatically if we have purchasesd the manager
    /// </summary>
    /// <param name="index"></param>
    public void HandleManager(int index)
    {
        if (_currentGameData.Managers[index])
        {
            AutomateTask(index);
        }
    }

    /// <summary>
    /// Performs the work of "clicking the button" automatically
    /// </summary>
    /// <param name="index"></param>
    private void AutomateTask(int index)
    {
        //IncreaseScore(index);
        HandleStartItemProgress(index);
    }

    /// <summary>
    /// Logic to unlock the item (purchase it) before we can use it to make money
    /// </summary>
    /// <param name="index"></param>
    public void PurchaseItemFirstTime(int index)
    {
        _currentGameData.Money -= _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        _currentGameData.ItemCount[index] = 1;

        if (PlayerPrefs.GetInt("TutorialStep") == 3)
            OnTutorialStepCompleted.Invoke();
        
        OnItemNotReadyToBuy.Invoke(index, 0);
        isTipShown = false;

        ActivateItem(index);

        FirebaseAnalytics.LogEvent(name: "creation_category_purchased");
    }

    /// <summary>
    /// Activates the Item that was purchased so that we can click it
    /// </summary>
    /// <param name="i"></param>
    private void ActivateItem(int i)
    {
        OnActivateItem?.Invoke(i);
        SendDataUpdate();
    }

    /// <summary>
    /// Adds money to the data and sends the update event
    /// </summary>
    /// <param name="index"></param>
    public void IncreaseScore(int index)
    {
        if (_currentGameData.ItemDataList[index].IsPremium)
        {
            _currentGeneralData.Diamonds += _currentGameData.ItemDataList[index].DiamondsIncome(_currentGameData.ItemCount[index]);
        }
        else
        {
            _currentGameData.Money += _currentGameData.ItemDataList[index].ItemIncome(_currentGameData.ItemCount[index], _currentGameData.ItemBonusMultiplayer[index]);
            _currentGeneralData.TotalScore += _currentGameData.ItemDataList[index].ItemIncome(_currentGameData.ItemCount[index], _currentGameData.ItemBonusMultiplayer[index]);
            _totalScore = _currentGeneralData.TotalScore;
        }

        SendDataUpdate();
    }


    /// <summary>
    /// Runs the work needed to produce money for a specific item
    /// </summary>
    /// <param name="index"></param>
    public void HandleStartItemProgress(int index)
    {
        OnPerformAction?.Invoke(index, _currentGameData, _currentGeneralData);
        OnStartWorkOnItem?.Invoke(index, _currentGameData.ItemDataList[index].Delay);
    }


    /// <summary>
    /// Handle Upgrading the item by spending the money to increas the income and count
    /// </summary>
    /// <param name="index"></param>
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

    #region LOAD GAMEDATA

    /// <summary>
    /// Handles Loading the Game Data and processing it to send updates to other scripts.
    /// </summary>
    /// <param name="gameDataSave"></param>
    public void LoadPlanet(string gameDataSave)
    {
        if (String.IsNullOrEmpty(gameDataSave))
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
        if (String.IsNullOrEmpty(gameDataSave))
            return;
        _currentGeneralData.SetData(gameDataSave);

        OnUpdateGameData?.Invoke(_currentGeneralData, _currentGameData);

        if (_currentGeneralData.ExitTime != null)
        {
            long tempExitTime = Convert.ToInt64(_currentGeneralData.ExitTime);

            var exitTime = DateTime.FromBinary(tempExitTime);
            var currentTime = DateTime.Now;
            var difference = currentTime.Subtract(exitTime);
            var rawTime = (float)difference.TotalSeconds;
            timeAfterExit = (int)rawTime;

            OnActivatePassiveIncome?.Invoke(timeAfterExit);
        }
        SendDataUpdate();
    }
    #endregion

    /// <summary>
    /// Prepares the game data when we start the game.
    /// </summary>
    /// <param name="gameData"></param>
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
            _currentGameData.UpgradeItemCount.Add(i == 0 ? 0 : 0);
        }
        SendDataUpdate();
        OnActivateItem?.Invoke(0);
    }

    public void PrepareGeneralData(GeneralGameData generalGameData)
    {
        _currentGeneralData = generalGameData;
    }

    /// <summary>
    /// Sends update about data changes as an event
    /// </summary>
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
        {
            CheckTutorialStep();
        }

        OnDataUpdated?.Invoke();
    }

    private void CalculateMoneyPerSec()
    {
        _currentGameData.MoneyPerSec = 0;
        for (int i = 0; i < _currentGameData.ItemDataList.Count; i++)
        {
            _currentGameData.MoneyPerSec += _currentGameData.ItemDataList[i].ItemIncomePerSec(_currentGameData.ItemCount[i], _currentGameData.ItemBonusMultiplayer[i]);
        }
    }

    /// <summary>
    /// Logic to unlock managers for purchase
    /// </summary>
    /// <param name="index"></param>
    private void UnlockManagers(int index)
    {
        if (_currentGameData.Managers[index] == false)
        {
            bool val = _currentGameData.ItemDataList[index].ManagerPrice < _currentGameData.Money;
            OnModifyManagerAvailability?.Invoke(index, val);

                if (val && !isTipShown && !_currentGameData.ItemDataList[index].IsPremium)
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
    }

    /// <summary>
    /// Logic to unlock other items for purchase when we have enough money
    /// </summary>
    /// <param name="index"></param>
    private void UnlockOtherItems(int index)
    {   
        if (_currentGameData.ItemCount[index] == 0)
        {
            bool val = _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]) < _currentGameData.Money;
            OnToggleItemActivationState?.Invoke(index, val);

                if (val && !isTipShown)
                {
                    itemIndex = index;                
                    OnItemReadyToBuy.Invoke(index, 0);
                    isTipShown = true;
                }
                else
                {                   
                    OnItemNotReadyToBuy.Invoke(index, 0);
                    isTipShown = false;
                }         
        }
    }

    /// <summary>
    /// Handles Bonus Multiplier so that we have a better balancing 
    /// https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i
    /// </summary>
    /// <param name="index"></param>
    private void CheckBonusMultiplier(int index)
    {
        if (_currentGameData.ItemCount[index] >= _currentGameData.ItemDataList[index].MaxCount(_currentGameData.ItemBonusMultiplayer[index], _currentGameData.ItemMaxCountHelper[index]) && _currentGameData.ItemCount[index] < _currentGameData.ItemDataList[index].MaxCountIncrement)
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

    #region PASSIVE INCOME

    public void GetPassiveIncome(double income, double diamonds)
    {
        _currentGameData.Money += income;
        _currentGeneralData.TotalScore += income;
        _totalScore = _currentGeneralData.TotalScore;
        _currentGeneralData.Diamonds -= diamonds;
        SendDataUpdate();

        FirebaseAnalytics.LogEvent(name: "triple_passive_income_received");
    }

    public void UpdatePassiveIncomeTime(double price, int time)
    {
        _currentGeneralData.Diamonds -= price;
        _currentGeneralData.PassiveIncomeTime += time;
        _currentGeneralData.ExtraTimePurchasedCount++;
        SendDataUpdate();

        FirebaseAnalytics.LogEvent(name: "passive_income_time_updated");
    }
    #endregion

    #region REWARDS

    public void GetReward(double reward)
    {
        _currentGameData.Money += reward;
        _currentGeneralData.TotalScore += reward;
        _totalScore = _currentGeneralData.TotalScore;
        SendDataUpdate();

        FirebaseAnalytics.LogEvent(name: "coins_for_ads");
    }

    public void GetDiamonds(double reward)
    {
        _currentGeneralData.Diamonds += reward;
        SendDataUpdate();

        FirebaseAnalytics.LogEvent(name: "diamonds_for_ads");
    }

    public void GetCoinsBooster()
    {
        _currentGameData.BoosterMultiplier = _currentGameData.IsBoosterPurchased ? 3 : 2;
        SendDataUpdate();

        FirebaseAnalytics.LogEvent(name: "booster_for_ads");
        Debug.Log("Game Rules /// GetCoinsBooster");
    }

    public void DisableCoinsBooster()
    {
        _currentGameData.BoosterMultiplier -= 1;
        SendDataUpdate();
    }
    #endregion

    #region PURCHASES

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
    #endregion

    private void CheckTutorialStep()
    {
        int tutorialStep = PlayerPrefs.GetInt("TutorialStep");

        if (tutorialStep == 1 && _currentGameData.Money >= 5)
            OnTutorialStepCompleted.Invoke();

        if ((tutorialStep == 3 && _currentGameData.Money >= moneyForTreeHint) || (tutorialStep == 4 && _currentGameData.Money >= moneyForManagerHint) || (tutorialStep == 5 && _currentGameData.Money >= moneyForUpgradeHint))
        {
            OnTutorialStepReady.Invoke(tutorialStep, 1);  
        }                           
    }

    #region PLANET UNLOCKER

    public void HandlePlanet(double planetPrice)
    {
        _currentGameData.Money -= planetPrice;
        _currentGeneralData.ActivePlanet++;

        //string eventName = _currentGeneralData.ActivePlanet + "_planet_unlocked";
        //FirebaseAnalytics.LogEvent(name: eventName);

        FirebaseAnalytics.LogEvent(name: _currentGeneralData.ActivePlanet + "_planet_unlocked");
        Debug.Log($"New planet unlocked: {_currentGeneralData.ActivePlanet}");

        SendDataUpdate();
    }
    #endregion
}


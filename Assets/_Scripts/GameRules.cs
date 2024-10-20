using System;
using UnityEngine;
using CBS;
using CBS.Models;

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
    public event Action<int> OnActivateItem, OnActivateUpgradeItem, OnAutomateItem, OnActivatePassiveIncome;
    public event Action<int, GameData, GeneralGameData> OnUpdateData, OnPerformAction;
    public event Action<int, GameData, GeneralGameData> OnUpdateUpgradeData, OnUpdatePerformAction;
    public event Action<GeneralGameData, GameData> OnUpdateGameData;
    private int timeAfterExit;
    public double _totalScore;

    #region CBS FIELDS
    [SerializeField] private string currencyCode;
    private ICurrency CurrencyModule { get; set; }
    private string diamondCode = "DI";

    #endregion

    #region CBS CURRENCIES
    private void Start()
    {
        CurrencyModule = CBSModule.Get<CBSCurrencyModule>();
    }

    private void OnAddCurrency(CBSUpdateCurrencyResult result)
    {
        if (result.IsSuccess)
        {
            var balanceChange = result.BalanceChange;
            var updatedCurrency = result.UpdatedCurrency;
        }
        else
        {
            Debug.Log(result.Error.Message);
        }
    }

    private void OnSubtract(CBSUpdateCurrencyResult result)
    {
        if (result.IsSuccess)
        {
            var balanceChange = result.BalanceChange;
            var updatedCurrency = result.UpdatedCurrency;
        }
        else
        {
            Debug.Log(result.Error.Message);
        }
    }

    private void UpdateCurrency(string code, double coins, bool isIncrease)
    {
        if (isIncrease == true)
            CurrencyModule.AddCurrencyToProfile(code, (int)coins, OnAddCurrency);
        else
            CurrencyModule.SubtractCurrencyFromProfile(code, (int)coins, OnSubtract);
    }
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

        // double itemPrice = _currentGameData.ItemDataList[index].ManagerPrice;
        // UpdateCurrency(currencyCode, itemPrice, false);

        Debug.Log($"Purchased a manager for {index}");
        HandleManager(index);
        _currentGameData.IsManagerPurchased += 1;
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

    public void HandleUpgradeManager(int index)
    {
        IncreaseDiamondsScore(index);
        HandleStartUpgradeItemProgress(index);
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

        // double itemPrice = _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        // UpdateCurrency(currencyCode, itemPrice, false);

        ActivateItem(index);
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

    private void ActivateUpgradeItem(int i)
    {
        OnActivateUpgradeItem?.Invoke(i);
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

            // double income = _currentGameData.ItemDataList[index].DiamondsIncome(_currentGameData.ItemCount[index]);
            // UpdateCurrency(diamondCode, income, true);
        }
        else
        {
            _currentGeneralData.TotalScore += _currentGameData.ItemDataList[index].ItemIncome(_currentGameData.ItemCount[index], _currentGameData.ItemBonusMultiplayer[index]);

            if (_currentGameData.Managers[index])
            {
                _currentGameData.MoneyPerSec = _currentGameData.ItemDataList[index].ItemIncomePerSec(_currentGameData.ItemCount[index], _currentGameData.ItemBonusMultiplayer[index]);
                _currentGameData.Money += _currentGameData.MoneyPerSec;

                //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! GameRules /// IncreaseScore /// Money: " + _currentGameData.Money + " /// MoneyPerSec: " + _currentGameData.MoneyPerSec);
            }
            else
            {
                _currentGameData.Money += _currentGameData.ItemDataList[index].ItemIncome(_currentGameData.ItemCount[index], _currentGameData.ItemBonusMultiplayer[index]);

                //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! GameRules /// IncreaseScore /// Money: " + _currentGameData.Money);
            }

            // double income = _currentGameData.ItemDataList[index].ItemIncome(_currentGameData.ItemCount[index], _currentGameData.ItemBonusMultiplayer[index]);
            // UpdateCurrency(currencyCode, income, true);

            _totalScore = _currentGeneralData.TotalScore;
        }

        SendDataUpdate();
    }

    public void IncreaseDiamondsScore(int index)
    {
        _currentGeneralData.Diamonds += _currentGameData.UpgradeItemDataList[index].ItemIncome(_currentGameData.UpgradeItemCount[index]);

        // double income = _currentGameData.UpgradeItemDataList[index].ItemIncome(_currentGameData.UpgradeItemCount[index]);
        // UpdateCurrency(diamondCode, income, true);
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

        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! GameRules /// HandleStartItemProgress /// Delay: " + _currentGameData.ItemDataList[index].Delay);
    }

    public void HandleStartUpgradeItemProgress(int index)
    {
        OnUpdatePerformAction?.Invoke(index, _currentGameData, _currentGeneralData);
        OnStartWorkOnUpgradeItem?.Invoke(index, _currentGameData.UpgradeItemDataList[index].Delay);
    }

    /// <summary>
    /// Handle Upgrading the item by spending the money to increas the income and count
    /// </summary>
    /// <param name="index"></param>
    public void HandleUpgrade(int index)
    {
        _currentGameData.Money -= _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        _currentGameData.ItemCount[index] += 1;

        // double itemPrice = _currentGameData.ItemDataList[index].ItemUpgradePrice(_currentGameData.ItemCount[index]);
        // UpdateCurrency(currencyCode, itemPrice, false);

        SendDataUpdate();
    }
    public void HandleDiamondsUpgrade(int index)
    {
        _currentGeneralData.Diamonds -= _currentGameData.UpgradeItemDataList[index].ItemCost;
        _currentGameData.UpgradeItemCount[index] += 1;

        // double itemPrice = _currentGameData.UpgradeItemDataList[index].ItemCost;
        // UpdateCurrency(diamondCode, itemPrice, false);

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
            {
                ActivateItem(i);
            }

            _currentGameData.ItemDataList[i].Auto = _currentGameData.Managers[i];
            
            //HandleManager(i);
        }

        for (int i = 0; i < _currentGameData.UpgradeItemDataList.Count; i++)
        {
            if (_currentGameData.UpgradeItemCount[i] > 0)
            {
                ActivateUpgradeItem(i);
            }
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

            Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! GameRules /// LoadGame /// Time After Exit: " + timeAfterExit);
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
            OnUpdateData?.Invoke(i, _currentGameData, _currentGeneralData);
        }

        for (int i = 0; i < _currentGameData.UpgradeItemDataList.Count; i++)
        {
            OnUpdateUpgradeData?.Invoke(i, _currentGameData, _currentGeneralData);
        }

        OnUpdateGameData?.Invoke(_currentGeneralData, _currentGameData);
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

    public void GetMoney()
    {
        _currentGameData.Money += 1000000;
        _currentGeneralData.Diamonds += 200;
        _currentGeneralData.TotalScore += 1000000;

        // UpdateCurrency(currencyCode, 1000000, true);
        // UpdateCurrency(diamondCode, 200, true);

        SendDataUpdate();
    }

    #region PASSIVE INCOME

    public void GetPassiveIncome(double income, double diamonds)
    {
        _currentGameData.Money += income;
        _currentGeneralData.TotalScore += income;
        _totalScore = _currentGeneralData.TotalScore;
        _currentGeneralData.Diamonds -= diamonds;

        // UpdateCurrency(currencyCode, income, true);
        // UpdateCurrency(diamondCode, diamonds, false);

        SendDataUpdate();
    }

    public void UpdatePassiveIncomeTime(double price, int time)
    {
        _currentGeneralData.Diamonds -= price;
        _currentGeneralData.PassiveIncomeTime += time;
        _currentGeneralData.ExtraTimePurchasedCount++;

        // UpdateCurrency(diamondCode, price, false);

        SendDataUpdate();
    }
    #endregion

    #region REWARDS

    public void GetReward(double reward)
    {
        _currentGameData.Money += reward;
        _currentGeneralData.TotalScore += reward;
        _totalScore = _currentGeneralData.TotalScore;

        // UpdateCurrency(currencyCode, reward, true);

        SendDataUpdate();
    }
    #endregion

    #region PURCHASES

    public void GetPurchasedProduct(double coins, double diamonds)
    {
        _currentGameData.Money += coins;
        _currentGeneralData.Diamonds += diamonds;

        // UpdateCurrency(currencyCode, coins, true);
        // UpdateCurrency(diamondCode, diamonds, true);

        SendDataUpdate();
    }

    public void GetPurchasedProduct(double diamonds)
    {
        _currentGeneralData.Diamonds += diamonds;

        // UpdateCurrency(diamondCode, diamonds, true);

        SendDataUpdate();
    }
    #endregion
}


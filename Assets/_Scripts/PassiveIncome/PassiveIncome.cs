using Firebase.Analytics;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;

public class PassiveIncome : MonoBehaviour
{
    #region EDITOR FIELDS

    [Header("Purchasing Extra Time")]
    [SerializeField] private PassiveIncomeData _passiveIncomeData;
    public PassiveIncomeData ExtraTimeConfig => _passiveIncomeData;

    [Space(10)]
    [SerializeField] private GameObject _extraTimePanel;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _extraTimeTxt;
    [SerializeField] private TextMeshProUGUI _extraTimePriceTxt;
    [SerializeField] private TextMeshProUGUI _timeTxt;

    [Header("Passive Income")]
    [SerializeField] private int _delayTime;

    [Space(10)]
    public GameObject passiveIncomeWind;
    [SerializeField] private TextMeshProUGUI _timeAwayTxt;
    [SerializeField] private TextMeshProUGUI _passiveIncomeTxt;
    [SerializeField] private TextMeshProUGUI _maxTimeAwayTxt;

    [Header("Passive Income X2")]
    [SerializeField] private TextMeshProUGUI _2xPassiveIncomeTxt;
    [SerializeField] private Button _2xButtonReward;


    [Header("Passive Income X3")]
    [SerializeField] private double _3xPassiveIncomePrice;
    [SerializeField] private TextMeshProUGUI _3xPassiveIncomeTxt;
    [SerializeField] private Button _3xButtonReward;

    [Space(10)]
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    [Header("Passive Income Chest")]
    [SerializeField] private PassiveIncomeChestController _chestController;

    #endregion

    #region EVENTS & ACTIONS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent;

    public event Action<double, double> OnEarningPassiveIncome;
    public event Action<double, int> OnGetPassiveIncomeExtraTime;
    public event Action<double, double, double, double> OnPassiveIncomeQuotePrepared;

    #endregion

    #region PRIVATE FIELDS
    GameData _currentGameData;
    GeneralGameData _currentGeneralData;
    private double _passiveProfit;
    private double _passiveDiamondsProfit;
    private bool _rewardReceived;
    private bool _isPassiveIncome;
    private double _2xPassiveIncome;
    private double _3xPassiveIncome;
    private string _openedPlanetKey;
    private string _openedRewardId;

    public event Func<string, string, int, double, bool> OnClaimChestReward;

    #endregion

    private void OnEnable()
    {
        _rewardReceived = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
        _adController.OnAdClosedEvent.AddListener(RewardedAdClosed);

        if (_chestController != null)
            _chestController.OnChestClicked += HandleChestClicked;
    }

    private void Start()
    {
        RefreshExtraTimeUI();
    }

    void UpdateTimeText()
    {
        int hours = _currentGeneralData.PassiveIncomeTime / 3600;
        int minutes = (_currentGeneralData.PassiveIncomeTime % 3600) / 60;
        int seconds = _currentGeneralData.PassiveIncomeTime % 60;

        _timeTxt.text = $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }


    private void RefreshExtraTimeUI()
    {
        if (_currentGeneralData == null || _passiveIncomeData == null)
            return;

        UpdateTimeText();

        int count = _currentGeneralData.ExtraTimePurchasedCount;

        bool maxPurchased = count >= _passiveIncomeData.ExtraTimeCount;

        _buyButton.gameObject.SetActive(!maxPurchased);
        _timeTxt.gameObject.SetActive(!maxPurchased);

        if (maxPurchased)
        {
            _extraTimeTxt.text = _timeTxt.text;
            return;
        }

        _extraTimeTxt.text = "+" + (_passiveIncomeData.ExtraTime / 60) + " " + Lean.Localization.LeanLocalization.GetTranslationText("minutes");

        double price = GetExtraTimePrice();

        bool validPrice = price > 0d && !double.IsNaN(price) && !double.IsInfinity(price);

        _extraTimePriceTxt.text = validPrice ? price.ToString("0.##") : "-";

        _buyButton.interactable = count >= 0 && validPrice && _currentGeneralData.Diamonds >= price;
    }

    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        _currentGeneralData = generalGameData;
        _currentGameData = gameData;

        RefreshExtraTimeUI();
    }


    public void GetExtraTimeForPassiveIncome()
    {
        if (_currentGeneralData == null || _passiveIncomeData == null)
            return;

        int count = _currentGeneralData.ExtraTimePurchasedCount;

        if (count < 0 || count >= _passiveIncomeData.ExtraTimeCount)
            return;

        double price = GetExtraTimePrice();

        if (price <= 0d || double.IsNaN(price) || double.IsInfinity(price) || _currentGeneralData.Diamonds < price)
        {
            RefreshExtraTimeUI();
            return;
        }

        OnGetPassiveIncomeExtraTime?.Invoke(price, _passiveIncomeData.ExtraTime);

        RefreshExtraTimeUI();
    }


    public void ActivatePassiveIncome(int timeAfterExit)
    {
        if (_currentGeneralData == null || _currentGeneralData.OfflinePlanetRewardsEnabled)
            return;

        int pendingSeconds = _currentGeneralData.PendingOfflineSeconds;

        if (pendingSeconds > _delayTime && _currentGameData.MoneyPerSec > 0 && _currentGameData.IsManagerPurchased > 0 && !passiveIncomeWind.activeSelf)
        {
            _passiveProfit = Math.Min(pendingSeconds, _currentGeneralData.PassiveIncomeTime) * _currentGameData.MoneyPerSec;

            Debug.Log(
                $"[PassiveIncome] ACTIVATE | " +
                $"Pending={pendingSeconds}s | " +
                $"Limit={_currentGeneralData.PassiveIncomeTime}s | " +
                $"MoneyPerSec={_currentGameData.MoneyPerSec} | " +
                $"Reward={_passiveProfit}"
            );

            PreparePassiveIncomeUI(pendingSeconds, _passiveProfit);
        }
    }

    private bool TryClaimChestReward(int multiplier, double diamondCost)
    {
        if (string.IsNullOrEmpty(_openedPlanetKey) ||
            string.IsNullOrEmpty(_openedRewardId) ||
            OnClaimChestReward == null)
            return false;

        bool success = OnClaimChestReward.Invoke(
            _openedPlanetKey,
            _openedRewardId,
            multiplier,
            diamondCost);

        if (!success)
        {
            Debug.LogWarning(
                $"[PassiveIncome] CLAIM X{multiplier} REJECTED");
            return false;
        }

        _openedPlanetKey = null;
        _openedRewardId = null;

        _rewardReceived = true;
        _isPassiveIncome = false;

        _2xButtonReward.interactable = true;
        _3xButtonReward.interactable = true;

        passiveIncomeWind.SetActive(false);
        _chestController?.Refresh();

        return true;
    }

    public void EarningPassiveIncome()
    {
        if (_currentGeneralData != null &&
            _currentGeneralData.OfflinePlanetRewardsEnabled)
        {
            TryClaimChestReward(1, 0d);
            return;
        }

        if (_currentGeneralData == null || _currentGameData == null || _currentGeneralData.PendingOfflineSeconds <= 0)
            return;

        _passiveProfit = Math.Min(_currentGeneralData.PendingOfflineSeconds, _currentGeneralData.PassiveIncomeTime) * _currentGameData.MoneyPerSec;

        Debug.Log($"[PassiveIncome] CLAIM X1 | " + $"MoneyBefore={_currentGameData.Money} | " + $"PendingBefore={_currentGeneralData.PendingOfflineSeconds}s | " + $"Reward={_passiveProfit}");

        OnEarningPassiveIncome?.Invoke(_passiveProfit, 0);

        if (_currentGeneralData.PendingOfflineSeconds > 0)
        {
            Debug.LogWarning("[PassiveIncome] CLAIM REJECTED");
            return;
        }

        _rewardReceived = true;

        Debug.Log($"[PassiveIncome] CLAIM SUCCESS | " + $"MoneyAfter={_currentGameData.Money} | " + $"PendingAfter={_currentGeneralData.PendingOfflineSeconds}s");

        passiveIncomeWind.SetActive(false);
        FirebaseAnalytics.LogEvent(name: "passive_income_received");
        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! PassiveIncome /// EarningPassiveIncome /// PassiveIncome: " + _passiveProfit);
    }

    public void PreparePassiveIncomeUI(
        int timeAfterExit,
        double passiveIncome,
        double? savedX2 = null,
        double? savedX3 = null)
    {
        _2xPassiveIncome = savedX2 ?? Math.Truncate(passiveIncome) * 2d;

        _3xPassiveIncome = savedX3 ?? Math.Truncate(passiveIncome) * 3d;

        OnPassiveIncomeQuotePrepared?.Invoke(
            passiveIncome,
            _2xPassiveIncome,
            _3xPassiveIncome,
            _3xPassiveIncomePrice
        );

        passiveIncomeWind.SetActive(true);

        _3xButtonReward.interactable = _currentGeneralData.Diamonds >= _3xPassiveIncomePrice;

        TimeSpan timeAway = TimeSpan.FromSeconds(timeAfterExit);
        _timeAwayTxt.text = $"{timeAway.Hours} h {timeAway.Minutes} m {timeAway.Seconds} s";

        TimeSpan passiveIncomeTime = TimeSpan.FromSeconds(_currentGeneralData.PassiveIncomeTime);
        _maxTimeAwayTxt.text = $"{passiveIncomeTime.Hours} h {passiveIncomeTime.Minutes} m {passiveIncomeTime.Seconds} s";

        _passiveIncomeTxt.text = $"{passiveIncome:N0}";

        if (_2xPassiveIncome > 1000)
        {
            _2xPassiveIncomeTxt.text = $"{AbbreviateNumber(_2xPassiveIncome)}";
            _3xPassiveIncomeTxt.text = $"{AbbreviateNumber(_3xPassiveIncome)}";
        }
        else
        {
            _2xPassiveIncomeTxt.text = _2xPassiveIncome.ToString();
            _3xPassiveIncomeTxt.text = _3xPassiveIncome.ToString();
        }
    }

    string AbbreviateNumber(double number)
    {
        string[] suffixes = { "", "K", "M", "B", "T" };
        int suffixIndex = 0;
        while (number >= 1000 && suffixIndex < suffixes.Length - 1)
        {
            number /= 1000;
            suffixIndex++;
        }
        return string.Format("{0:0.##} {1}", number, suffixes[suffixIndex]).Replace(',', '.');
    }

    public void UserEarnedReward()
    {
        if (_isPassiveIncome && _currentGeneralData != null && _currentGeneralData.OfflinePlanetRewardsEnabled)
        {
            bool success = TryClaimChestReward(2, 0d);

            if (success)
            {
                FirebaseAnalytics.LogEvent("double_passive_income_received");
            }
            else
            {
                _isPassiveIncome = false;
                _2xButtonReward.interactable = true;
                _3xButtonReward.interactable =
                    _currentGeneralData.Diamonds >= _3xPassiveIncomePrice;
            }

            return;
        }
        if (_isPassiveIncome)
        {
            OnEarningPassiveIncome?.Invoke(_2xPassiveIncome, 0);

            FirebaseAnalytics.LogEvent(name: "double_passive_income_received");

            _2xButtonReward.interactable = true;
            _3xButtonReward.interactable = true;
            passiveIncomeWind.SetActive(false);
            _isPassiveIncome = false;
        }
    }

    public void GetDoublePassiveIncome()
    {
        _2xButtonReward.interactable = false;
        _3xButtonReward.interactable = false;

        _rewardReceived = true;
        _isPassiveIncome = true;

        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _adController.LoadAd();
    }

    public void GetTriplePassiveIncome()
    {
        if (_currentGeneralData != null &&
    _currentGeneralData.OfflinePlanetRewardsEnabled)
        {
            TryClaimChestReward(3, _3xPassiveIncomePrice);
            return;
        }
        _2xButtonReward.interactable = false;
        _3xButtonReward.interactable = false;

        OnEarningPassiveIncome?.Invoke(_3xPassiveIncome, _3xPassiveIncomePrice);

        _rewardReceived = true;
        passiveIncomeWind.SetActive(false);
    }

    public void ShowRewardedAd()
    {
        if (_isPassiveIncome)
            _adController.ShowAd();
    }

    public void RewardedAdWithError()
    {
        if (_currentGeneralData != null && _currentGeneralData.OfflinePlanetRewardsEnabled && _isPassiveIncome)
        {
            _isPassiveIncome = false;
            _2xButtonReward.interactable = true;
            _3xButtonReward.interactable =
                _currentGeneralData.Diamonds >= _3xPassiveIncomePrice;
        }
        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("RewardedAdError")}";
    }


    private void RewardedAdClosed()
    {
        if (!_isPassiveIncome || _currentGeneralData == null || !_currentGeneralData.OfflinePlanetRewardsEnabled)
            return;

        _2xButtonReward.interactable = true;
        _3xButtonReward.interactable = _currentGeneralData.Diamonds >= _3xPassiveIncomePrice;
    }



    private void HandleChestClicked(string planetKey, string rewardId)
    {
        if (_currentGeneralData == null || !_currentGeneralData.OfflinePlanetRewardsEnabled || _currentGeneralData.OfflinePlanetRewards == null)
        {
            Debug.LogWarning("[PassiveIncome] Offline rewards not ready.");
            return;
        }

        OfflinePlanetRewardData reward = _currentGeneralData.OfflinePlanetRewards.Find(r => r != null && r.PlanetKey == planetKey && r.RewardId == rewardId);

        if (reward == null)
        {
            Debug.LogWarning("[PassiveIncome] Chest reward not found.");
            return;
        }

        if (reward.CreditedSeconds < 60)
            return;

        if (passiveIncomeWind.activeSelf)
            return;

        Debug.Log($"[PassiveIncome] Opening chest reward: {reward.MoneyX1:N0}");

        _openedPlanetKey = planetKey;
        _openedRewardId = rewardId;

        PreparePassiveIncomeUI(reward.CreditedSeconds, reward.MoneyX1, reward.MoneyX2, reward.MoneyX3);
    }

    private double GetExtraTimePrice()
    {
        return _passiveIncomeData.ExtraTimePrice *
               Math.Pow(
                   _passiveIncomeData.ExtraTimePriceMultiplier,
                   _currentGeneralData.ExtraTimePurchasedCount);
    }


    private void OnDisable()
    {
        _adController.OnUserEarnedRewardEvent.RemoveListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.RemoveListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.RemoveListener(RewardedAdWithError);
        _adController.OnAdClosedEvent.RemoveListener(RewardedAdClosed);

        if (_chestController != null)
            _chestController.OnChestClicked -= HandleChestClicked;
    }
}

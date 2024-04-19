using Firebase.Analytics;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PassiveIncomeMultiplierReward : MonoBehaviour
{
    #region EDITOR FIELDS

    [SerializeField] private ScorePanel _money;
    [SerializeField] private ScorePanel _diamonds;

    [Space(10)]
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

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent;

    #endregion

    #region PRIVATE FIELDS

    GameData _currentGameData;
    private double _coinsReward;
    private bool _rewardReceived;

    public bool RewardReceived { get => _rewardReceived; }
    public int DelayTime { get => _delayTime; }

    private int _2xPassiveIncome;
    private int _3xPassiveIncome;

    #endregion

    private void OnEnable()
    {
        _rewardReceived = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    public void PreparePassiveIncome(GameData gameData, int timeAfterExit, double passiveIncome)
    {
        _currentGameData = gameData;

        _2xPassiveIncome = (int)passiveIncome * 2;
        _3xPassiveIncome = (int)passiveIncome * 3;

        passiveIncomeWind.SetActive(true);

        TimeSpan timeAway = TimeSpan.FromSeconds(timeAfterExit);
        _timeAwayTxt.text = $"{timeAway.Hours} h {timeAway.Minutes} m {timeAway.Seconds} s";

        TimeSpan passiveIncomeTime = TimeSpan.FromSeconds(gameData.PassiveIncomeTime);
        _maxTimeAwayTxt.text = $"{passiveIncomeTime.Hours} h {passiveIncomeTime.Minutes} m {passiveIncomeTime.Seconds} s";

        _passiveIncomeTxt.text = $"{passiveIncome:N0}";

        _2xPassiveIncomeTxt.text = _2xPassiveIncome.ToString();
        _3xPassiveIncomeTxt.text = _3xPassiveIncome.ToString();
    }

    public void UserEarnedReward()
    {
        _currentGameData.Money += _2xPassiveIncome;
        _money.SetScore(_currentGameData.Money);

        FirebaseAnalytics.LogEvent(name: "coins_for_ads");

        _2xButtonReward.interactable = true;
        _3xButtonReward.interactable = true;
        passiveIncomeWind.SetActive(false);
    }

    public void GetDoublePassiveIncome()
    {
        _2xButtonReward.interactable = false;
        _3xButtonReward.interactable = false;

        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _adController.LoadAd();

        _rewardReceived = true;
    }

    public void GetTriplePassiveIncome()
    {
        _2xButtonReward.interactable = false;
        _3xButtonReward.interactable = false;

        _currentGameData.Money += _3xPassiveIncome;
        _currentGameData.Diamonds -= _3xPassiveIncomePrice;
        _money.SetScore(_currentGameData.Money);
        _diamonds.SetDiamondsScore(_currentGameData.Diamonds);

        passiveIncomeWind.SetActive(false);

        _rewardReceived = true;
    }

    public void ShowRewardedAd()
    {
        _adController.ShowAd();
    }

    public void RewardedAdWithError()
    {
        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("RewardedAdError")}";
    }

    private void OnDisable()
    {
        _adController.OnUserEarnedRewardEvent.RemoveListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.RemoveListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.RemoveListener(RewardedAdWithError);
    }
}

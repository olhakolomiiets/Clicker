using Firebase.Analytics;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class PassiveIncome : MonoBehaviour
{
    //public static PassiveIncome instance;
    #region EDITOR FIELDS

    [Header("Purchasing Extra Time")]
    [SerializeField] private PassiveIncomeData _passiveIncomeData;

    [Space(10)]
    [SerializeField] private GameObject _extraTimePanel;
    [SerializeField] private Button _buyButton;
    [SerializeField] private TextMeshProUGUI _extraTimeTxt;
    [SerializeField] private TextMeshProUGUI _extraTimePriceTxt;

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
    [SerializeField] private ScorePanel _money;
    [SerializeField] private ScorePanel _diamonds;
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent;

    public event Action<double> OnEarningPassiveIncome;

    #endregion

    #region PRIVATE FIELDS

    GameData _currentGameData;
    GeneralGameData _currentGeneralData;
    private double _passiveProfit;
    private bool _rewardReceived;
    private int _2xPassiveIncome;
    private int _3xPassiveIncome;

    #endregion

    // private void Awake()
    // {
    //     if (instance == null)
    //     {
    //         instance = this;
    //         DontDestroyOnLoad(gameObject);
    //     }
    //     else if (instance != this)
    //     {
    //         Destroy(gameObject);
    //     }
    // }

    private void OnEnable()
    {
        _rewardReceived = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    private void Start()
    {
        _extraTimeTxt.text = $"{"+" + (_passiveIncomeData.ExtraTime / 60).ToString() + " " + Lean.Localization.LeanLocalization.GetTranslationText("minutes")}";

        if (_currentGeneralData.ExtraTimePurchasedCount >= _passiveIncomeData.ExtraTimeCount)
            _extraTimePanel.SetActive(false);
        else
            _extraTimePriceTxt.text = _passiveIncomeData.ExtraTimePrice.ToString();
    }

    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        _currentGeneralData = generalGameData;
        _currentGameData = gameData;

        _buyButton.interactable = (generalGameData.Diamonds <= _passiveIncomeData.ExtraTimePrice) ? false : true;
    }

    public void GetExtraTimeForPassiveIncome()
    {       
        _currentGeneralData.Diamonds -= _passiveIncomeData.ExtraTimePrice;
        _diamonds.SetDiamondsScore(_currentGeneralData.Diamonds);
        _currentGeneralData.PassiveIncomeTime += _passiveIncomeData.ExtraTime;

        _currentGeneralData.ExtraTimePurchasedCount++;

        if (_currentGeneralData.ExtraTimePurchasedCount >= _passiveIncomeData.ExtraTimeCount)
            _extraTimePanel.SetActive(false);

        _passiveIncomeData.ExtraTimePrice = _passiveIncomeData.ExtraTimePrice * _passiveIncomeData.ExtraTimePriceMultiplier;
        _extraTimePriceTxt.text = _passiveIncomeData.ExtraTimePrice.ToString();
    }

    public void ActivatePassiveIncome(int timeAfterExit)
    {
        if (!_rewardReceived && timeAfterExit > _delayTime && _currentGameData.MoneyPerSec > 0)
        {
            _passiveProfit = Math.Min(timeAfterExit, _currentGeneralData.PassiveIncomeTime) * _currentGameData.MoneyPerSec;
            PreparePassiveIncomeUI(timeAfterExit, _passiveProfit);
        }
    }

    public void EarningPassiveIncome()
    {
        OnEarningPassiveIncome?.Invoke(_passiveProfit);
        passiveIncomeWind.SetActive(false);
    }

    public void PreparePassiveIncomeUI(int timeAfterExit, double passiveIncome)
    {
        _2xPassiveIncome = (int)passiveIncome * 2;
        _3xPassiveIncome = (int)passiveIncome * 3;

        passiveIncomeWind.SetActive(true);

        _3xButtonReward.interactable = (_currentGeneralData.Diamonds <= _3xPassiveIncomePrice) ? false : true;

        TimeSpan timeAway = TimeSpan.FromSeconds(timeAfterExit);
        _timeAwayTxt.text = $"{timeAway.Hours} h {timeAway.Minutes} m {timeAway.Seconds} s";

        TimeSpan passiveIncomeTime = TimeSpan.FromSeconds(_currentGeneralData.PassiveIncomeTime);
        _maxTimeAwayTxt.text = $"{passiveIncomeTime.Hours} h {passiveIncomeTime.Minutes} m {passiveIncomeTime.Seconds} s";

        _passiveIncomeTxt.text = $"{passiveIncome:N0}";

        _2xPassiveIncomeTxt.text = _2xPassiveIncome.ToString();
        _3xPassiveIncomeTxt.text = _3xPassiveIncome.ToString();
    }

    public void UserEarnedReward()
    {
        _currentGameData.Money += _2xPassiveIncome;
        _money.SetScore(_currentGameData.Money);

        FirebaseAnalytics.LogEvent(name: "Get_2xPassiveIncome");

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
        _currentGeneralData.Diamonds -= _3xPassiveIncomePrice;
        _money.SetScore(_currentGameData.Money);
        _diamonds.SetDiamondsScore(_currentGeneralData.Diamonds);

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

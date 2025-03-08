using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Firebase.Analytics;
using TMPro;
using System;
using System.Collections;

public class RewardsManager : MonoBehaviour
{
    #region EDITOR FIELDS

    [Header("Coins Reward")]
    [SerializeField] private TextMeshProUGUI _coinsTitle;
    [SerializeField] private Button _coinsButton;
    [SerializeField] private GameObject _coinsObj;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private float _coinsMultiplicator;

    [Header("Booster Reward")]
    [SerializeField] private TextMeshProUGUI _boosterTitle;
    [SerializeField] private Button _boosterButton;
    [SerializeField] private GameObject _boosterObj;

    [Space(10)]
    [SerializeField] private int _boosterTime;

    [Header("Diamonds Reward")]
    [SerializeField] private TextMeshProUGUI _diamondsTitle;
    [SerializeField] private Button _diamondsButton;
    [SerializeField] private GameObject _diamondsObj;
    [SerializeField] private TextMeshProUGUI _diamondsRewardTxt;

    [Space(10)]
    [SerializeField] private float _diamonds;

    [Space(10)]
    [SerializeField] private TextMeshProUGUI _serviceTxt;
    [SerializeField] MoveBetweenTransforms _meteor;
    [SerializeField] RewardTimers _rewardTimer;
    //[SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;
    [SerializeField] private AppodealAdController _appodealController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnCoinsRewardReceived, OnBoosterRewardEarned, OnDiamondsRewardReceived;

    public event Action<double> OnEarningReward, OnEarningDiamonds;
    public event Action<int, bool> OnEarningBoosterReward;

    #endregion

    #region PRIVATE FIELDS

    private float _coinsReward;
    private bool _isCoinsRewardActive;
    private bool _isBoosterRewardActive;
    private bool _isDiamondsRewardActive;

    #endregion

    private void OnEnable()
    {
        if (_appodealController == null)
        {
            _appodealController = FindAnyObjectByType<AppodealAdController>();
        }
        _appodealController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        //_adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _appodealController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    public void EnableCoinsReward()
    {
        _boosterButton.gameObject.SetActive(false);
        _boosterObj.SetActive(false);

        _diamondsButton.gameObject.SetActive(false);
        _diamondsObj.SetActive(false);

        _coinsButton.gameObject.SetActive(true);
        _coinsObj.SetActive(true);
        _serviceTxt.text = $"";
        _coinsTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("GetCoins")}";
    }

    public void EnableDiamondsReward()
    {
        _boosterButton.gameObject.SetActive(false);        
        _boosterObj.SetActive(false);

        _coinsButton.gameObject.SetActive(false);
        _coinsObj.SetActive(false);

        _diamondsButton.gameObject.SetActive(true);
        _diamondsObj.SetActive(true);
        _serviceTxt.text = $"";
        _diamondsTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("GetDiamonds")}";
    }

    public void EnableBoosterReward()
    {
        _coinsButton.gameObject.SetActive(false);     
        _coinsObj.SetActive(false);

        _diamondsButton.gameObject.SetActive(false);
        _diamondsObj.SetActive(false);

        _boosterButton.gameObject.SetActive(true);
        _serviceTxt.text = $"";
        _boosterTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Booster")}";
        _boosterObj.SetActive(true);
    }

    public void PrepareRewardData(float money)
    {
        EnableCoinsReward();

        if (!_rewardTimer.isBoosterTimerActive)
            _coinsReward = money * _coinsMultiplicator;
        else _coinsReward = (money/2) * _coinsMultiplicator;

        _coinsRewardTxt.text = $"{_coinsReward.ToString("N0")}";

    }

    public void UserEarnedReward()
    {
        if (_isCoinsRewardActive)
        {
            OnEarningReward?.Invoke(_coinsReward);
            OnCoinsRewardReceived?.Invoke();
            FirebaseAnalytics.LogEvent(name: "coins_for_ads");

            _meteor.SetObjectPosition();

            _coinsButton.interactable = true;
            _coinsButton.gameObject.SetActive(false);
            
            _isCoinsRewardActive = false;

            _meteor.gameObject.SetActive(false);
        }
        else if (_isBoosterRewardActive)
        {
            OnEarningBoosterReward?.Invoke(_boosterTime, true);
            OnBoosterRewardEarned?.Invoke();
            FirebaseAnalytics.LogEvent(name: "booster_for_ads");

            _meteor.SetObjectPosition();

            _boosterButton.interactable = true;
            _boosterButton.gameObject.SetActive(false);
            
            _isBoosterRewardActive = false;

            _meteor.gameObject.SetActive(false);
        }
        else if (_isDiamondsRewardActive)
        {
            OnEarningDiamonds?.Invoke(_diamonds);
            OnDiamondsRewardReceived?.Invoke();
            FirebaseAnalytics.LogEvent(name: "diamonds_for_ads");

            _meteor.SetObjectPosition();

            _diamondsButton.interactable = true;
            _diamondsButton.gameObject.SetActive(false);

            _isDiamondsRewardActive = false;

            _meteor.gameObject.SetActive(false);
        }
    }

    public void GetCoinsBooster()
    {
        _boosterButton.interactable = false;
        _isBoosterRewardActive = true;
        _boosterObj.SetActive(false);
        _serviceTxt.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _appodealController.ShowRewardedVideo(); 
        //_adController.LoadAd();
    }

    public void GetCoins()
    {
        _coinsButton.interactable = false;
        _isCoinsRewardActive = true;
        _coinsObj.SetActive(false);
        _serviceTxt.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _appodealController.ShowRewardedVideo(); 
        //_adController.LoadAd();
    }

    public void GetDiamondssBooster()
    {
        _diamondsButton.interactable = false;
        _isDiamondsRewardActive = true;
        _diamondsObj.SetActive(false);
        _serviceTxt.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _appodealController.ShowRewardedVideo();
    }

    public void ShowRewardedAd()
    {
        //_adController.ShowAd();
    }

    public void RewardedAdWithError()
    {
        _serviceTxt.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("RewardedAdError")}";
    }

    private void OnDisable()
    {
        _appodealController.OnUserEarnedRewardEvent.RemoveListener(UserEarnedReward);
        //_adController.RewardedAdLoadedEvent.RemoveListener(ShowRewardedAd);
        _appodealController.RewardedAdLoadedWithErrorEvent.RemoveListener(RewardedAdWithError);
    }
}

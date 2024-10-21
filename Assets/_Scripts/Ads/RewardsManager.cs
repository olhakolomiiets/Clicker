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
    [SerializeField] private GameObject _reward;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private float _coinsMultiplicator;

    [Header("Booster Reward")]
    [SerializeField] private int _boosterTime;
    [SerializeField] private TextMeshProUGUI _boosterTitle;
    [SerializeField] private Button _boosterButton;
    [SerializeField] private GameObject _description;

    [Space(10)]
    [SerializeField] MoveBetweenTransforms _meteor;
    [SerializeField] RewardTimers _rewardTimer;
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnCoinsRewardReceived, OnBoosterRewardEarned;

    public event Action<double> OnEarningReward;
    public event Action<int, bool> OnEarningBoosterReward;

    #endregion

    #region PRIVATE FIELDS

    private float _coinsReward;
    private bool _isCoinsRewardActive;
    private bool _isBoosterRewardActive;

    #endregion

    private void OnEnable()
    {
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    public void EnableCoinsReward()
    {
        _boosterButton.gameObject.SetActive(false);
        _description.SetActive(false);

        _coinsButton.gameObject.SetActive(true);
        _reward.SetActive(true);
        _coinsTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("GetCoins")}";
    }

    public void EnableBoosterReward()
    {
        _coinsButton.gameObject.SetActive(false);
        _reward.SetActive(false);

        _boosterButton.gameObject.SetActive(true);
        _boosterTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Booster")}";
        _description.SetActive(true);
    }

    public void PrepareRewardData(float money)
    {
        _coinsReward = money * _coinsMultiplicator;
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
            _reward.SetActive(false);
            _isCoinsRewardActive = false;

            this.gameObject.SetActive(false);
        }
        else if (_isBoosterRewardActive)
        {
            OnEarningBoosterReward?.Invoke(_boosterTime, true);
            OnBoosterRewardEarned?.Invoke();
            FirebaseAnalytics.LogEvent(name: "booster_for_ads");

            _meteor.SetObjectPosition();

            _boosterButton.interactable = true;
            _boosterButton.gameObject.SetActive(false);
            _description.SetActive(false);
            _isBoosterRewardActive = false;

            this.gameObject.SetActive(false);
        }
    }

    public void GetCoinsBooster()
    {
        _boosterButton.interactable = false;
        _isBoosterRewardActive = true;
        _description.GetComponent<TextMeshProUGUI>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _adController.LoadAd();
    }

    public void GetCoins()
    {
        _coinsButton.interactable = false;
        _isCoinsRewardActive = true;
        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _adController.LoadAd();
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

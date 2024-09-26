using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Firebase.Analytics;
using TMPro;
using System;
using System.Collections;

public class CoinsReward : MonoBehaviour
{
    #region EDITOR FIELDS
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private Button buttonReward;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private float coinsMultiplicator;

    [Space(10)]
    [SerializeField] RewardTimers rewardTimer;
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnCoinsRewardReceived;

    public event Action<double> OnEarningReward;

    #endregion

    #region PRIVATE FIELDS

    private float _coinsReward;
    private bool _rewardedAdUsed;

    #endregion

    private void OnEnable()
    {
        _rewardedAdUsed = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    private void Start()
    {
        buttonReward.gameObject.SetActive(true);
        _title.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("GetCoins")}";
    }

    public void PrepareRewardData(float money)
    {
        _coinsReward = money * coinsMultiplicator;
        _coinsRewardTxt.text = $"{_coinsReward.ToString("N0")}";
    }

    public void UserEarnedReward()
    {
        this.gameObject.SetActive(false);
        OnEarningReward?.Invoke(_coinsReward);
        OnCoinsRewardReceived?.Invoke();

        FirebaseAnalytics.LogEvent(name: "coins_for_ads");

        buttonReward.interactable = true;
        buttonReward.gameObject.SetActive(false);
        _rewardedAdUsed = true;
    }

    public void GetCoins()
    {
        buttonReward.interactable = false;
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
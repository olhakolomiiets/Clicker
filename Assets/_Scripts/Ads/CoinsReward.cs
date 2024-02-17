using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GoogleMobileAds;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine.Events;
using Firebase.Analytics;

public class CoinsReward : MonoBehaviour
{
    #region EDITOR FIELDS

    GameData _currentGameData;

    [SerializeField] private Button buttonReward;
    [SerializeField] private GameObject getCoinsForAdsWindow;

    [SerializeField] private int coinsReward;
    [SerializeField] CoinsRewardTimer rewardTimer;

    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent;
    [HideInInspector] public UnityEvent RewardedAdLoadedEvent;
    [HideInInspector] public UnityEvent RewardedAdLoadedWithErrorEvent;

    #endregion

    #region PRIVATE FIELDS

    private bool _rewardedAdUsed;
    private int TotalScore;

    #endregion

    private void OnEnable()
    {
        _rewardedAdUsed = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    public void UserEarnedReward()
    {
        _currentGameData.Money += coinsReward;
        //rewardTimer.AdViewed();

        FirebaseAnalytics.LogEvent(name: "coins_for_ads");

        buttonReward.interactable = true;
        getCoinsForAdsWindow.SetActive(false);
        _rewardedAdUsed = true;
    }

    public void GetCoins()
    {
        buttonReward.interactable = false;
        buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";

        _adController.LoadAd();
    }

    public void ShowRewardedAd()
    {
        _adController.ShowAd();
    }

    public void RewardedAdWithError()
    {
        buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("RewardedAdError")}";
    }

    private void OnDisable()
    {
        _adController.OnUserEarnedRewardEvent.RemoveListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.RemoveListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.RemoveListener(RewardedAdWithError);
    }
}
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Firebase.Analytics;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System;

public class BoosterReward : MonoBehaviour
{
    #region EDITOR FIELDS
    [SerializeField] private TextMeshProUGUI _title;
    [SerializeField] private Button buttonReward;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnBoosterRewardEarned;

    #endregion

    #region PRIVATE FIELDS

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
        _title.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Booster")}";
    }

    public void UserEarnedReward()
    {
        OnBoosterRewardEarned?.Invoke();

        this.gameObject.SetActive(false);
        buttonReward.gameObject.SetActive(false);

        FirebaseAnalytics.LogEvent(name: "booster_for_ads");

        _rewardedAdUsed = true;
    }

    public void GetCoinsBooster()
    {
        //buttonReward.GetComponentInChildren<Text>().text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";
        _adController.LoadAd();

        //this.gameObject.SetActive(false);
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
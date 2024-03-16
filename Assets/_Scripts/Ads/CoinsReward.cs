using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using Firebase.Analytics;
using TMPro;

public class CoinsReward : MonoBehaviour
{
    #region EDITOR FIELDS
    
    [SerializeField] private ScorePanel _score;

    [Space(10)]
    [SerializeField] private Button buttonReward;
    [SerializeField] private GameObject getCoinsForAdsWindow;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private double coinsMultiplicator;

    [Space(10)]
    [SerializeField] RewardTimers rewardTimer;
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnCoinsRewardReceived;

    #endregion

    #region PRIVATE FIELDS

    GameData _currentGameData;
    private double _coinsReward;
    private bool _rewardedAdUsed;

    #endregion

    private void OnEnable()
    {
        _rewardedAdUsed = false;
        _adController.OnUserEarnedRewardEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);

    }

    public void PrepareRewardData(GameData gameData)
    {
        _currentGameData = gameData;
        _coinsReward = _currentGameData.Money * coinsMultiplicator;
        _coinsRewardTxt.text = $"{_coinsReward.ToString("N0")}";
    }

    public void UserEarnedReward()
    {       
        _currentGameData.Money += _coinsReward;
        _score.SetScore(_currentGameData.Money);
        OnCoinsRewardReceived?.Invoke();

        FirebaseAnalytics.LogEvent(name: "coins_for_ads");

        buttonReward.interactable = true;
        getCoinsForAdsWindow.SetActive(false);
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
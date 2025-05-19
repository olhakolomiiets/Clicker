using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using System;

public class RewardsManager : MonoBehaviour
{
    #region EDITOR FIELDS

    [Header("Coins Reward")]
    [SerializeField] private TextMeshProUGUI _coinsTitle;
    [SerializeField] private GameObject _coinsObj;
    [SerializeField] private TextMeshProUGUI _coinsRewardTxt;

    [Space(10)]
    [SerializeField] private float _coinsMultiplicator;

    [Header("Booster Reward")]
    [SerializeField] private TextMeshProUGUI _boosterTitle;
    [SerializeField] private GameObject _boosterObj;

    [Space(10)]
    [SerializeField] private int _boosterTime;

    [Header("Diamonds Reward")]
    [SerializeField] private TextMeshProUGUI _diamondsTitle;
    [SerializeField] private GameObject _diamondsObj;
    [SerializeField] private TextMeshProUGUI _diamondsRewardTxt;

    [Space(10)]
    [SerializeField] private Button _rewardButton;
    [SerializeField] private float _diamonds;

    [Space(10)]
    [SerializeField] private TextMeshProUGUI _serviceTxt;
    [SerializeField] AstronautMoveBetweenTransforms _meteor;
    [SerializeField] RewardTimers _rewardTimer;
    [SerializeField] private GoogleMobileAds.Sample.RewardedAdController _adController;

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent, OnAdClosedEvent, RewardedAdLoadedEvent, RewardedAdLoadedWithErrorEvent, OnCoinsRewardReceived, OnBoosterRewardEarned, OnDiamondsRewardReceived, OnRewardReceived;

    public event Action<double> OnEarningReward, OnEarningDiamonds;
    public event Action<int, bool> OnEarningBoosterReward;

    #endregion

    #region PRIVATE FIELDS

    private float _coinsReward;
    private bool _isCoinsRewardActive;
    private bool _isBoosterRewardActive;
    private bool _isDiamondsRewardActive;
    private bool _isRewardEarned;

    #endregion

    private void OnEnable()
    {
        _adController.OnUserEarnedRewardEvent.AddListener(SetRewardState);
        _adController.OnAdClosedEvent.AddListener(UserEarnedReward);
        _adController.RewardedAdLoadedEvent.AddListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.AddListener(RewardedAdWithError);
    }

    private void SetRewardState()
    {
        _isRewardEarned = true;

        Debug.Log("Rewards Manager /// SetRewardState() /// _isRewardEarned " + _isRewardEarned);
    }

    public void EnableCoinsReward()
    {
        _isCoinsRewardActive = true;

        _boosterObj.SetActive(false);
        _diamondsObj.SetActive(false);

        _rewardButton.gameObject.SetActive(true);
        _coinsObj.SetActive(true);
        _serviceTxt.text = $"";
        _coinsTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("GetCoins")}";
    }

    public void EnableDiamondsReward()
    {
        _isDiamondsRewardActive = true;

        _boosterObj.SetActive(false);
        _coinsObj.SetActive(false);

        _rewardButton.gameObject.SetActive(true);
        _diamondsObj.SetActive(true);
        _serviceTxt.text = $"";
        _diamondsTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("GetDiamonds")}";      
    }

    public void EnableBoosterReward()
    {
        _isBoosterRewardActive = true;

        _coinsObj.SetActive(false);
        _diamondsObj.SetActive(false);

        _rewardButton.gameObject.SetActive(true);
        _boosterObj.SetActive(true);
        _serviceTxt.text = $"";
        _boosterTitle.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Booster")}";    
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
        if (_isRewardEarned)
        {
            if (_isCoinsRewardActive)
            {
                _isCoinsRewardActive = false;

                OnEarningReward?.Invoke(_coinsReward);
                OnRewardReceived?.Invoke();

                _meteor.SetObjectPosition();
                _meteor.gameObject.SetActive(false);

                Debug.Log("Rewards Manager /// UserEarnedReward() /// Coins " + _coinsReward + "_isRewardEarned " + _isRewardEarned);
            }
            else if (_isBoosterRewardActive)
            {
                _isBoosterRewardActive = false;

                OnEarningBoosterReward?.Invoke(_boosterTime, true);
                OnRewardReceived?.Invoke();

                _meteor.SetObjectPosition();
                _meteor.gameObject.SetActive(false);

                Debug.Log("Rewards Manager /// UserEarnedReward() /// Booster " + _boosterTime + "_isRewardEarned " + _isRewardEarned);
            }
            else if (_isDiamondsRewardActive)
            {
                _isDiamondsRewardActive = false;

                OnEarningDiamonds?.Invoke(_diamonds);
                OnRewardReceived?.Invoke();

                _meteor.SetObjectPosition();
                _meteor.gameObject.SetActive(false);

                Debug.Log("Rewards Manager /// UserEarnedReward() /// Diamonds " + _diamonds + "_isRewardEarned " + _isRewardEarned);
            }

            _isRewardEarned = false;   
            Debug.Log("Rewards Manager /// _isRewardEarned " + _isRewardEarned);
        }
    }

    public void GetReward()
    {
        if (_isCoinsRewardActive)
        {
            _coinsObj.SetActive(false);
        }
        else if (_isBoosterRewardActive)
        {
            _boosterObj.SetActive(false);
        }
        else if (_isDiamondsRewardActive)
        {
            _diamondsObj.SetActive(false);
        }

        _rewardButton.gameObject.SetActive(false);
        _serviceTxt.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("Loading")}";
        _adController.LoadAd();
    }

    public void ShowRewardedAd()
    {
        _adController.ShowAd();
    }

    public void RewardedAdWithError()
    {
        _serviceTxt.text = $"{Lean.Localization.LeanLocalization.GetTranslationText("RewardedAdError")}";
    }

    private void OnDisable()
    {
        _adController.OnUserEarnedRewardEvent.RemoveListener(UserEarnedReward);
        _adController.OnUserEarnedRewardEvent.RemoveListener(SetRewardState);
        _adController.RewardedAdLoadedEvent.RemoveListener(ShowRewardedAd);
        _adController.RewardedAdLoadedWithErrorEvent.RemoveListener(RewardedAdWithError);
    }
}

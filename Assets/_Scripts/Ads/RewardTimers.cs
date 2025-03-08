using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class RewardTimers : MonoBehaviour
{
    [Header("Coins Reward")]
    [SerializeField] private RewardsManager _rewardsManager;
    [SerializeField] private GameObject _meteor;

    [Space(10)]
    [SerializeField] private GameObject _boosterTimer;
    [SerializeField] private TextMeshProUGUI _timerTxt;

    [Space(10)]
    [SerializeField] private List<ItemData> _creationItemsDataList;

    private DateTime endTime;
    private DateTime startTime;
    private int _boosterTime;
    private int timeLeft;
    private bool isTimeSaved = true;
    public bool isBoosterTimerActive;

    [Space(10)]
    public float activationInterval;

    [HideInInspector] public UnityEvent OnActivatedCoinsRewardButton, OnCoinsRewardReceived, OnBoosterRewardEarned, OnDiamondsRewardReceived;
    public event Action OnSetBoosterTimer, OnEndBoosterTimer;

    private void OnEnable()
    {
        _rewardsManager.OnCoinsRewardReceived.AddListener(StartBoosterRewardCoroutine);
        _rewardsManager.OnBoosterRewardEarned.AddListener(StartDiamondsRewardCoroutine);
        _rewardsManager.OnDiamondsRewardReceived.AddListener(StartCoinsRewardCoroutine);

        _rewardsManager.OnEarningBoosterReward += SetBoosterTimer;
    }

    void Start()
    {
        StartCoroutine(ActivateCoinsRewardAd());
        Debug.Log($"/// RewardTimers /// Start ///");
    }

    public void ActivateRewardAd(int index)
    {
        switch (index)
        {
            case 1:
                ActivateCoinsRewardObject();
                break;
            case 2:
                ActivateBoosterRewardObject();
                break;
            case 3:
                ActivateDiamondsRewardObject();
                break;
        }
    }

    IEnumerator ActivateCoinsRewardAd()
    {
        yield return new WaitForSecondsRealtime(activationInterval);
        ActivateCoinsRewardObject();
    }

    IEnumerator ActivateBoosterRewardAd()
    {
        yield return new WaitForSecondsRealtime(activationInterval);
        ActivateBoosterRewardObject();
    }

    IEnumerator ActivateDiamondsRewardAd()
    {
        yield return new WaitForSecondsRealtime(activationInterval);
        ActivateDiamondsRewardObject();
    }

    void ActivateCoinsRewardObject()
    {      
        _meteor.SetActive(true);
        OnActivatedCoinsRewardButton?.Invoke();
    }

    void ActivateBoosterRewardObject()
    {
        _meteor.SetActive(true);
        _rewardsManager.EnableBoosterReward();
    }

    void ActivateDiamondsRewardObject()
    {
        _meteor.SetActive(true);
        _rewardsManager.EnableDiamondsReward();
    }

    private void StartBoosterRewardCoroutine()
    {
        StartCoroutine(ActivateBoosterRewardAd());
    }

    private void StartCoinsRewardCoroutine()
    {
        StartCoroutine(ActivateCoinsRewardAd());
    }

    private void StartDiamondsRewardCoroutine()
    {
        StartCoroutine(ActivateDiamondsRewardAd());
    }

    private void SetBoosterTimer(int time, bool isReward)
    {
        DateTime now = DateTime.Now;
        int day = now.Day;
        int hour = now.Hour;
        int minute = now.Minute;
        int second = now.Second;

        startTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

        if (isReward)
            timeLeft += time;
        else
            timeLeft = time;

        endTime = startTime.AddSeconds(timeLeft);

        _boosterTimer.SetActive(true);
        OnSetBoosterTimer?.Invoke();
        isBoosterTimerActive = true;

        StartCoroutine(UpdateCoinsBoosterTimer());
    }

    IEnumerator UpdateCoinsBoosterTimer()
    {
        while (true)
        {
            TimeSpan timeRemaining = endTime - DateTime.Now;

            _timerTxt.text = string.Format("{0:D2}:{1:D2}", timeRemaining.Minutes, timeRemaining.Seconds);

            timeLeft = (int)timeRemaining.TotalSeconds;

            if (timeRemaining.Ticks <= 0)
            {
                DisableCoinsBooster();
                yield break;
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private void DisableCoinsBooster()
    {
        isBoosterTimerActive = false;
        OnEndBoosterTimer?.Invoke();
        _boosterTimer.SetActive(false);
    }

    private void SaveTimerState()
    {
        PlayerPrefs.SetInt("BoosterTimeRemaining", timeLeft);
        isTimeSaved = true;
    }

    private void LoadTimerState()
    {
        if (PlayerPrefs.HasKey("BoosterTimeRemaining"))
        {
            _boosterTime = PlayerPrefs.GetInt("BoosterTimeRemaining");

            _boosterTimer.SetActive(true);
            SetBoosterTimer(_boosterTime, false);

            isTimeSaved = false;
        }
    }

    private void OnApplicationFocus(bool focusStatus)
    {
        if (focusStatus)
        {
            if (isTimeSaved)
                LoadTimerState();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveTimerState();
            StopCoroutine(UpdateCoinsBoosterTimer());
        }
        else
        {
            if (isTimeSaved)
                LoadTimerState();
        }
    }

    private void OnDisable()
    {
        _rewardsManager.OnCoinsRewardReceived.RemoveListener(StartBoosterRewardCoroutine);
        _rewardsManager.OnBoosterRewardEarned.RemoveListener(StartDiamondsRewardCoroutine);
        _rewardsManager.OnDiamondsRewardReceived.RemoveListener(StartCoinsRewardCoroutine);
    }
}

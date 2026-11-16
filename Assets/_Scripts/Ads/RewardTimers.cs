using MoreMountains.Tools;
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
    private IEnumerator _boosterTimerRoutine;
    private int timeLeft;
    private bool isTimeSaved = true;
    public bool isBoosterTimerActive;

    [Space(10)]
    public float minDelay = 90f;
    public float maxDelay = 140f;

    [HideInInspector] public UnityEvent OnActivatedCoinsRewardButton, OnCoinsRewardReceived, OnBoosterRewardEarned, OnDiamondsRewardReceived;
    public event Action OnSetBoosterTimer, OnEndBoosterTimer;

    private void OnEnable()
    {
        _rewardsManager.OnRewardReceived.AddListener(StartMeteorCoroutine);
        _rewardsManager.OnEarningBoosterReward += SetBoosterTimer;

        if (isBoosterTimerActive && !isTimeSaved)
            StartBoosterTimer();
    }

    void Start()
    {
        StartCoroutine(ActivateCoinsRewardAd());
    }

    public float GetRandomDelay()
    {
        return UnityEngine.Random.Range(minDelay, maxDelay);
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
        float interval = GetRandomDelay();
        Debug.Log($"RewardTimers /// ActivateCoinsRewardAd /// Random Delay: {interval}");
        yield return new WaitForSecondsRealtime(interval);
        ActivateCoinsRewardObject();
    }

    IEnumerator ActivateMeteor()
    {
        float interval = GetRandomDelay();
        Debug.Log($"RewardTimers /// ActivateCoinsRewardAd /// Random Delay: {interval}");
        yield return new WaitForSecondsRealtime(interval);
        _meteor.SetActive(true);
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

    private void StartMeteorCoroutine()
    {
        StartCoroutine(ActivateMeteor());
    }

    private void SetBoosterTimer(int time, bool isReward)
    {
        if (isReward)
            timeLeft += time;
        else
            timeLeft = time;

        if (timeLeft <= 0)
        {
            StopBoosterTimer();
            DisableCoinsBooster();
            return;
        }

        DateTime now = DateTime.Now;
        DateTime startTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        endTime = startTime.AddSeconds(timeLeft);

        isTimeSaved = false;
        isBoosterTimerActive = true;
        _boosterTimer.SetActive(true);
        StartBoosterTimer();
        if (isBoosterTimerActive)
            OnSetBoosterTimer?.Invoke();
    }

    private void StartBoosterTimer()
    {
        if (_boosterTimerRoutine != null)
            return;

        _boosterTimerRoutine = UpdateCoinsBoosterTimer();
        StartCoroutine(_boosterTimerRoutine);
    }

    private void StopBoosterTimer()
    {
        if (_boosterTimerRoutine == null)
            return;

        StopCoroutine(_boosterTimerRoutine);
        _boosterTimerRoutine = null;
    }

    IEnumerator UpdateCoinsBoosterTimer()
    {
        while (true)
        {
            TimeSpan timeRemaining = endTime - DateTime.Now;

            if (timeRemaining.Ticks <= 0)
            {
                _boosterTimerRoutine = null;
                DisableCoinsBooster();
                yield break;
            }

            timeLeft = (int)timeRemaining.TotalSeconds;
            _timerTxt.text = string.Format("{0:D2}:{1:D2}", timeRemaining.Minutes, timeRemaining.Seconds);

            yield return new WaitForSeconds(1f);
        }
    }

    private void DisableCoinsBooster()
    {
        timeLeft = 0;
        if (!isBoosterTimerActive)
            return;

        isBoosterTimerActive = false;
        _boosterTimer.SetActive(false);
        OnEndBoosterTimer?.Invoke();
    }

    private void SaveTimerState()
    {
        timeLeft = isBoosterTimerActive ? Math.Max(0, (int)(endTime - DateTime.Now).TotalSeconds) : 0;
        PlayerPrefs.SetInt("BoosterTimeRemaining", timeLeft);
        isTimeSaved = true;
        StopBoosterTimer();
        if (timeLeft == 0)
            DisableCoinsBooster();
    }

    private void LoadTimerState()
    {
        isTimeSaved = false;
        SetBoosterTimer(PlayerPrefs.GetInt("BoosterTimeRemaining", 0), false);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            if (!isTimeSaved)
                SaveTimerState();
            Debug.Log("Reward Timers /// OnApplicationPause() /// Save");
        }
        else
        {
            if (isTimeSaved)
                LoadTimerState();
            Debug.Log("Reward Timers /// OnApplicationPause() /// Load");
        }
    }

    private void OnDisable()
    {
        _rewardsManager.OnRewardReceived.RemoveListener(StartMeteorCoroutine);
        _rewardsManager.OnEarningBoosterReward -= SetBoosterTimer;
        StopBoosterTimer();
    }
}

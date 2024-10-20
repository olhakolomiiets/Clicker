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

    [Space(10)]
    [SerializeField] private GameObject _boosterTimer;
    [SerializeField] private TextMeshProUGUI _timerTxt;
    [SerializeField] private int _boosterTime;

    [Space(10)]
    [SerializeField] private List<ItemData> _creationItemsDataList;

    private DateTime endTime;
    private DateTime startTime;
    [SerializeField] private int timeLeft;
    private bool isTimeSaved = true;
    private bool isBooster;

    [Space(10)]
    [SerializeField] private float activationInterval;

    [HideInInspector] public UnityEvent OnActivatedCoinsRewardButton, OnBoosterRewardReceived, OnCoinsRewardReceived, OnBoosterRewardEarned;

    private void OnEnable()
    {
        _rewardsManager.OnCoinsRewardReceived.AddListener(StartBoosterRewardCoroutine);
        _rewardsManager.OnBoosterRewardEarned.AddListener(StartCoinsRewardCoroutine);

        _rewardsManager.OnEarningBoosterReward += SetBoosterTimer;
    }

    void Start()
    {
        ActivateRewardAd();
    }

    public void ActivateRewardAd()
    {
        if (!isBooster)
        {
            //StartCoroutine(ActivateCoinsRewardAd());
            ActivateCoinsRewardObject();
            isBooster = true;
        }

        else
        {
            //StartCoroutine(ActivateBoosterRewardAd());
            ActivateBoosterRewardObject();
            isBooster = false;
        }

    }

    IEnumerator ActivateCoinsRewardAd()
    {
        yield return new WaitForSeconds(activationInterval);
        ActivateCoinsRewardObject();
        isBooster = true;
    }

    IEnumerator ActivateBoosterRewardAd()
    {
        yield return new WaitForSeconds(activationInterval);
        ActivateBoosterRewardObject();
        isBooster = false;
    }

    void ActivateCoinsRewardObject()
    {
        _rewardsManager.gameObject.SetActive(true);
        OnActivatedCoinsRewardButton?.Invoke();
        _rewardsManager.EnableCoinsReward();
    }

    void ActivateBoosterRewardObject()
    {
        _rewardsManager.gameObject.SetActive(true);
        _rewardsManager.EnableBoosterReward();
    }

    private void StartBoosterRewardCoroutine()
    {
        StartCoroutine(ActivateBoosterRewardAd());
    }

    private void StartCoinsRewardCoroutine()
    {
        StartCoroutine(ActivateCoinsRewardAd());
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

        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].BoosterMultiplier = 2;
        }

        StartCoroutine(UpdateCoinsBoosterTimer());
        //StartCoroutine(ActivateCoinsRewardAd());
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

            Debug.Log("++++++++++++++++++++++ Update Coins Booster Timer " + _timerTxt);

            yield return new WaitForSeconds(1f);
        }
    }

    private void DisableCoinsBooster()
    {
        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].BoosterMultiplier = 1;
        }
        OnBoosterRewardReceived?.Invoke();
        _boosterTimer.SetActive(false);
    }

    private void DisableBooster()
    {
        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].BoosterMultiplier = 1;
        }
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
        DisableBooster();
        _rewardsManager.OnCoinsRewardReceived.RemoveListener(StartBoosterRewardCoroutine);
        _rewardsManager.OnBoosterRewardEarned.RemoveListener(StartCoinsRewardCoroutine);
    }
}

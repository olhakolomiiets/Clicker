using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class RewardTimers : MonoBehaviour
{
    [Header("Coins Reward")]
    [SerializeField] private CoinsReward _coinsReward;

    [Header("Booster Reward")]
    [SerializeField] private BoosterReward _boosterReward;

    [Space(10)]
    [SerializeField] private GameObject _boosterTimer;
    [SerializeField] private TextMeshProUGUI _timerTxt;
    [SerializeField] private int _boosterTime;

    [Space(10)]
    [SerializeField] private List<ItemData> _creationItemsDataList;

    private DateTime startTime;
    private DateTime endTime;

    [Space(10)]
    [SerializeField] private float activationInterval;

    [HideInInspector] public UnityEvent OnActivatedCoinsRewardButton, OnActivatedBoosterRewardButton, OnBoosterRewardReceived, OnCoinsRewardReceived, OnBoosterRewardEarned;

    private void OnEnable()
    {
        _coinsReward.OnCoinsRewardReceived.AddListener(StartBoosterRewardCoroutine);
        _boosterReward.OnBoosterRewardEarned.AddListener(SetBoosterTimer);
    }

    void Start()
    {
        StartCoroutine(ActivateCoinsRewardAd());
    }

    IEnumerator ActivateCoinsRewardAd()
    {
        yield return new WaitForSeconds(activationInterval);
        ActivateCoinsRewardObject();
    }

    IEnumerator ActivateBoosterRewardAd()
    {
        yield return new WaitForSeconds(activationInterval);
        ActivateBoosterRewardObject();
    }

    void ActivateCoinsRewardObject()
    {
        _boosterReward.enabled = false;
        _coinsReward.enabled = true;
        _coinsReward.gameObject.SetActive(true);
        OnActivatedCoinsRewardButton?.Invoke();
    }

    void ActivateBoosterRewardObject()
    {
        //OnActivatedBoosterRewardButton?.Invoke();
        _coinsReward.enabled = false;
        _boosterReward.enabled = true;
        _boosterReward.gameObject.SetActive(true);
    }

    private void StartCoinsRewardCoroutine()
    {
        StartCoroutine(ActivateCoinsRewardAd());
    }

    private void StartBoosterRewardCoroutine()
    {
        _coinsReward.gameObject.SetActive(false);
        StartCoroutine(ActivateBoosterRewardAd());
    }

    private void SetBoosterTimer()
    {
        DateTime now = DateTime.Now;
        int day = now.Day;
        int hour = now.Hour;
        int minute = now.Minute;
        int second = now.Second;

        startTime = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);
        endTime = startTime.AddMinutes(_boosterTime);

        _boosterTimer.SetActive(true);

        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].MultiplyItemBaseIncomeBy2();
        }

        StartCoroutine(UpdateCoinsBoosterTimer());
    }

    IEnumerator UpdateCoinsBoosterTimer()
    {
        while (true)
        {
            TimeSpan timeRemaining = endTime - DateTime.Now;

            _timerTxt.text = string.Format("{0:D2}:{1:D2}", timeRemaining.Minutes, timeRemaining.Seconds);

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
            _creationItemsDataList[i].DivideItemBaseIncomeBy2();
        }
        OnBoosterRewardReceived?.Invoke();
        StartCoroutine(ActivateCoinsRewardAd());
        _boosterTimer.SetActive(false);
    }

    private void OnDisable()
    {
        _coinsReward.OnCoinsRewardReceived.RemoveListener(StartBoosterRewardCoroutine);
        _boosterReward.OnBoosterRewardEarned.RemoveListener(SetBoosterTimer);
    }
}

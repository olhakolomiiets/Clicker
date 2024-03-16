using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class RewardTimers : MonoBehaviour
{
    [Header("Coins Reward")]
    [SerializeField] private CoinsReward _coinsReward;

    [Header("Booster Reward")]
    [SerializeField] private BoosterReward _boosterReward;

    [Space(10)]
    [SerializeField] private float activationInterval;

    [HideInInspector] public UnityEvent OnActivatedCoinsRewardButton, OnActivatedBoosterRewardButton, OnBoosterRewardReceived, OnCoinsRewardReceived;

    private void OnEnable()
    {
        _coinsReward.OnCoinsRewardReceived.AddListener(StartBoosterRewardCoroutine);
        _boosterReward.OnBoosterRewardReceived.AddListener(StartCoinsRewardCoroutine);
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
        OnActivatedCoinsRewardButton?.Invoke();
        _coinsReward.gameObject.SetActive(true);
    }

    void ActivateBoosterRewardObject()
    {
        //OnActivatedBoosterRewardButton?.Invoke();
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

    private void OnDisable()
    {
        _coinsReward.OnCoinsRewardReceived.RemoveListener(StartBoosterRewardCoroutine);
        _boosterReward.OnBoosterRewardReceived.RemoveListener(StartCoinsRewardCoroutine);
    }
}

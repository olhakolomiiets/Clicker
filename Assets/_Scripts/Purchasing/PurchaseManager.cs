using Firebase.Analytics;
using System;
using System.Collections.Generic;
using UnityEngine;

public class PurchaseManager : MonoBehaviour
{
    [Header("No Ads")]
    [SerializeField] private GameObject _noAdsButton;

    [Header("Starter Pack")]
    [SerializeField] private GameObject _starterPackButton;
    [SerializeField] private double _coinsInPack;
    [SerializeField] private double _diamondsInPack;

    [Header("Special Offer")]
    [SerializeField] private GameObject _specialOfferButton;
    [SerializeField] private double _coins;
    [SerializeField] private double _diamonds;

    [Header("Diamonds Packs")]
    [SerializeField] private double _diamondsInPack1;
    [SerializeField] private double _diamondsInPack2;
    [SerializeField] private double _diamondsInPack3;

    [Header("Coins Booster")]
    [SerializeField] private GameObject _coinsBoosterButton;
    [SerializeField] private List<ItemData> _creationItemsDataList;

    public event Action<double, double> OnPurchasingPack;
    public event Action<double> OnPurchasingDiamonds;
    public event Action OnPurchasingBooster;


    private void Start()
    {
        RestoreVariable();
    }

    public void NoAds()
    {
        PlayerPrefs.SetInt("adsRemoved", 1);
        _noAdsButton.SetActive(false);  
        FirebaseAnalytics.LogEvent(name: "no_ads_purchased");   
    }

    public void StarterPack()
    {
        OnPurchasingPack?.Invoke(_coinsInPack, _diamondsInPack);
        _starterPackButton.SetActive(false);
        PlayerPrefs.SetInt("StarterPackPurchased", 1);
        UpdateShopUI();
        FirebaseAnalytics.LogEvent(name: "money_starterPack_purchased");
    }

    public void SpecialOffer()
    {
        OnPurchasingPack?.Invoke(_coins, _diamonds);
        NoAds();
        _specialOfferButton.SetActive(false);
        PlayerPrefs.SetInt("SpecialOfferPurchased", 1);
        UpdateShopUI();
        FirebaseAnalytics.LogEvent(name: "money_specialOffer_purchased");
    }

    public void CoinsBooster()
    {
        OnPurchasingBooster?.Invoke();
        _coinsBoosterButton.SetActive(false);
        PlayerPrefs.SetInt("CoinsBoosterPurchased", 1);
        UpdateShopUI();
        FirebaseAnalytics.LogEvent(name: "money_coinsBooster_purchased");
    }

    public void DiamondsPack50()
    {
        OnPurchasingDiamonds?.Invoke(_diamondsInPack1);
        FirebaseAnalytics.LogEvent(name: "money_diamondsPack50_purchased");
    }
    public void DiamondsPack100()
    {
        OnPurchasingDiamonds?.Invoke(_diamondsInPack2);
        FirebaseAnalytics.LogEvent(name: "money_diamondsPack100_purchased");
    }

    public void DiamondsPack300()
    {
        OnPurchasingDiamonds?.Invoke(_diamondsInPack3);
        FirebaseAnalytics.LogEvent(name: "money_diamondsPack300_purchased");
    }

    void RestoreVariable()
    {
        if (PlayerPrefs.GetInt("StarterPackPurchased") == 1)
        {
            _starterPackButton.SetActive(false);
        }
        if (PlayerPrefs.GetInt("SpecialOfferPurchased") == 1)
        {
            _specialOfferButton.SetActive(false);
            _noAdsButton.SetActive(false);
        }
        if (PlayerPrefs.GetInt("CoinsBoosterPurchased") == 1)
        {
            _coinsBoosterButton.SetActive(false);
        }
        if (PlayerPrefs.GetInt("adsRemoved") == 1)
        {
            _noAdsButton.SetActive(false);
        }

        UpdateShopUI();
    }

    public void UpdateShopUI()
    {
        RectTransform shopItemParent = GetComponent<RectTransform>();
        int activeChildrenCount = GetActiveChildrenCount(shopItemParent);

        float _scrollItemGroupHeight = 165 * activeChildrenCount;
        shopItemParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);
    }

    int GetActiveChildrenCount(RectTransform parent)
    {
        int count = 0;
        foreach (Transform child in parent)
        {
            if (child.gameObject.activeSelf)
                count++;
        }
        return count;
    }
}

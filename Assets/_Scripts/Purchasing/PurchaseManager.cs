using Firebase.Analytics;
using GoogleMobileAds.Sample;
using Samples.Purchasing.IAP5.Demo;
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

    [Header("Planet Currency Packs")]
    [SerializeField] private List<GameObject> _gearsPackButtons = new();
    [SerializeField] private List<GameObject> _batteriesPackButtons = new();
    [SerializeField] private List<GameObject> _likesPackButtons = new();

    [Header("Coins Booster")]
    [SerializeField] private GameObject _coinsBoosterCard;
    [SerializeField] private GameObject _coinsBoosterButton;
    [SerializeField] private GameObject _coinsBoosterPurchasedText;
    [SerializeField] private List<ItemData> _creationItemsDataList;

    public event Action<double, double> OnPurchasingPack;
    public event Action<double> OnPurchasingDiamonds;
    public event Action OnPurchasingBooster;


    private void OnEnable()
    {
        RefreshShopVisibility();
    }

    public void NoAds()
    {
        PlayerPrefs.SetInt(SaveSystem.AdsRemovedKey, 1);
        _noAdsButton.SetActive(false);
        ApplyNoAds();
        FirebaseAnalytics.LogEvent(name: "no_ads_purchased");
    }

    internal static void ApplyNoAds()
    {
        try
        {
            foreach (var banner in FindObjectsByType<BannerViewController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                banner.DestroyAd();

            foreach (var banner in FindObjectsByType<StandardBannerViewController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                banner.DestroyAd();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
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
        PlayerPrefs.SetInt(SaveSystem.CoinsBoosterPurchasedKey, 1);
        OnPurchasingBooster?.Invoke();
        RefreshShopVisibility();
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

    public static bool IsProductAllowedInShop(string productId, string sceneName)
    {
        if (sceneName is not ("PlanetMeta" or "PlanetLevel1" or "PlanetLevel3" or "PlanetLevel4"))
            return false;

        return productId switch
        {
            "com.planetclicker.diamonds.mini" or "com.planetclicker.diamonds.small" or "com.planetclicker.diamonds.medium" or "com.planetclicker.diamonds.large" => true,
            "com.planetclicker.coins.1p.small" or "com.planetclicker.coins.1p.medium" or "com.planetclicker.coins.1p.large" => sceneName == "PlanetLevel1",
            "com.planetclicker.coins.2p.small" or "com.planetclicker.coins.2p.medium" or "com.planetclicker.coins.2p.large" => sceneName == "PlanetLevel3",
            "com.planetclicker.coins.3p.small" or "com.planetclicker.coins.3p.medium" or "com.planetclicker.coins.3p.large" => sceneName == "PlanetLevel4",
            "com.planetclicker.starterpack.v2" => sceneName == "PlanetLevel1" && !HasPurchased("StarterPackPurchased"),
            "com.planetclicker.specialoffer.v2" => sceneName == "PlanetLevel1" && !HasPurchased("SpecialOfferPurchased"),
            "com.planetclicker.noads" => sceneName != "PlanetLevel1" && !HasNoAds(),
            "com.planetclicker.coinsbooster" => sceneName != "PlanetMeta" && !SaveSystem.HasPurchasedCoinsBooster,
            _ => false
        };
    }

    public void RefreshShopVisibility()
    {
        PaywallManager paywall = PaywallManager.Instance;
        bool programmaticShop = paywall != null && paywall.IsPurchaseProcessingReady;
        string sceneName = gameObject.scene.name;

        SetPackCardsActive(_gearsPackButtons, programmaticShop && IsProductAllowedInShop("com.planetclicker.coins.1p.small", sceneName));
        SetPackCardsActive(_batteriesPackButtons, programmaticShop && IsProductAllowedInShop("com.planetclicker.coins.2p.small", sceneName));
        SetPackCardsActive(_likesPackButtons, programmaticShop && IsProductAllowedInShop("com.planetclicker.coins.3p.small", sceneName));

        if (programmaticShop)
        {
            SetCardActive(_starterPackButton, IsProductAllowedInShop("com.planetclicker.starterpack.v2", sceneName));
            SetCardActive(_specialOfferButton, IsProductAllowedInShop("com.planetclicker.specialoffer.v2", sceneName));
            SetCardActive(_noAdsButton, IsProductAllowedInShop("com.planetclicker.noads", sceneName));
            SetCardActive(_coinsBoosterCard, sceneName is "PlanetLevel1" or "PlanetLevel3" or "PlanetLevel4");
        }
        else
        {
            RestoreVariable();
        }

        bool boosterPurchased = SaveSystem.HasPurchasedCoinsBooster;
        SetCardActive(_coinsBoosterButton, !boosterPurchased);
        SetCardActive(_coinsBoosterPurchasedText, boosterPurchased);

        UpdateShopUI();
    }

    void RestoreVariable()
    {
        if (HasPurchased("StarterPackPurchased"))
            SetCardActive(_starterPackButton, false);

        if (HasPurchased("SpecialOfferPurchased"))
            SetCardActive(_specialOfferButton, false);

        if (HasNoAds())
            SetCardActive(_noAdsButton, false);
    }

    private static bool HasPurchased(string key)
    {
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    private static bool HasNoAds()
    {
        return SaveSystem.HasRemovedAds || HasPurchased("SpecialOfferPurchased");
    }

    private static void SetPackCardsActive(List<GameObject> cards, bool active)
    {
        if (cards == null)
            return;

        foreach (GameObject card in cards)
            SetCardActive(card, active);
    }

    private static void SetCardActive(GameObject card, bool active)
    {
        if (card != null && card.activeSelf != active)
            card.SetActive(active);
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

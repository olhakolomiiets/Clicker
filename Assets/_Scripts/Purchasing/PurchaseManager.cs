using Firebase.Analytics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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


    [SerializeField] private IAPManager _purchaseController;

    [HideInInspector] public UnityEvent PurchasedProductNoAds;
    [HideInInspector] public UnityEvent PurchasedProductStarterPack;
    [HideInInspector] public UnityEvent PurchasedProductSpecialOffer;
    [HideInInspector] public UnityEvent PurchasedProductCoinsBooster;
    [HideInInspector] public UnityEvent PurchasedProductDiamondsPack50;
    [HideInInspector] public UnityEvent PurchasedProductDiamondsPack100;
    [HideInInspector] public UnityEvent PurchasedProductDiamondsPack300;

    public event Action<double, double> OnPurchasingPack;
    public event Action<double> OnPurchasingDiamonds;
    public event Action OnPurchasingBooster;

    private void Awake()
    {
        _purchaseController = FindAnyObjectByType<IAPManager>();
    }

    private void OnEnable()
    {
        _purchaseController.PurchasedProductNoAds.AddListener(NoAds);
        _purchaseController.PurchasedProductStarterPack.AddListener(StarterPack);
        _purchaseController.PurchasedProductSpecialOffer.AddListener(SpecialOffer);
        _purchaseController.PurchasedProductCoinsBooster.AddListener(CoinsBooster);
        _purchaseController.PurchasedProductDiamondsPack50.AddListener(DiamondsPack50);
        _purchaseController.PurchasedProductDiamondsPack100.AddListener(DiamondsPack100);
        _purchaseController.PurchasedProductDiamondsPack300.AddListener(DiamondsPack300);
    }
    private void Start()
    {
        RestoreVariable();
    }

    public void NoAds()
    {
        _noAdsButton.SetActive(false);
    }

    public void StarterPack()
    {
        OnPurchasingPack?.Invoke(_coinsInPack, _diamondsInPack);
        _starterPackButton.SetActive(false);
        PlayerPrefs.SetInt("StarterPackPurchased", 1);
        UpdateShopUI();
    }

    public void SpecialOffer()
    {
        OnPurchasingPack?.Invoke(_coins, _diamonds);
        _noAdsButton.SetActive(false);
        _specialOfferButton.SetActive(false);
        PlayerPrefs.SetInt("SpecialOfferPurchased", 1);
        UpdateShopUI();
    }

    public void CoinsBooster()
    {
        OnPurchasingBooster?.Invoke();
        _coinsBoosterButton.SetActive(false);
        PlayerPrefs.SetInt("CoinsBoosterPurchased", 1);
        UpdateShopUI();
    }

    public void DiamondsPack50()
    {
        OnPurchasingDiamonds?.Invoke(_diamondsInPack1);
    }
    public void DiamondsPack100()
    {
        OnPurchasingDiamonds?.Invoke(_diamondsInPack2);
    }

    public void DiamondsPack300()
    {
        OnPurchasingDiamonds?.Invoke(_diamondsInPack3);
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

    private void OnDisable()
    {
        _purchaseController.PurchasedProductNoAds.RemoveListener(NoAds);
        _purchaseController.PurchasedProductStarterPack.RemoveListener(StarterPack);
        _purchaseController.PurchasedProductSpecialOffer.RemoveListener(SpecialOffer);
        _purchaseController.PurchasedProductCoinsBooster.RemoveListener(CoinsBooster);
        _purchaseController.PurchasedProductDiamondsPack50.RemoveListener(DiamondsPack50);
        _purchaseController.PurchasedProductDiamondsPack100.RemoveListener(DiamondsPack100);
        _purchaseController.PurchasedProductDiamondsPack300.RemoveListener(DiamondsPack300);
    }
}

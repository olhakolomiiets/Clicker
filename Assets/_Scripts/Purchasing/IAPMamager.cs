using Firebase.Analytics;
using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Purchasing;

public class IAPManager : MonoBehaviour, IStoreListener
{
    public static IAPManager instance = null;

    private static IStoreController _storeController;
    private static IExtensionProvider _extensionProvider;

    private string noAds = "com.planetclicker.noads";
    private string starterPack = "com.planetclicker.starterpack";
    private string specialOffer = "com.planetclicker.specialoffer";
    private string coinsBooster = "com.planetclicker.coinsbooster";
    private string diamondsPack50 = "com.planetclicker.diamondspack50";
    private string diamondsPack100 = "com.planetclicker.diamondspack100";
    private string diamondsPack300 = "com.planetclicker.diamondspack300";


    [HideInInspector] public UnityEvent PurchasedProductNoAds;
    [HideInInspector] public UnityEvent PurchasedProductStarterPack;
    [HideInInspector] public UnityEvent PurchasedProductSpecialOffer;
    [HideInInspector] public UnityEvent PurchasedProductCoinsBooster;
    [HideInInspector] public UnityEvent PurchasedProductDiamondsPack50;
    [HideInInspector] public UnityEvent PurchasedProductDiamondsPack100;
    [HideInInspector] public UnityEvent PurchasedProductDiamondsPack300;



    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (_storeController == null)
        {
            Invoke("InitializePurchasing", 2f);
        }
    }

    void InitializePurchasing()
    {
        if (IsInitialized())
        {
            return;
        }

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());

        builder.AddProduct(noAds, ProductType.NonConsumable);
        builder.AddProduct(starterPack, ProductType.Consumable);
        builder.AddProduct(specialOffer, ProductType.Consumable);
        builder.AddProduct(coinsBooster, ProductType.Consumable);
        builder.AddProduct(diamondsPack50, ProductType.Consumable);
        builder.AddProduct(diamondsPack100, ProductType.Consumable);
        builder.AddProduct(diamondsPack300, ProductType.Consumable);

        UnityPurchasing.Initialize(this, builder);

    }

    public void BuyProduct(string productName)
    {
        if (IsInitialized())
        {
            Product product = _storeController.products.WithID(productName);

            if (product != null && product.availableToPurchase)
            {
                Debug.Log(string.Format("Purchasing product asychronously: '{0}'", product.definition.id));

                _storeController.InitiatePurchase(product);
            }
            else
            {
                Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
            }
        }
        else
        {
            Debug.Log("BuyProductID FAIL. Not initialized.");
        }
    }

    #region PURCHASE CONTROL
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        var product = args.purchasedProduct;

        if (String.Equals(product.definition.id, noAds, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductNoAds.Invoke();

            NoAds();

            FirebaseAnalytics.LogEvent(name: "no_ads_purchased");
        }
        else if (String.Equals(product.definition.id, starterPack, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductStarterPack.Invoke();

            FirebaseAnalytics.LogEvent(name: "money_starterPack_purchased");
        }
        else if (String.Equals(product.definition.id, specialOffer, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductSpecialOffer.Invoke();

            FirebaseAnalytics.LogEvent(name: "money_specialOffer_purchased");
        }
        else if (String.Equals(product.definition.id, coinsBooster, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductCoinsBooster.Invoke();

            FirebaseAnalytics.LogEvent(name: "money_coinsBooster_purchased");
        }
        else if (String.Equals(product.definition.id, diamondsPack50, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductDiamondsPack50.Invoke();

            FirebaseAnalytics.LogEvent(name: "money_diamondsPack50_purchased");
        }
        else if (String.Equals(product.definition.id, diamondsPack100, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductDiamondsPack100.Invoke();

            FirebaseAnalytics.LogEvent(name: "money_diamondsPack100_purchased");
        }
        else if (String.Equals(product.definition.id, diamondsPack300, StringComparison.Ordinal))
        {
            Debug.Log(string.Format("ProcessPurchase: PASS. Product: '{0}'", product.definition.id));

            PurchasedProductDiamondsPack300.Invoke();

            FirebaseAnalytics.LogEvent(name: "money_diamondsPack300_purchased");
        }
        else
        {
            Debug.Log(string.Format("ProcessPurchase: FAIL. Unrecognized product: '{0}'", product.definition.id));
        }

        return PurchaseProcessingResult.Complete;
    }
    #endregion

    #region PURCHASES RESTORE METHODS

    public void RestorePurchases() //For AppStore
    {
        if (!IsInitialized())
        {
            Debug.Log("RestorePurchases FAIL. Not initialized.");
            return;
        }

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.OSXPlayer)
        {
            Debug.Log("RestorePurchases started ...");

            var apple = _extensionProvider.GetExtension<IAppleExtensions>();

            apple.RestoreTransactions((result) =>
            {
                Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
            });
        }
        else
        {
            Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
        }
    }

    private void NoAds()
    {
        PlayerPrefs.SetInt("adsRemoved", 1);
    }

    #endregion

    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("In-App Purchasing successfully initialized");
        _storeController = controller;
        _extensionProvider = extensions;
    }

    private bool IsInitialized()
    {
        return _storeController != null && _extensionProvider != null;
    }

    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log($"In-App Purchasing initialize failed: {error}");
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        Debug.Log($"In-App Purchasing initialize failed: {error}, Message: {message}");
    }

    public void OnPurchaseComplete(Product product)
    {
        Debug.Log($"Purchase completed - Product: '{product.definition.id}'");
    }

    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log($"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureReason}");
    }

}
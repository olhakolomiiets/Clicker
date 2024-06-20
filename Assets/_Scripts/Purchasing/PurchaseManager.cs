using Firebase.Analytics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PurchaseManager : MonoBehaviour
{
    [SerializeField] private ScorePanel _coinsPanel;
    [SerializeField] private ScorePanel _diamondsPanel;

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

    GameData _currentGameData;
    GeneralGameData _currentGeneralData;

    private void Awake()
    {
        _purchaseController = FindObjectOfType<IAPManager>();
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
        //RestoreVariable();
    }

    public void PrepareGameData(GameData gameData)
    {
        _currentGameData = gameData;
    }

    public void PrepareGeneralData(GeneralGameData generalData)
    {
        _currentGeneralData = generalData;
    }

    public void NoAds()
    {
        //PlayerPrefs.SetInt("NoAdsPurchased", 1);
    }

    public void StarterPack()
    {
        _currentGameData.Money += _coinsInPack;
        _currentGeneralData.Diamonds += _diamondsInPack;
        _coinsPanel.SetScore(_currentGameData.Money);
        _diamondsPanel.SetDiamondsScore(_currentGeneralData.Diamonds);
        _starterPackButton.SetActive(false);
        //PlayerPrefs.SetInt("StarterPackPurchased", 1);
    }

    public void SpecialOffer()
    {
        _currentGameData.Money += _coins;
        _currentGeneralData.Diamonds += _diamonds;
        _coinsPanel.SetScore(_currentGameData.Money);
        _diamondsPanel.SetDiamondsScore(_currentGeneralData.Diamonds);
        _specialOfferButton.SetActive(false);
        //PlayerPrefs.SetInt("SpecialOfferPurchased", 1);
    }

    public void CoinsBooster()
    {
        for (int i = 0; i < _creationItemsDataList.Count; i++)
        {
            _creationItemsDataList[i].MultiplyItemBaseIncomeBy2();
        }
        _coinsBoosterButton.SetActive(false);
        //PlayerPrefs.SetInt("CoinsBoosterPurchased", 1);
    }

    public void DiamondsPack50()
    {
        _currentGeneralData.Diamonds += _diamondsInPack1;
        _diamondsPanel.SetDiamondsScore(_currentGeneralData.Diamonds);
    }
    public void DiamondsPack100()
    {
        _currentGeneralData.Diamonds += _diamondsInPack2;
        _diamondsPanel.SetDiamondsScore(_currentGeneralData.Diamonds);
    }    
    
    public void DiamondsPack300()
    {
        _currentGeneralData.Diamonds += _diamondsInPack3;
        _diamondsPanel.SetDiamondsScore(_currentGeneralData.Diamonds);
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
        }
        if (PlayerPrefs.GetInt("CoinsBoosterPurchased") == 1)
        {
            _coinsBoosterButton.SetActive(false);
        }
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

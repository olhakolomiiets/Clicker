using DG.Tweening;
using Firebase.Analytics;
using MoreMountains.Feedbacks;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Handles the information exchange between UI and other scripts
/// </summary>
public class GameUI : MonoBehaviour
{
    #region EDITOR FIELDS
    [Header("Score Panel")]
    [SerializeField] private ScorePanel _coins;
    [SerializeField] private ScorePanel _diamonds;

    [Header("Creation Store")]
    [SerializeField] private GameObject _uiItemPrefab;
    [SerializeField] private RectTransform _uiItemParent;
    [SerializeField] private List<ObjectActivator> _objectActivator = new();
    private List<UIManagerController> _managerControllers = new();
    private List<ItemController> _uiCreationItemsList = new();

    [Header("Upgrade Store")]
    [SerializeField] private GameObject _upgradeItemPrefab;
    [SerializeField] private RectTransform _upgradeItemParent;
    [SerializeField] private List<ObjectActivator> _upgradeObjectActivator = new();
    private List<UpgradeItemController> _uiUpgradeItemsList = new();

    //[SerializeField] private List<GameObject> _trees;
    //[SerializeField] private GameObject _decorations;
    //[SerializeField] private GameObject _buildings;
    //[SerializeField] private List<GameObject> _extraObjs;
    //[SerializeField] private GameObject _humans;
    //[SerializeField] private GameObject _animals;

    [SerializeField] private MMF_Player mmfPlayer;

    #endregion

    #region PRIVATE FIELDS

    private bool isTreesDisplayed;
    private bool isDecorationsDisplayed;
    private bool isBuildingsDisplayed;
    private bool isExtraObjsDisplayed;
    private bool isHumansDisplayed;
    private bool isAnimalsDisplayed;

    #endregion

    public event Action<int> OnProgressButtonClicked, OnWorkFinished, OnFirstActivation, OnUpdateWorkFinished, OnBuyButonClicked, OnActivationPremium, OnUpgradeItemPurchased, OnPurchaseItemFirstTime,
        OnManagerPurchased;
    public event Action OnTutorialStepCompleted, OnNewObjectPurchased;

    public GameObject GetCreationProgressButtonTarget(int index)
    {
        ItemController itemController = GetCreationItemController(index);
        return itemController != null ? itemController.ProgressButtonTarget : null;
    }

    public GameObject GetCreationBuyButtonTarget(int index)
    {
        ItemController itemController = GetCreationItemController(index);
        return itemController != null ? itemController.BuyButtonTarget : null;
    }

    public GameObject GetManagerButtonTarget(int index)
    {
        UIManagerController managerController = GetManagerController(index);
        return managerController != null ? managerController.ManagerButtonTarget : null;
    }

    public GameObject GetManagerBuyButtonTarget(int index)
    {
        UIManagerController managerController = GetManagerController(index);
        return managerController != null ? managerController.ManagerBuyButtonTarget : null;
    }

    /// <summary>Tutorial helper: returns the coin balance panel GameObject (may be null).</summary>
    public GameObject GetCoinsBalanceTarget() => _coins != null ? _coins.gameObject : null;

    /// <summary>
    /// Tutorial helper: plays a brief pop animation on the coin balance panel.
    /// Safe no-op when no coin panel is assigned. Uses the existing DOTween setup.
    /// </summary>
    public void PlayCoinsEarnedFeedback()
    {
        if (_coins == null)
            return;

        Transform coinsTransform = _coins.transform;
        coinsTransform.DOKill(false);
        coinsTransform.DOPunchScale(Vector3.one * 0.18f, 0.3f, 8, 0.6f);
    }

    /// <summary>Tutorial helper: forces the Creation progress (earn) button interactable state.</summary>
    public void SetCreationProgressButtonEnabled(int index, bool value)
    {
        ItemController itemController = GetCreationItemController(index);

        if (itemController != null)
            itemController.ToggleActivation(value);
    }

    /// <summary>Tutorial helper: forces the Creation buy button interactable state.</summary>
    public void SetCreationBuyButtonInteractable(int index, bool value)
    {
        ItemController itemController = GetCreationItemController(index);

        if (itemController != null)
            itemController.ToggleBuyButton(value);
    }

    /// <summary>Tutorial helper: forces the manager buy button interactable state.</summary>
    public void SetManagerBuyButtonInteractable(int index, bool value)
    {
        UIManagerController managerController = GetManagerController(index);

        if (managerController != null)
            managerController.ToggleButton(index, value);
    }

    public void PrepareCreationUI(List<ItemData> data)
    {
        _uiCreationItemsList.Clear();
        _managerControllers.Clear();

        for (int i = 0; i < data.Count; i++)
        {
            ItemController itemController = Instantiate(_uiItemPrefab, _uiItemParent).GetComponent<ItemController>();

            _uiCreationItemsList.Add(itemController);
            itemController.Prepare(data[i].ItemImage, data[i].CurrencyImage, data[i].IsPremium, data[i].TranslationText, data[i].MaxCountIncrement);

            UIManagerController _managerController = itemController.GetComponent<UIManagerController>();
            _managerControllers.Add(_managerController);
            _managerControllers[i].AddButton(i, data[i].ManagerPrice, data[i].CurrencyImage);

            _objectActivator[i].itemController = itemController;

            ConnectEvents(i, itemController);

            float _scrollItemGroupHeight = 165 * data.Count;
            _uiItemParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

            _managerControllers[i].OnManagerPurchased += PurchaseManager;
        }

        OnBuyButonClicked += ActivateNextCreationObject;
        OnPurchaseItemFirstTime += ActivateNewCreationObject;
    }

    public void ActivatePurchasedCreationObject(List<int> itemCount)
    {
        for (int i = 0; i < itemCount.Count; i++)
        {
            _objectActivator[i].ActivatePurchasedObject(itemCount[i]);
        }
    }

    public void ActivateNextCreationObject(int i)
    {
        _objectActivator[i].ActivateNextObject();

        if (PlayerPrefs.GetInt("TutorialStep") == 2)
        {
            OnNewObjectPurchased.Invoke();
            StartCoroutine(InvokeAction());
            return;
        }
        FirebaseAnalytics.LogEvent(name: "creation_object_purchased");
    }

    public void ActivateNewCreationObject(int i)
    {
        _objectActivator[i].ActivateNextObject();
    }

    private IEnumerator InvokeAction()
    {        
        yield return new WaitForSeconds(3.25f);
        OnTutorialStepCompleted.Invoke();         
    }

    public void PrepareUpgradeUI(List<UpgradeItemData> data)
    {
        _uiUpgradeItemsList.Clear();
        for (int i = 0; i < data.Count; i++)
        {
            UpgradeItemController upgradeItemController = Instantiate(_upgradeItemPrefab, _upgradeItemParent).GetComponent<UpgradeItemController>();
            upgradeItemController.Prepare(data[i].ItemImage, data[i].TranslationText);
            _uiUpgradeItemsList.Add(upgradeItemController);

            _upgradeObjectActivator[i].upgradeItemController = upgradeItemController;

            ConnectEvents(i, upgradeItemController);

            float _scrollItemGroupHeight = 165 * (data.Count + 1);
            _upgradeItemParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);
        }

        OnUpgradeItemPurchased += ActivateNextUpgradeObject;
    }

    public void ActivatePurchasedUpgradeObject(List<int> itemCount)
    {
        for (int i = 0; i < itemCount.Count; i++)
        {
            _upgradeObjectActivator[i].ActivatePurchasedObject(itemCount[i]);
        }
    }

    public void ActivateNextUpgradeObject(int i)
    {
        _upgradeObjectActivator[i].ActivateNextObject();
    }

    private void PurchaseManager(int index)
    {
        mmfPlayer.PlayFeedbacks();
        OnManagerPurchased?.Invoke(index);
    }

    private void ConnectEvents(int i, ItemController itemController)
    {
        itemController.OnProgressButtonClicked += () => OnProgressButtonClicked?.Invoke(i);
        itemController.OnWorkFinished += () => OnWorkFinished?.Invoke(i);
        itemController.OnActivationPremium += () => OnActivationPremium?.Invoke(i);
        itemController.OnBuyButtonClicked += () => OnBuyButonClicked?.Invoke(i);
        itemController.OnFirstActivation += () => OnPurchaseItemFirstTime?.Invoke(i);
    }

    private void ConnectEvents(int i, UpgradeItemController upgradeItemController)
    {
        upgradeItemController.OnUpgradeItemBuyButtonClicked += () => OnUpgradeItemPurchased?.Invoke(i);
    }

    public void UpdateManagerAvailability(int index, bool val)
    {
        _managerControllers[index].ToggleButton(index, val);
    }

    public void StartWorkOnItem(int index, float delay)
    {
        _uiCreationItemsList[index].StartWork(delay);
    }

    public void ToggleItemActiveState(int index, bool val)
    {
        _uiCreationItemsList[index].ToggleActivation(val);
    }

    public void UpdateUI(int index, GameData gameData, GeneralGameData generalData)
    {
        _managerControllers[index].SetButtonPurchased(index, gameData.Managers[index]);

        if (gameData.ItemDataList[index].IsPremium)
        {
            _uiCreationItemsList[index].SetIncomePerSec(gameData.ItemDataList[index].DiamondsIncomePerHour(gameData.ItemCount[index]), " / hour");
            _diamonds.SetDiamondsScore(generalData.Diamonds);
        }
        else
        {
            if (gameData.Managers[index])
                _uiCreationItemsList[index].SetIncomePerSec(gameData.ItemDataList[index].ItemIncomePerSec(gameData.ItemCount[index], gameData.ItemBonusMultiplayer[index]), " / sec");
            else
                _uiCreationItemsList[index].SetIncome(gameData.ItemDataList[index].ItemIncome(gameData.ItemCount[index], gameData.ItemBonusMultiplayer[index]));

            _coins.SetScore(gameData.Money);
            _diamonds.SetDiamondsScore(generalData.Diamonds);
        }

        if (gameData.BoosterMultiplier > 0)
            gameData.ItemDataList[index].BoosterMultiplier = gameData.BoosterMultiplier;

        _uiCreationItemsList[index].SetBuyPrice(gameData.ItemDataList[index].ItemUpgradePrice(gameData.ItemCount[index]));
        _uiCreationItemsList[index].SetItemCount(gameData.ItemCount[index], gameData.ItemDataList[index].MaxCount(gameData.ItemBonusMultiplayer[index], gameData.ItemMaxCountHelper[index]));
        _uiCreationItemsList[index].ToggleBuyButton(gameData.Money >= gameData.ItemDataList[index].ItemUpgradePrice(gameData.ItemCount[index]));
        _uiCreationItemsList[index].DisableBuyPanel(gameData.ItemCount[index] > 0 && gameData.ItemCount[index] < gameData.ItemDataList[index].MaxCountIncrement);
    }

    public void UpdateUpgradeUI(int index, GameData gameData, GeneralGameData generalData)
    {
        _uiUpgradeItemsList[index].SetBuyPrice(gameData.UpgradeItemDataList[index].ItemCost);
        _uiUpgradeItemsList[index].ToggleBuyButton(generalData.Diamonds >= gameData.UpgradeItemDataList[index].ItemCost);
        _uiUpgradeItemsList[index].DisableBuyPanel(gameData.UpgradeItemCount[index] < gameData.UpgradeItemDataList[index].MaxCountIncrement);
        _diamonds.SetDiamondsScore(generalData.Diamonds);
    }

    public void UpdateUILanguage()
    {
        for (int i = 0; i < _uiCreationItemsList.Count; i++)
        {
            _uiCreationItemsList[i].UpdateLanguage();
        }

        for (int i = 0; i < _uiUpgradeItemsList.Count; i++)
        {
            _uiUpgradeItemsList[i].UpdateLanguage();
        }
    }

    internal void ActivateItem(int index)
    {
        _uiCreationItemsList[index].ActivateButton();
    }

    public void PlanetObjectsToggle()
    {
        foreach (var obj in _objectActivator)
        {
            obj.PlanetObjectsToggle();
        }

        foreach (var obj in _upgradeObjectActivator)
        {
            obj.PlanetObjectsToggle();
        }
    }

    private ItemController GetCreationItemController(int index)
    {
        if (index < 0 || index >= _uiCreationItemsList.Count)
            return null;

        return _uiCreationItemsList[index];
    }

    private UIManagerController GetManagerController(int index)
    {
        if (index < 0 || index >= _managerControllers.Count)
            return null;

        return _managerControllers[index];
    }

    //public void TreesToggle()
    //{
    //    foreach (var obj in _trees)
    //    {
    //        obj.SetActive(!isTreesDisplayed);
    //    }

    //    isTreesDisplayed = !isTreesDisplayed;
    //}

    //public void ExtraObjsToggle()
    //{
    //    foreach (var obj in _extraObjs)
    //    {
    //        obj.SetActive(!isExtraObjsDisplayed);
    //    }

    //    isExtraObjsDisplayed = !isExtraObjsDisplayed;
    //}

    //public void DecorationsToggle()
    //{
    //    _decorations.SetActive(!isDecorationsDisplayed);
    //    isDecorationsDisplayed = !isDecorationsDisplayed;
    //}

    //public void BuildingsToggle()
    //{
    //    _buildings.SetActive(!isBuildingsDisplayed);
    //    isBuildingsDisplayed = !isBuildingsDisplayed;
    //}

    //public void HumansToggle()
    //{
    //    _humans.SetActive(!isHumansDisplayed);
    //    isHumansDisplayed = !isHumansDisplayed;
    //}

    //public void AnimalsToggle()
    //{
    //    _animals.SetActive(!isAnimalsDisplayed);
    //    isAnimalsDisplayed = !isAnimalsDisplayed;
    //}
}

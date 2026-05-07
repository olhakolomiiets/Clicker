using Firebase.Analytics;
using System;
using System.Collections.Generic;
using UnityEngine;

public class MetaPlanetManager : MonoBehaviour
{
    [SerializeField] private UpgradePanelUI _upgradeUI;
    GeneralGameData _generalGameData;
    [SerializeField] private MetaGameRules _metaGameRules;

    [Header("Score Panel")]
    [SerializeField] private ScorePanel _diamonds;
    [SerializeField] private GameObject _objectPrefab;

    [Header("Variant Planet Objects")]
    [SerializeField] private RectTransform _variantObjectParent;
    [SerializeField] private List<MetaVariantsController> _variants;
    [SerializeField] private List<MetaVariantItemData> _variantItemDataList;
    private List<MetaObjectController> _variantObjectsList = new();

    [Header("Upgrade Planet Objects")]
    [SerializeField] private RectTransform _upgradeObjectParent;
    [SerializeField] private List<MetaUpgradeItemController> _upgradeControllerList;
    [SerializeField] private List<MetaUpgradeItemData> _upgradeItemDataList;
    private List<MetaObjectController> _upgradeObjectsList = new();

    public event Action<int> OnVariantBuyButonClicked, OnUpgradeBuyButonClicked, OnVariantObjectAddButtonClicked, OnUpgradeObjectAddButtonClicked;
    public event Action OnVariantOpened, OnUpgradeOpened;


    public void Initialize(GeneralGameData generalData)
    {
        _generalGameData = generalData;

        PrepareVariantUI();
        PrepareUpgradeUI();
        ConnectRulesToUI();

        UpdateUI(_generalGameData, null);
    }

    public void PrepareData(GeneralGameData generalData)
    {
        _generalGameData = generalData;
    }

    public void PrepareGameData(GeneralGameData generalGameData, GameData gameData)
    {
        _generalGameData = generalGameData;

        foreach (var controller in _variants)
            controller.PrepareData(_generalGameData);
    }

    private void ConnectRulesToUI()
    {
        _metaGameRules.OnUpdateGameData += UpdateUI;

        foreach (var controller in _variants)
            controller.OnVariantBuyButonClicked += _metaGameRules.HandleVariantItem;
    }

    public void PrepareVariantUI()
    {
        _variantObjectsList.Clear();

        for (int i = 0; i < _variantItemDataList.Count; i++)
        {
            MetaObjectController itemController = Instantiate(_objectPrefab, _variantObjectParent).GetComponent<MetaObjectController>();
            _variantObjectsList.Add(itemController);
            itemController.PrepareVariantObject(_variantItemDataList[i].Icon, _variantItemDataList[i].ItemName);

            _variants[i].SetMetaObjectController(itemController);

            itemController.OnObjectAddButtonClicked += UpdatePanelUI;
        }

        float _scrollItemGroupHeight = 165 * _variantItemDataList.Count;
        _variantObjectParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

        UpdatePanelUI();
    }

    public void PrepareUpgradeUI()
    {
        _upgradeObjectsList.Clear();

        for (int i = 0; i < _upgradeItemDataList.Count; i++)
        {
            MetaObjectController itemController = Instantiate(_objectPrefab, _upgradeObjectParent).GetComponent<MetaObjectController>();
            _upgradeObjectsList.Add(itemController);
            itemController.PrepareUpgradeObject(_upgradeItemDataList[i].Icon, _upgradeItemDataList[i].ItemName);

            _upgradeControllerList[i].SetMetaObjectController(itemController);

            itemController.OnObjectAddButtonClicked += UpdatePanelUI;
        }

        float _scrollItemGroupHeight = 165 * _upgradeItemDataList.Count;
        _upgradeObjectParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

        OnUpgradeObjectAddButtonClicked += ActivateNewUpgradeObject;
    }

    private void ConnectEvents(int i, MetaObjectController itemController)
    {
        itemController.OnObjectAddButtonClicked += () => OnVariantObjectAddButtonClicked?.Invoke(i);
    }

    public void ActivateNewVariantObject(int i)
    {
        _variants[i].ActivateNextObject();
    }

    public void ActivateNewUpgradeObject(int i)
    {
        //_upgradeObjectActivator[i].ActivateNextObject();
    }

    public void UpdateUI(GeneralGameData data, GameData gameData)
    {
        _diamonds.SetDiamondsScore(data.Diamonds);

        foreach (MetaVariantsController controller in _variants)
        {
            controller.PrepareData(data);
            controller.MetaObjectUI();
        }

        for (int i = 0; i < _variantObjectsList.Count; i++)
        {
            _variantObjectsList[i].SetItemCount(_variants[i].ItemCount, _variantItemDataList[i].Quantity);

            _variantObjectsList[i].DisableBuyPanel(_variants[i].ItemCount >= 0 && _variants[i].ItemCount < _variantItemDataList[i].Quantity);
        }
    }

    public void UpdatePanelUI()
    {
        for (int i = 0; i < _variantObjectsList.Count; i++)
        {
            _variantObjectsList[i].SetItemCount(_variants[i].ItemCount, _variantItemDataList[i].Quantity);

            _variantObjectsList[i].DisableBuyPanel(_variants[i].ItemCount >= 0 && _variants[i].ItemCount < _variantItemDataList[i].Quantity);
        }

        for (int i = 0; i < _upgradeObjectsList.Count; i++)
        {
            _upgradeObjectsList[i].SetItemCount(_variants[i].ItemCount, _variantItemDataList[i].Quantity);

            _upgradeObjectsList[i].DisableBuyPanel(_variants[i].ItemCount >= 0 && _variants[i].ItemCount < _variantItemDataList[i].Quantity);
        }
    }


}
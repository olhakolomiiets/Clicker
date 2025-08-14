using Firebase.Analytics;
using MoreMountains.Feedbacks;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MetaPlanetUI : MonoBehaviour
{
    [Header("Score Panel")]
    [SerializeField] private ScorePanel _diamonds;

    [Header("Variant Planet Objects")]
    [SerializeField] private GameObject _variantObjectPrefab;
    [SerializeField] private RectTransform _variantObjectParent;
    [SerializeField] private List<MetaVariantsController> _variants = new();
    public List<MetaObjectController> _variantObjectsList = new();

    [Header("Upgrade Planet Objects")]
    [SerializeField] private GameObject _upgradeObjectPrefab;
    [SerializeField] private RectTransform _upgradeObjectParent;
    [SerializeField] private List<MetaObjectActivator> _upgradeObjectActivator = new();
    public List<MetaObjectController> _upgradeObjectsList = new();

    [Header("Upgrade UI")]
    [SerializeField] private UpgradePanelUI _upgradePanel;
    [SerializeField] private List<MetaUpgradeItemController> _upgradeControllerList;
    [SerializeField] private MetaUpgradeItemController _upgrade;
    public List<UpgradeLevel> upgradeLevels = new();
    public List<MetaUpgradeItemController> _upgradeList = new();

    public event Action<int> OnVariantBuyButonClicked, OnUpgradeBuyButonClicked, OnVariantObjectAddButtonClicked, OnUpgradeObjectAddButtonClicked;
    public event Action OnVariantOpened, OnUpgradeOpened;

    public void PrepareVariantUI(List<MetaVariantItemData> variantData)
    {
        _variantObjectsList.Clear();

        for (int i = 0; i < variantData.Count; i++)
        {
            MetaObjectController itemController = Instantiate(_variantObjectPrefab, _variantObjectParent).GetComponent<MetaObjectController>();
            _variantObjectsList.Add(itemController);
            itemController.PrepareVariantObject(variantData[i].Icon, variantData[i].ItemName);

            _variants[i].itemController = itemController;

            ConnectEvents(i, itemController);
        }

        float _scrollItemGroupHeight = 165 * variantData.Count;
        _variantObjectParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

        OnVariantObjectAddButtonClicked += ActivateNewVariantObject;
    }

    public void PrepareUpgradeUI(List<MetaUpgradeItemData> upgradeData)
    {
        _upgradeObjectsList.Clear();

        for (int i = 0; i < upgradeData.Count; i++)
        {
            MetaObjectController itemController = Instantiate(_upgradeObjectPrefab, _upgradeObjectParent).GetComponent<MetaObjectController>();
            _upgradeObjectsList.Add(itemController);
            itemController.PrepareUpgradeObject(upgradeData[i].Icon, upgradeData[i].ItemName);

            _upgradeObjectActivator[i].itemController = itemController;

            //ConnectEvents(index, itemController);
        }

        float _scrollItemGroupHeight = 165 * upgradeData.Count;
        _upgradeObjectParent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _scrollItemGroupHeight);

        OnUpgradeObjectAddButtonClicked += ActivateNewUpgradeObject;
}


    public void ActivateNewVariantObject(int i)
    {
        _variants[i].ActivateNextObject();
    }

    public void ActivateNewUpgradeObject(int i)
    {
        _upgradeObjectActivator[i].ActivateNextObject();
    }

    public void OpenUpgradePanel(MetaUpgradeItemController item)
    {
        _upgrade = item;

        //ConnectUpgradeEvents(_upgrade.PanelUI);
    }

    public void UpdateUI(int index, GeneralGameData data)
    {
        _diamonds.SetDiamondsScore(data.Diamonds);

    }

    // private void ConnectVariantEvents(int i, VariantButtonUI variantButton)
    // {
    //     variantButton.OnBuyButtonClicked += () => OnVariantBuyButonClicked?.Invoke(i);
    // }

    // private void ConnectUpgradeEvents(UpgradePanelUI upgradePanel)
    // {
    //     upgradePanel.OnBuyButtonClicked += () => OnUpgradeBuyButonClicked?.Invoke();
    // }

    private void ConnectEvents(int i, MetaObjectController itemController)
    {
        itemController.OnObjectAddButtonClicked += () => OnVariantObjectAddButtonClicked?.Invoke(i);
    }
    
}

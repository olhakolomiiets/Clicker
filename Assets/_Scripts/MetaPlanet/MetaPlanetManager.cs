using System.Collections.Generic;
using UnityEngine;

public class MetaPlanetManager : MonoBehaviour
{
    [SerializeField] private VariantButtonUI _variantUI;
    [SerializeField] private UpgradePanelUI _upgradeUI;
    [SerializeField] private MetaPlanetUI planetUI;
    GeneralGameData _generalGameData;
    [SerializeField] private MetaPlanetRules _metaPlanetRules;
    [SerializeField] private SaveSystem _saveSystem;

    [Space(10)]
    [SerializeField] private List<MetaVariantsController> _variantsControllerList;
    [SerializeField] private List<MetaVariantItemData> _variantItemDataList;

    [SerializeField] private List<int> _variantItemCount = new();
    [SerializeField] private List<MetaVariantItemController> _variantItemsList = new();
    [SerializeField] private List<int> _variantIndex = new();

    [Space(10)]
    [SerializeField] private List<MetaUpgradeItemData> _upgradeItemDataList;
    [SerializeField] private List<int> _upgradeItemCount = new();
    [SerializeField] private List<int> _upgradeLevel = new();

    private bool isGameSaved = true;

    private void OnEnable()
    {
        PrepareGameData();
        ConnectRulesToUI();

        _metaPlanetRules.PrepareData(_generalGameData); 
    }

    private void Start()
    {
        if (isGameSaved)
        {
            LoadGeneralGameData();
        }

        // _gameUI.ActivatePurchasedCreationObject(_creationItemsCount);
        // _gameUI.ActivatePurchasedUpgradeObject(_upgradeItemCount);
    }

    private void PrepareGameData()
    {
        _generalGameData = new();

        _generalGameData.VariantsControllerList = _variantsControllerList;

        _generalGameData.VariantItemDataList = _variantItemDataList;

        _variantItemCount = _generalGameData.VariantItemCount;
        _variantIndex = _generalGameData.VariantIndex;

        _generalGameData.UpgradeItemDataList = _upgradeItemDataList;
        _upgradeItemCount = _generalGameData.UpgradeItemCount;
        _upgradeLevel = _generalGameData.UpgradeLevel;


        // if (_variantItemCount != null && _variantItemCount.Count != 0)
        // {
        //     for (int i = 0; i < _variantsControllerList.Count; i++)
        //     {
        //         int itemsCount = _variantsControllerList[i].variantControllerList.Count;
        //         _variantItemCount.Add(itemsCount);
        //     }
        // }

        planetUI.PrepareVariantUI(_variantItemDataList);
        planetUI.PrepareUpgradeUI(_upgradeItemDataList);
    }

    private void ConnectRulesToUI()
    {
        _metaPlanetRules.OnUpdateVariantData += planetUI.UpdateUI;
        _metaPlanetRules.OnUpdateUpgradeData += planetUI.UpdateUI;

        planetUI.OnVariantObjectAddButtonClicked += _metaPlanetRules.AddVariantObject;
        planetUI.OnUpgradeObjectAddButtonClicked += _metaPlanetRules.AddUpgradeObject;

        _metaPlanetRules.OnActivateVariantItem += planetUI.ActivateNewVariantObject;

        // _metaPlanetRules.OnActivateUpgradeItem += _upgradeUI.UpdateState;

        planetUI.OnVariantBuyButonClicked += _metaPlanetRules.HandleVariantItem;

        planetUI.OnVariantOpened += _metaPlanetRules.SendItemsData;

        _metaPlanetRules.OnToggleVariantItem += _variantUI.UpdateState;
        _metaPlanetRules.OnToggleUpgradeItem += _upgradeUI.UpdateState;
    }

    public void SaveGeneralGameData()
    {
        List<string> dataToSave = new()
        {
            _generalGameData.GetSaveData()
        };
        _saveSystem.SaveTheGame(dataToSave);
    }

    public void LoadGeneralGameData()
    {
        List<string> data = _saveSystem.LoadGame();
        if (data.Count > 0)
        {
            _metaPlanetRules.LoadMetaPlanet(data[0]);
        }
    }

        private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGeneralGameData();
        }
        else
        {
            if (isGameSaved)
            {
                LoadGeneralGameData();
            }
        }
    }

    private void OnDisable()
    {
        if (!isGameSaved)
        {
            SaveGeneralGameData();
        }
    }

    private void OnDestroy()
    {
        if (!isGameSaved)
        {
            SaveGeneralGameData();
        }
    }
    
}

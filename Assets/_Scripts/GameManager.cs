using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Connects Game systems and drives the game.
/// </summary>
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameUI _gameUI;
    private GameData _gameData;
    GeneralGameData _generalGameData;
    [SerializeField] private GameRules _gameRules;
    [SerializeField] private SaveSystem _saveSystem;
    //[SerializeField] private VisualsController _visualsController;

    [Space(10)]
    [SerializeField] private List<ItemData> _creationItemsDataList;
    [SerializeField] private List<int> _creationItemsCount = new();

    [Space(10)]
    [SerializeField] private List<UpgradeItemData> _upgradeItemsDataList;
    [SerializeField] private List<int> _upgradeItemCount = new();

    [Space(10)]
    [SerializeField] private RewardTimers _rewardTimer;
    [SerializeField] private RewardsManager _rewardsManager;

    [Space(10)]
    [SerializeField] private PurchaseManager _purchaseManager;
    [SerializeField] private PassiveIncome _passiveIncome;

    [Space(10)]
    [SerializeField] private LevelController _levelController;

    [Space(10)]
    [SerializeField] private UIController _uiController;
    [SerializeField] private TutorialManager _tutorialManager;

    private bool isGameSaved = true;

    /// <summary>
    /// All the setup happens here
    /// </summary>
    private void OnEnable()
    {
        _rewardTimer.OnActivatedCoinsRewardButton.AddListener(ActivatedRewardButton);

        PrepareGameData();
        PrepareUI();
        ConnectGameRulesToUI();
        ConnectGameRulesToRewards();
        ConnectGameTips();

        if (PlayerPrefs.GetInt("TutorialCompleted") == 0)
            ConnectTutorialManager();

        _gameRules.PrepareGameData(_gameData, _generalGameData);

        //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!   _visualsController.InitializeVisual(_gameData);   
    }

    private void Start()
    {
        if (isGameSaved)
        {
            LoadGeneralGameData();
            LoadSavedData();
        }

        _creationItemsCount = _gameData.ItemCount;
        _upgradeItemCount = _gameData.UpgradeItemCount;
        _gameUI.ActivatePurchasedCreationObject(_creationItemsCount);
        _gameUI.ActivatePurchasedUpgradeObject(_upgradeItemCount);
    }

    private void ActivatedRewardButton()
    {
        _rewardsManager.PrepareRewardData((float)_gameData.MoneyPerSec);
    }

    #region CONNECT METHODS
    /// <summary>
    /// Connecting Game Rule events to Ui so we can click buttons, progress the game and get a visual response in the UI and
    /// a Visualization
    /// </summary>
    private void ConnectGameRulesToUI()
    {
        _gameRules.OnModifyManagerAvailability += _gameUI.UpdateManagerAvailability;
        _gameRules.OnActivateItem += _gameUI.ActivateItem;

        _gameRules.OnStartWorkOnItem += _gameUI.StartWorkOnItem;

        _gameRules.OnToggleItemActivationState += _gameUI.ToggleItemActiveState;

        _gameRules.OnUpdateData += _gameUI.UpdateUI;
        _gameRules.OnUpdateUpgradeData += _gameUI.UpdateUpgradeUI;

        //_gameRules.OnUpdateData += _visualsController.UpdateVisuals;
        //_gameRules.OnPerformAction += _visualsController.PerformAction;
    }

    private void ConnectGameRulesToRewards()
    {
        _gameRules.OnUpdateGameData += _passiveIncome.PrepareGameData;
        _gameRules.OnUpdateGameData += _levelController.PrepareGameData;

        _gameRules.OnActivatePassiveIncome += _passiveIncome.ActivatePassiveIncome;

        _passiveIncome.OnEarningPassiveIncome += _gameRules.GetPassiveIncome;
        _passiveIncome.OnGetPassiveIncomeExtraTime += _gameRules.UpdatePassiveIncomeTime;

        _rewardsManager.OnEarningReward += _gameRules.GetReward;

        _rewardTimer.OnSetBoosterTimer += _gameRules.GetCoinsBooster;
        _rewardTimer.OnEndBoosterTimer += _gameRules.DisableCoinsBooster;

        _purchaseManager.OnPurchasingPack += _gameRules.GetPurchasedProduct;
        _purchaseManager.OnPurchasingDiamonds += _gameRules.GetPurchasedProduct;
        _purchaseManager.OnPurchasingBooster += _gameRules.GetPurchasedBooster;
    }

    private void ConnectTutorialManager()
    {
        _uiController.OnTutorialStepCompleted += _tutorialManager.AdvanceTutorial;
        _uiController.OnTutorialNonCompleted += _tutorialManager.ShowFirstStep;
        _uiController.OnLoadTutorialNextStep += _tutorialManager.ShowNextTutorial;

        _gameRules.OnTutorialStepCompleted += _tutorialManager.AdvanceTutorial;        
        _gameRules.OnTutorialStepReady += _tutorialManager.ShowNextTutorial;
        _gameRules.OnTutorialStepReady += _uiController.ShowTutorialHint;

        _gameUI.OnTutorialStepCompleted += _tutorialManager.AdvanceTutorial;
    }

    private void ConnectGameTips()
    {
        _gameUI.OnItemReadyToBuy += _tutorialManager.ShowGameTip;
        _gameRules.OnNewObjectPurchased += _tutorialManager.HideGameTip;
        _gameUI.OnItemNotReadyToBuy += _tutorialManager.HideGameTip;

        _uiController.OnStorePanelDisplayed += _tutorialManager.ResetPointer;
        _uiController.OnStorePanelNotDisplayed += _tutorialManager.ShowFirstStep;
    }
    #endregion

    #region PREPARE METHODS

    /// <summary>
    /// GameData stores the state of our game
    /// </summary>
    private void PrepareGameData()
    {
        _generalGameData = new();
        _gameData = new();
        _gameData.ItemDataList = _creationItemsDataList;
        _gameData.UpgradeItemDataList = _upgradeItemsDataList;
    }

    /// <summary>
    /// UI is separate from the visual aprt. We could have just UI buttons and progress bar with no visuals.
    /// </summary>
    private void PrepareUI()
    {
        _gameUI.PrepareCreationUI(_creationItemsDataList);
        _gameUI.PrepareUpgradeUI(_upgradeItemsDataList);

        _gameUI.OnProgressButtonClicked += _gameRules.HandleStartItemProgress;

        _gameUI.OnWorkFinished += _gameRules.IncreaseScore;

        _gameUI.OnWorkFinished += _gameRules.HandleManager;    

        _gameUI.OnUpgradeItemPurchased += _gameRules.HandleDiamondsUpgrade;

        _gameUI.OnBuyButonClicked += _gameRules.HandleUpgrade;

        _gameUI.OnActivationPremium += _gameRules.HandlePremiumManager;

        _gameUI.OnPurchaseItemFirstTime += _gameRules.PurchaseItemFirstTime;
        _gameUI.OnManagerPurchased += _gameRules.HandleManagerPurchased;
    }
    #endregion

    #region SAVE & LOAD
    /// <summary>
    /// I have decided that GameManager will know what objects needs to save and load theire data.
    /// SaveSystem just does the Saving work
    /// </summary>
    public void SaveGame()
    {
        List<string> dataToSave = new()
        {
            _gameData.GetSaveData(),
            //_visualsController.GetSaveData()
        };
        _saveSystem.SaveThePlanet(dataToSave);

        isGameSaved = true;
    }

    /// <summary>
    /// I have decided that GameManager will know what objects needs to save and load theire data.
    /// SaveSystem just does the Saving work
    /// </summary>
    public void LoadSavedData()
    {
        List<string> data = _saveSystem.LoadPlanet();
        if (data.Count > 0)
        {
            _gameRules.LoadPlanet(data[0]);
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! _visualsController.LoadData(data[1]);
        }

        isGameSaved = false;
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
            _gameRules.LoadGame(data[0]);
        }
    }

    /// <summary>
    /// Removes the loaded data
    /// </summary>
    public void ResetGame()
    {
        _saveSystem.ResetData();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    #endregion

    private void OnApplicationFocus(bool focusStatus)
    {
        if (focusStatus)
        {
            if (isGameSaved)
            {
                LoadGeneralGameData();
                LoadSavedData();
            }
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGame();
            SaveGeneralGameData();
        }
        else
        {
            if (isGameSaved)
            {
                LoadGeneralGameData();
                LoadSavedData();
            }
        }
    }

    // private void OnApplicationQuit()
    // {
    //     if (!isGameSaved)
    //     {
    //         SaveGame();
    //         SaveGeneralGameData();
    //         Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! GameManager /// OnApplicationQuit /// SaveGame");
    //     }
    // }

    private void OnDisable()
    {
        if (!isGameSaved)
        {
            SaveGame();
            SaveGeneralGameData();
        }
        _rewardTimer.OnActivatedCoinsRewardButton.RemoveListener(ActivatedRewardButton);
    }

    private void OnDestroy()
    {
        if (!isGameSaved)
        {
            SaveGame();
            SaveGeneralGameData();
        }
    }
}

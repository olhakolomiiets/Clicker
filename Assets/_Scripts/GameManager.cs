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
    [SerializeField] private GameRules _gameRules;
    [SerializeField] private SaveSystem _saveSystem;
    [SerializeField] private VisualsController _visualsController;

    [SerializeField] private List<ItemData> _creationItemsDataList;

    [SerializeField] private List<UpgradeItemData> _upgradeItemsDataList;

    [SerializeField] private RewardTimers _rewardTimer;
    [SerializeField] private CoinsReward _coinsReward;

    [SerializeField] private PurchaseManager _purchaseManager;

    /// <summary>
    /// All the setup happens here
    /// </summary>
    private void OnEnable()
    {
        _rewardTimer.OnActivatedCoinsRewardButton.AddListener(ActivatedRewardButton);
    }
    private void Start()
    {
        PrepareGameData();
        PrepareUI();
        ConnectGameRulesToUI();

        _gameRules.PrepareGameData(_gameData);
        _purchaseManager.PrepareGameData(_gameData);

        //Visuals (soldiers / shooting) are optional and depends on GameData
        //GameRules or game data does NOT depend on visuals.

        //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!   _visualsController.InitializeVisual(_gameData);

        //We load the saved game data if we have any
        LoadSavedData();    
    }

    private void ActivatedRewardButton()
    {
        _coinsReward.PrepareRewardData(_gameData);
    }

    /// <summary>
    /// Connecting Game Rule events to Ui so we can click buttons, progress the game and get a visual response in the UI and
    /// a Visualization
    /// </summary>
    private void ConnectGameRulesToUI()
    {
        _gameRules.OnModifyManagerAvailability += _gameUI.UpdateManagerAvailability;
        _gameRules.OnActivateItem += _gameUI.ActivateItem;

        _gameRules.OnStartWorkOnItem += _gameUI.StartWorkOnItem;
        _gameRules.OnStartWorkOnUpgradeItem += _gameUI.StartWorkOnUpgradeItem;

        _gameRules.OnToggleItemActivationState += _gameUI.ToggleItemActiveState;

        _gameRules.OnUpdateData += _gameUI.UpdateUI;
        _gameRules.OnUpdateUpgradeData += _gameUI.UpdateUpgradeUI;

        _gameRules.OnUpdateData += _visualsController.UpdateVisuals;
        _gameRules.OnPerformAction += _visualsController.PerformAction;
    }

    /// <summary>
    /// GameData stores the state of our game
    /// </summary>
    private void PrepareGameData()
    {
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

        _gameUI.OnUpgradeItemPurchased += _gameRules.HandleStartUpgradeItemProgress;
        _gameUI.OnUpdateWorkFinished += _gameRules.HandleUpgradeManager;
        _gameUI.OnUpgradeItemPurchased += _gameRules.HandleDiamondsUpgrade;

        _gameUI.OnBuyButonClicked += _gameRules.HandleUpgrade;
        _gameUI.OnActivationPremium += _gameRules.HandleUpgrade;

        _gameUI.OnPurchaseItemFirstTime += _gameRules.PurchaseItemFirstTime;
        _gameUI.OnManagerPurchased += _gameRules.HandleManagerPurchased;
    }

    /// <summary>
    /// I have decided that GameManager will know what objects needs to save and load theire data.
    /// SaveSystem just does the Saving work
    /// </summary>
    public void SaveGame()
    {
        List<string> dataToSave = new()
        {
            _gameData.GetSaveData(),
            _visualsController.GetSaveData()
        };
        _saveSystem.SaveTheGame(dataToSave);
    }

    /// <summary>
    /// I have decided that GameManager will know what objects needs to save and load theire data.
    /// SaveSystem just does the Saving work
    /// </summary>
    public void LoadSavedData()
    {
        List<string> data = _saveSystem.LoadGame();
        if (data.Count > 0)
        {
            _gameRules.LoadGame(data[0]);
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! _visualsController.LoadData(data[1]);
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


    private void OnDisable()
    {
        _rewardTimer.OnActivatedCoinsRewardButton.RemoveListener(ActivatedRewardButton);
    }

    private void OnApplicationQuit()
    {
        SaveGame();
    }
}

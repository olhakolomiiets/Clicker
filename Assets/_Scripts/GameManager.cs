using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private SaveSystem _saveSystem;

    [Header("Planet Mode")]
    [SerializeField] private MonoBehaviour _planetModeBehaviour;

    [Space(10)]
    [SerializeField] private PurchaseManager _purchaseManager;

    private IPlanetMode _planetMode;
    private GeneralGameData _generalGameData;

    private bool _isGameSaved = true;

    private void OnEnable()
    {
        _generalGameData = new GeneralGameData();

        _planetMode = _planetModeBehaviour as IPlanetMode;

        if (_planetMode == null)
        {
            Debug.LogError($"{nameof(_planetModeBehaviour)} must implement IPlanetMode");
            return;
        }

        LoadGeneralGameData();

        _planetMode.Initialize(_generalGameData);

        ConnectPurchaseManager();

        if (_isGameSaved)
            LoadSavedData();
    }

    private void ConnectPurchaseManager()
    {
        _purchaseManager.OnPurchasingPack += _planetMode.AddPurchasedPack;
        _purchaseManager.OnPurchasingDiamonds += _planetMode.AddPurchasedDiamonds;
        _purchaseManager.OnPurchasingBooster += _planetMode.ActivatePurchasedBooster;
    }

    public void SaveGame()
    {
        if (_planetMode == null)
            return;

        List<string> dataToSave = new()
        {
            _planetMode.Save()
        };

        _saveSystem.SaveThePlanet(dataToSave);

        _isGameSaved = true;
    }

    public void LoadSavedData()
    {
        if (_planetMode == null)
            return;

        List<string> data = _saveSystem.LoadPlanet();

        if (data.Count > 0)
            _planetMode.Load(data[0]);

        _isGameSaved = false;
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
            _generalGameData.SetData(data[0]);
    }

    public void ResetGame()
    {
        _saveSystem.ResetData();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveGame();
            SaveGeneralGameData();
            Debug.Log("GameManager /// OnApplicationPause /// Save");
        }
        else
        {
            if (_isGameSaved)
            {
                LoadGeneralGameData();
                LoadSavedData();
                Debug.Log("GameManager /// OnApplicationPause /// Load");
            }
        }
    }

    private void OnDisable()
    {
        if (_planetMode != null && _purchaseManager != null)
        {
            _purchaseManager.OnPurchasingPack -= _planetMode.AddPurchasedPack;
            _purchaseManager.OnPurchasingDiamonds -= _planetMode.AddPurchasedDiamonds;
            _purchaseManager.OnPurchasingBooster -= _planetMode.ActivatePurchasedBooster;
        }

        if (!_isGameSaved)
        {
            SaveGame();
            SaveGeneralGameData();
        }
    }

    private void OnDestroy()
    {
        if (!_isGameSaved)
        {
            SaveGame();
            SaveGeneralGameData();
        }
    }
}
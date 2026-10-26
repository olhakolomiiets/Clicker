using System;
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

    [Header("Meta Upgrade Effects")]
    [SerializeField]
    private List<MetaUpgradeItemData> _metaUpgradeConfigs = new();

    private IPlanetMode _planetMode;
    private GeneralGameData _generalGameData;

    private bool _isGameSaved = true;

    [SerializeField] private OfflinePlanetCatalog _offlinePlanetCatalog;
    private static string _observedOfflineExitTime;
    private static DateTime _observedOfflineReturnUtc;

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

        _generalGameData.MetaUpgradeEffects = new MetaUpgradeEffects();
        _generalGameData.MetaUpgradeEffects.Rebuild(_metaUpgradeConfigs);

        _planetMode.Initialize(_generalGameData);

        ConnectPurchaseManager();

        if (_isGameSaved)
        {
            LoadSavedData();

#if UNITY_EDITOR
            if (_planetMode is StandardPlanetMode)
            {
                var catalog =
                    UnityEditor.AssetDatabase.LoadAssetAtPath<OfflinePlanetCatalog>(
                        "Assets/_Scripts/PassiveIncome/OfflinePlanetCatalog.asset");

                if (catalog == null)
                {
                    Debug.LogError("[OfflineIncomeTest] Catalog not found");
                }
                else
                {
                    var entry = catalog.Planets.Find(
                        p => p.SceneBuildIndex == gameObject.scene.buildIndex);

                    if (entry == null)
                    {
                        Debug.LogError("[OfflineIncomeTest] Planet not found in catalog");
                    }
                    else
                    {
                        GameDataSave snapshot =
                            JsonUtility.FromJson<GameDataSave>(_planetMode.Save());

                        bool success = OfflinePlanetIncomeCalculator.TryCalculate(
                            entry,
                            snapshot,
                            _generalGameData.MetaUpgradeEffects,
                            out double calculated,
                            out string error);

                        if (!success)
                        {
                            Debug.LogError($"[OfflineIncomeTest] {error}");
                        }
                        else
                        {
                            double expected = snapshot.MoneyPerSec;

                            double tolerance = System.Math.Max(
                                1e-9,
                                System.Math.Abs(expected) * 1e-12);

                            bool matched =
                                System.Math.Abs(expected - calculated) <= tolerance;

                            Debug.Log(
                                $"[OfflineIncomeTest] Planet={entry.SaveKey} | " +
                                $"GameRules={expected:R} | " +
                                $"OfflineCalculator={calculated:R} | " +
                                $"Match={matched}"
                            );
                        }
                    }
                }
            }
#endif
        }

#if UNITY_EDITOR
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex == 0)
        {
            TestOfflinePlanetIncomes();
        }
#endif

        if (_planetMode is StandardPlanetMode standardMode)
        {
            standardMode.SetPassiveIncomeSaver(SavePassiveIncomeReward);
            standardMode.TryActivatePassiveIncome();
        }
    }

    private void ConnectPurchaseManager()
    {
        _purchaseManager.OnPurchasingPack += _planetMode.AddPurchasedPack;
        _purchaseManager.OnPurchasingDiamonds += _planetMode.AddPurchasedDiamonds;
        _purchaseManager.OnPurchasingBooster += _planetMode.ActivatePurchasedBooster;
    }

    [RuntimeInitializeOnLoadMethod(
    RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOfflineCaptureObservation()
    {
        _observedOfflineExitTime = null;
        _observedOfflineReturnUtc = DateTime.MinValue;
    }

    private DateTime ObserveOfflineReturnUtc()
    {
        string exitTime = _generalGameData.ExitTime;

        if (_observedOfflineReturnUtc == DateTime.MinValue ||
            _observedOfflineExitTime != exitTime)
        {
            _observedOfflineExitTime = exitTime;
            _observedOfflineReturnUtc = DateTime.UtcNow;
        }

        return _observedOfflineReturnUtc;
    }

    private bool CaptureOfflinePlanetRewards(DateTime utcNow)
    {
        if (_offlinePlanetCatalog == null ||
            _offlinePlanetCatalog.Planets == null ||
            _offlinePlanetCatalog.Planets.Count == 0 ||
            _offlinePlanetCatalog.MetaUpgradeConfigs == null ||
            _offlinePlanetCatalog.MetaUpgradeConfigs.Count == 0)
        {
            Debug.LogError(
                "[OfflinePlanetRewards] Missing planet catalog configuration.");
            return false;
        }

        // Работаем с копией, чтобы при ошибке не сохранять
        // частично рассчитанные награды.
        var working = new GeneralGameData();
        working.SetData(_generalGameData.GetSaveData());

        bool modeChanged = !working.OfflinePlanetRewardsEnabled;
        working.OfflinePlanetRewardsEnabled = true;

        bool captured = working.CaptureOfflineTime(
            utcNow,
            out int newOfflineSeconds);

        if (!captured && !modeChanged)
            return false;

        if (captured && newOfflineSeconds > 0)
        {
            var effects = new MetaUpgradeEffects();
            effects.Rebuild(_offlinePlanetCatalog.MetaUpgradeConfigs);

            var catalogKeys = new HashSet<string>();
            var rewardKeys = new HashSet<string>();

            foreach (var existing in working.OfflinePlanetRewards)
            {
                if (existing == null ||
                    string.IsNullOrWhiteSpace(existing.PlanetKey) ||
                    string.IsNullOrWhiteSpace(existing.RewardId) ||
                    existing.CreditedSeconds < 0 ||
                    existing.MoneyX1 < 0d ||
                    double.IsNaN(existing.MoneyX1) ||
                    double.IsInfinity(existing.MoneyX1) ||
                    !rewardKeys.Add(existing.PlanetKey))
                {
                    Debug.LogError(
                        "[OfflinePlanetRewards] Invalid saved reward.");
                    return false;
                }
            }

            foreach (OfflinePlanetEntry entry in _offlinePlanetCatalog.Planets)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.SaveKey) ||
                    entry.SceneBuildIndex <= 0 ||
                    !catalogKeys.Add(entry.SaveKey))
                {
                    Debug.LogError(
                        "[OfflinePlanetRewards] Invalid planet catalog entry.");
                    return false;
                }

                bool isUnlocked =
                    entry.SceneBuildIndex == 1 ||
                    working.ActivePlanet > entry.SceneBuildIndex;

                if (!isUnlocked)
                    continue;

                OfflinePlanetRewardData reward =
                    working.OfflinePlanetRewards.Find(
                        r => r.PlanetKey == entry.SaveKey);

                int creditedSeconds = reward?.CreditedSeconds ?? 0;

                int remainingSeconds = Math.Max(
                    0,
                    working.PassiveIncomeTime - creditedSeconds);

                int addedSeconds = Math.Min(
                    newOfflineSeconds,
                    remainingSeconds);

                if (addedSeconds <= 0)
                    continue;

                if (!PlayerPrefs.HasKey(entry.SaveKey))
                {
                    Debug.LogWarning(
                        $"[OfflinePlanetRewards] No save: {entry.SaveKey}");
                    continue;
                }

                if (!OfflinePlanetSaveReader.TryRead(
                        entry.SaveKey,
                        out GameDataSave planetSave,
                        out string error))
                {
                    Debug.LogError(
                        $"[OfflinePlanetRewards] {entry.SaveKey}: {error}");
                    return false;
                }

                if (!OfflinePlanetIncomeCalculator.TryCalculate(
                        entry,
                        planetSave,
                        effects,
                        out double moneyPerSec,
                        out error))
                {
                    Debug.LogError(
                        $"[OfflinePlanetRewards] {entry.SaveKey}: {error}");
                    return false;
                }

                if (moneyPerSec <= 0d)
                    continue;

                double moneyX1 =
                    (reward?.MoneyX1 ?? 0d) +
                    addedSeconds * moneyPerSec;

                double moneyX2 = Math.Truncate(moneyX1) * 2d;
                double moneyX3 = Math.Truncate(moneyX1) * 3d;

                if (double.IsNaN(moneyX1) ||
                    double.IsInfinity(moneyX1) ||
                    double.IsInfinity(moneyX2) ||
                    double.IsInfinity(moneyX3))
                {
                    Debug.LogError(
                        $"[OfflinePlanetRewards] Reward overflow: {entry.SaveKey}");
                    return false;
                }

                if (reward == null)
                {
                    reward = new OfflinePlanetRewardData
                    {
                        PlanetKey = entry.SaveKey,
                        RewardId = Guid.NewGuid().ToString("N")
                    };

                    working.OfflinePlanetRewards.Add(reward);
                }

                reward.CreditedSeconds += addedSeconds;
                reward.MoneyX1 = moneyX1;
                reward.MoneyX2 = moneyX2;
                reward.MoneyX3 = moneyX3;
                reward.LastCalculatedExitTime = working.ExitTime;

                Debug.Log(
                    $"[OfflinePlanetRewards] Planet={entry.SaveKey} | " +
                    $"AddedSeconds={addedSeconds} | " +
                    $"TotalSeconds={reward.CreditedSeconds} | " +
                    $"MoneyX1={reward.MoneyX1:R} | " +
                    $"MoneyX2={reward.MoneyX2:R} | " +
                    $"MoneyX3={reward.MoneyX3:R}");
            }
        }

        // Применяем только полностью подготовленные данные.
        _generalGameData.SetData(working.GetSaveData());

        Debug.Log(
            $"[OfflinePlanetRewards] Prepared | " +
            $"Captured={captured} | " +
            $"NewSeconds={newOfflineSeconds} | " +
            $"Rewards={working.OfflinePlanetRewards.Count}");

        return true;
    }

    private void SavePassiveIncomeReward()
    {
        _saveSystem.SavePassiveIncomeReward(
            new List<string> { _planetMode.Save() },
            new List<string> { _generalGameData.GetSaveData() }
        );
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

        Debug.Log(
            $"[PassiveIncomeTime] LOADED | " +
            $"Scene={gameObject.scene.name} | " +
            $"ExitTime={_generalGameData.ExitTime} | " +
            $"LastCalculated={_generalGameData.LastCalculatedExitTime} | " +
            $"Pending={_generalGameData.PendingOfflineSeconds}s"
        );

        if (CaptureOfflinePlanetRewards(ObserveOfflineReturnUtc()))
        {
            SaveGeneralGameData();
            PlayerPrefs.Save();

            Debug.Log(
                $"[PassiveIncomeTime] SAVED AFTER CAPTURE | " +
                $"Pending={_generalGameData.PendingOfflineSeconds}s"
            );
        }
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
            _generalGameData.ExitTime = System.DateTime.UtcNow.ToBinary().ToString();

            Debug.Log(
                $"[PassiveIncomeTime] APPLICATION PAUSED | " +
                $"Scene={gameObject.scene.name} | " +
                $"ExitTime={_generalGameData.ExitTime} | " +
                $"Pending={_generalGameData.PendingOfflineSeconds}s"
            );

            SaveGame();
            SaveGeneralGameData();

            PlayerPrefs.Save();

            Debug.Log(
                $"[PassiveIncomeTime] EXIT SAVED | " +
                $"ExitTime={_generalGameData.ExitTime}"
            );
        }
        else
        {
            if (_isGameSaved)
            {
                LoadGeneralGameData();
                LoadSavedData();
                Debug.Log("GameManager /// OnApplicationPause /// Load");

                if (_planetMode is StandardPlanetMode standardMode)
                    standardMode.TryActivatePassiveIncome();
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

#if UNITY_EDITOR
    private void TestOfflinePlanetIncomes()
    {
        var catalog =
            UnityEditor.AssetDatabase.LoadAssetAtPath<OfflinePlanetCatalog>(
                "Assets/_Scripts/PassiveIncome/OfflinePlanetCatalog.asset");

        if (catalog == null || catalog.Planets == null ||
            catalog.Planets.Count == 0)
        {
            Debug.LogError("[OfflineIncomeTest] Catalog is empty or missing");
            return;
        }

        var effects = new MetaUpgradeEffects();
        effects.Rebuild(catalog.MetaUpgradeConfigs);

        foreach (var entry in catalog.Planets)
        {
            if (!OfflinePlanetSaveReader.TryRead(
                    entry.SaveKey,
                    out GameDataSave save,
                    out string error))
            {
                Debug.LogWarning(
                    $"[OfflineIncomeTest] {entry.SaveKey}: {error}");
                continue;
            }

            if (!OfflinePlanetIncomeCalculator.TryCalculate(
                    entry,
                    save,
                    effects,
                    out double moneyPerSec,
                    out error))
            {
                Debug.LogWarning(
                    $"[OfflineIncomeTest] {entry.SaveKey}: {error}");
                continue;
            }

            Debug.Log(
                $"[OfflineIncomeTest] Planet={entry.SaveKey} | " +
                $"MoneyPerSec={moneyPerSec:R} | " +
                $"Managers={save.IsManagerPurchased}");
        }
    }
#endif
}
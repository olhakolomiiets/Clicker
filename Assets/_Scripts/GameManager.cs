using System;
using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Samples.Purchasing.IAP5.Demo;

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

    [Header("Passive Income Chest")]
    [SerializeField] private PassiveIncomeChestController _passiveIncomeChestController;

    private bool _iapGameContextReady;
    private bool _iapApplicationPaused;

    public enum IapGrantResult
    {
        Granted,
        AlreadyProcessed,
        Deferred,
        UnsupportedProduct
    }

    private const string GearsSaveKey = "SavelPlanetLevel1";
    private const string BatteriesSaveKey = "SavelPlanetLevel3";
    private const string LikesSaveKey = "SavelPlanetLevel4";

    private readonly struct IapReward
    {
        public readonly double Diamonds;
        public readonly string PlanetKey;
        public readonly double Money;
        public readonly bool StarterPackPurchased;
        public readonly bool SpecialOfferPurchased;
        public readonly bool AdsRemoved;
        public readonly bool CoinsBoosterPurchased;

        public IapReward(double diamonds = 0, string planetKey = null, double money = 0,
            bool starterPackPurchased = false, bool specialOfferPurchased = false, bool adsRemoved = false,
            bool coinsBoosterPurchased = false)
        {
            Diamonds = diamonds;
            PlanetKey = planetKey;
            Money = money;
            StarterPackPurchased = starterPackPurchased;
            SpecialOfferPurchased = specialOfferPurchased;
            AdsRemoved = adsRemoved;
            CoinsBoosterPurchased = coinsBoosterPurchased;
        }
    }

    private void OnEnable()
    {
        _iapGameContextReady = false;
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

        if (_planetMode is MetaPlanetMode metaPlanetMode)
        {
            metaPlanetMode.ConfigureCurrencyExchange(
                _saveSystem,
                _offlinePlanetCatalog);
        }

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
                    var entry = catalog.Planets.Find(p => p != null && p.SaveKey == _saveSystem.PlanetSaveKey);

                    if (entry == null)
                    {
                        Debug.LogError("[OfflineIncomeTest] Planet not found in catalog");
                    }
                    else
                    {
                        GameDataSave snapshot = JsonUtility.FromJson<GameDataSave>(_planetMode.Save());

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
        if (gameObject.scene.name == "PlanetMeta")
        {
            TestOfflinePlanetIncomes();
        }
#endif

        if (_planetMode is StandardPlanetMode standardMode)
        {
            standardMode.SetPassiveIncomeSaver(SavePassiveIncomeReward);

            _passiveIncomeChestController?.Initialize(_generalGameData, _saveSystem.PlanetSaveKey);

            standardMode.TryActivatePassiveIncome();
        }

        if (LastSceneLoader.TryGetSceneId(gameObject.scene.name, out int sceneId))
        {
            if (!PlayerPrefs.HasKey("SavedScene") || PlayerPrefs.GetInt("SavedScene") != sceneId)
            {
                PlayerPrefs.SetInt("SavedScene", sceneId);
                PlayerPrefs.Save();
            }
        }

        // Journal recovery can finish after the shop's OnEnable.
        if (SaveSystem.HasPurchasedCoinsBooster || SaveSystem.HasRemovedAds)
            RefreshIapShop(SaveSystem.HasRemovedAds);

        _iapGameContextReady = true;

        if (!_iapApplicationPaused)
            PaywallManager.Instance?.RegisterGameContext(this);
    }

    private void ConnectPurchaseManager()
    {
        _purchaseManager.OnPurchasingPack += _planetMode.AddPurchasedPack;
        _purchaseManager.OnPurchasingDiamonds += _planetMode.AddPurchasedDiamonds;
        _purchaseManager.OnPurchasingBooster += _planetMode.ActivatePurchasedBooster;
    }

    public IapGrantResult GrantIapPurchase(string store, string transactionId, string productId)
    {
        if (SaveSystem.IsIapRecoveryRequired)
        {
            UnregisterIapGameContext();
            return IapGrantResult.Deferred;
        }

        if (!HasReadyIapGameContext())
            return IapGrantResult.Deferred;

        if (_saveSystem.IsIapTransactionProcessed(store, transactionId, productId))
            return IapGrantResult.AlreadyProcessed;

        if (GetIapReward(productId) is not IapReward reward)
            return IapGrantResult.UnsupportedProduct;

        StandardPlanetMode openPlanet = null;
        string planetSnapshot = null;
        double updatedMoney = 0d;
        if (reward.PlanetKey != null)
        {
            GameDataSave planetSave;
            if (reward.PlanetKey == _saveSystem.PlanetSaveKey)
            {
                openPlanet = _planetMode as StandardPlanetMode;
                if (openPlanet == null || !openPlanet.HasInitializedIapData)
                    return IapGrantResult.Deferred;

                planetSave = JsonUtility.FromJson<GameDataSave>(_planetMode.Save());
            }
            else if (!OfflinePlanetSaveReader.TryRead(reward.PlanetKey, out planetSave, out _))
            {
                return IapGrantResult.Deferred;
            }

            // The offline reader accepts older saves with fewer structural checks.
            if (planetSave.ItemCount == null || planetSave.ItemCount.Count == 0 ||
                planetSave.UpgradeItemCount == null || planetSave.Managers?.Count != planetSave.ItemCount.Count ||
                planetSave.ItemBonusMultiplayer?.Count != planetSave.ItemCount.Count ||
                planetSave.ItemMaxCountHelper?.Count != planetSave.ItemCount.Count ||
                !TryAddIapBalance(planetSave.Money, reward.Money, out updatedMoney))
            {
                return IapGrantResult.Deferred;
            }

            planetSave.Money = updatedMoney;
            planetSnapshot = JsonUtility.ToJson(planetSave);
        }

        string generalSnapshot = null;
        double updatedDiamonds = 0d;
        if (reward.Diamonds > 0d)
        {
            var generalSave = JsonUtility.FromJson<GeneralGameDataSave>(_generalGameData.GetSaveData());
            if (!TryAddIapBalance(generalSave.Diamonds, reward.Diamonds, out updatedDiamonds))
                return IapGrantResult.Deferred;

            generalSave.Diamonds = updatedDiamonds;
            generalSnapshot = JsonUtility.ToJson(generalSave);
        }

        bool firstCoinsBoosterPurchase = reward.CoinsBoosterPurchased && !SaveSystem.HasPurchasedCoinsBooster;
        bool saved;
        try
        {
            saved = _saveSystem.SaveIapTransaction(store, transactionId, productId,
                planetKey: reward.PlanetKey,
                planetSnapshot: planetSnapshot,
                generalSnapshot: generalSnapshot,
                starterPackPurchased: reward.StarterPackPurchased,
                specialOfferPurchased: reward.SpecialOfferPurchased,
                adsRemoved: reward.AdsRemoved,
                coinsBoosterPurchased: reward.CoinsBoosterPurchased);
        }
        catch
        {
            if (SaveSystem.IsIapRecoveryRequired)
                UnregisterIapGameContext();

            throw;
        }

        if (!saved)
            return IapGrantResult.AlreadyProcessed;

        if (reward.Diamonds > 0d)
            _generalGameData.Diamonds = updatedDiamonds;

        openPlanet?.ApplySavedIapMoney(updatedMoney);

        LogIapPurchase(productId, reward, firstCoinsBoosterPurchase);

        try
        {
            _planetMode.UpdateData();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        RefreshIapShop(reward.AdsRemoved);

        return IapGrantResult.Granted;
    }

    private void LogIapPurchase(string productId, IapReward reward, bool firstCoinsBoosterPurchase)
    {
        try
        {
            var product = new Parameter("product_id", productId);
            string diamondsEvent = productId switch
            {
                "com.planetclicker.diamonds.small" or "com.planetclicker.diamondspack50" => "money_diamondsPack50_purchased",
                "com.planetclicker.diamonds.medium" or "com.planetclicker.diamondspack100" => "money_diamondsPack100_purchased",
                "com.planetclicker.diamonds.large" or "com.planetclicker.diamondspack300" => "money_diamondsPack300_purchased",
                _ => null
            };

            if (diamondsEvent != null)
            {
                FirebaseAnalytics.LogEvent(diamondsEvent, product, new Parameter("amount", reward.Diamonds));
            }
            else if (reward.StarterPackPurchased || reward.SpecialOfferPurchased)
            {
                string eventName = reward.StarterPackPurchased ? "money_starterPack_purchased" : "money_specialOffer_purchased";
                FirebaseAnalytics.LogEvent(eventName, product, new Parameter("diamonds_amount", reward.Diamonds),
                    new Parameter("gears_amount", reward.Money));
            }
            else if (productId == "com.planetclicker.noads")
            {
                FirebaseAnalytics.LogEvent("no_ads_purchased", product);
            }
            else if (firstCoinsBoosterPurchase)
            {
                FirebaseAnalytics.LogEvent("money_coinsBooster_purchased", product);
            }
            else if (reward.PlanetKey != null)
            {
                string resourceType = reward.PlanetKey switch
                {
                    GearsSaveKey => "gears",
                    BatteriesSaveKey => "batteries",
                    LikesSaveKey => "likes",
                    _ => null
                };

                if (resourceType != null)
                {
                    string packSize = productId.Substring(productId.LastIndexOf('.') + 1);
                    FirebaseAnalytics.LogEvent("money_planetCurrencyPack_purchased", product,
                        new Parameter("resource_type", resourceType), new Parameter("pack_size", packSize),
                        new Parameter("amount", reward.Money));
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    internal bool RestoreNoAds(string store, string transactionId)
    {
        if (!HasReadyIapGameContext() || SaveSystem.IsIapRecoveryRequired)
            return false;

        bool restored;
        try
        {
            restored = _saveSystem.RestoreNoAds(store, transactionId);
        }
        catch
        {
            if (SaveSystem.IsIapRecoveryRequired)
                UnregisterIapGameContext();

            throw;
        }

        if (restored)
            RefreshIapShop(adsRemoved: true);

        return true;
    }

    private bool HasReadyIapGameContext()
    {
        return _iapGameContextReady && !_iapApplicationPaused && isActiveAndEnabled &&
               _saveSystem != null && _planetModeBehaviour != null && _planetMode != null && _generalGameData != null;
    }

    private void RefreshIapShop(bool adsRemoved)
    {
        if (adsRemoved)
            PurchaseManager.ApplyNoAds();

        try
        {
            if (_purchaseManager != null)
                _purchaseManager.RefreshShopVisibility();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private static bool TryAddIapBalance(double balance, double reward, out double updatedBalance)
    {
        updatedBalance = balance + reward;
        // Reject rounding that changes the whole-unit reward.
        return balance >= 0d && updatedBalance > balance && !double.IsInfinity(updatedBalance) &&
               Math.Abs((updatedBalance - balance) - reward) < 0.5d;
    }

    private static IapReward? GetIapReward(string productId)
    {
        return productId switch
        {
            "com.planetclicker.diamonds.small" => new IapReward(diamonds: 100),
            "com.planetclicker.diamonds.medium" => new IapReward(diamonds: 250),
            "com.planetclicker.diamonds.large" => new IapReward(diamonds: 600),
            "com.planetclicker.coins.1p.small" => new IapReward(planetKey: GearsSaveKey, money: 600000),
            "com.planetclicker.coins.1p.medium" => new IapReward(planetKey: GearsSaveKey, money: 2100000),
            "com.planetclicker.coins.1p.large" => new IapReward(planetKey: GearsSaveKey, money: 4200000),
            "com.planetclicker.coins.2p.small" => new IapReward(planetKey: BatteriesSaveKey, money: 1200000),
            "com.planetclicker.coins.2p.medium" => new IapReward(planetKey: BatteriesSaveKey, money: 4200000),
            "com.planetclicker.coins.2p.large" => new IapReward(planetKey: BatteriesSaveKey, money: 8400000),
            "com.planetclicker.coins.3p.small" => new IapReward(planetKey: LikesSaveKey, money: 900000),
            "com.planetclicker.coins.3p.medium" => new IapReward(planetKey: LikesSaveKey, money: 3150000),
            "com.planetclicker.coins.3p.large" => new IapReward(planetKey: LikesSaveKey, money: 6300000),
            "com.planetclicker.starterpack.v2" => new IapReward(
                diamonds: 150, planetKey: GearsSaveKey, money: 3600, starterPackPurchased: true),
            "com.planetclicker.specialoffer.v2" => new IapReward(
                diamonds: 50, planetKey: GearsSaveKey, money: 1200, specialOfferPurchased: true, adsRemoved: true),
            "com.planetclicker.noads" => new IapReward(adsRemoved: true),
            "com.planetclicker.coinsbooster" => new IapReward(coinsBoosterPurchased: true),
            "com.planetclicker.diamondspack50" => new IapReward(diamonds: 50),
            "com.planetclicker.diamondspack100" => new IapReward(diamonds: 100),
            "com.planetclicker.diamondspack300" => new IapReward(diamonds: 300),
            "com.planetclicker.starterpack" => new IapReward(
                diamonds: 50, planetKey: GearsSaveKey, money: 50000, starterPackPurchased: true),
            "com.planetclicker.specialoffer" => new IapReward(
                diamonds: 50, planetKey: GearsSaveKey, money: 50000, specialOfferPurchased: true, adsRemoved: true),
            _ => null
        };
    }

    private void UnregisterIapGameContext()
    {
        _iapGameContextReady = false;
        PaywallManager.Instance?.UnregisterGameContext(this);
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
            bool? permanentBooster = working.OfflineCoinsBoosterState switch
            {
                1 => false,
                2 => true,
                _ => null
            };

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

                if (reward != null && reward.CreditedSeconds >= 60)
                    continue;

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
                        out error,
                        permanentBooster: permanentBooster))
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
        if (SaveSystem.IsIapRecoveryRequired)
        {
            UnregisterIapGameContext();
            return;
        }

        if (pauseStatus)
        {
            _iapApplicationPaused = true;
            PaywallManager.Instance?.UnregisterGameContext(this);
            _generalGameData.ExitTime = System.DateTime.UtcNow.ToBinary().ToString();
            _generalGameData.OfflineCoinsBoosterState = SaveSystem.HasPurchasedCoinsBooster ? 2 : 1;

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
                {
                    standardMode.TryActivatePassiveIncome();
                    _passiveIncomeChestController?.Refresh();
                }
            }

            _iapApplicationPaused = false;
            if (_iapGameContextReady && isActiveAndEnabled)
            {
                PaywallManager.Instance?.RegisterGameContext(this);
            }
        }
    }

    private void OnDisable()
    {
        UnregisterIapGameContext();

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

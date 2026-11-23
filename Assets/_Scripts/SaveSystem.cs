using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Save system for our game
/// </summary>
public class SaveSystem : MonoBehaviour
{
    //We areu using PlayerPrefs so here is the default key that we use
    [SerializeField]
    private string _saveKeyName;
    public string PlanetSaveKey => _saveKeyName;
    private string _saveGameKeyName = "SaveGeneralData";

    private const string PassiveIncomeTransactionKey =
    "PendingPassiveIncomeTransaction_v1";

    private const string CurrencyExchangeTransactionKey =
        "PendingCurrencyExchangeTransaction_v1";

    private const string IapTransactionKey = "PendingIapTransaction_v1";
    private const string ProcessedIapTransactionsKey = "ProcessedIapTransactions_v1";
    internal const string CoinsBoosterPurchasedKey = "CoinsBoosterPurchased";
    internal static bool HasPurchasedCoinsBooster => PlayerPrefs.GetInt(CoinsBoosterPurchasedKey, 0) == 1;
    internal const string NoAdsProductId = "com.planetclicker.noads";
    internal const string AdsRemovedKey = "adsRemoved";
    internal static bool HasRemovedAds => s_IapSaveFailed
        ? s_AdsRemovedBeforeIapSaveFailure
        : PlayerPrefs.GetInt(AdsRemovedKey, 0) == 1;

    private static bool s_passiveIncomeSaveFailed;
    internal static bool IsPassiveIncomeRecoveryRequired => s_passiveIncomeSaveFailed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPassiveIncomeSaveState()
    {
        s_passiveIncomeSaveFailed = false;
    }

    private static bool s_IapSaveFailed;
    private static bool s_AdsRemovedBeforeIapSaveFailure;

    internal static bool IsIapRecoveryRequired =>
        s_IapSaveFailed || PlayerPrefs.HasKey(IapTransactionKey);

    [Serializable]
    private sealed class PassiveIncomeTransaction
    {
        public string PlanetKey;
        public string PlanetJson;
        public string GeneralJson;
    }

    [Serializable]
    private sealed class CurrencyExchangeTransaction
    {
        public string PlanetKey;
        public string PlanetJson;
        public string GeneralJson;
    }

    [Serializable]
    private sealed class IapTransaction
    {
        public string Store;
        public string TransactionId;
        public string ProductId;
        public string PlanetKey;
        public string PlanetJson;
        public string GeneralJson;
        public bool StarterPackPurchased;
        public bool SpecialOfferPurchased;
        public bool AdsRemoved;
        public bool CoinsBoosterPurchased;
    }

    [Serializable]
    private sealed class ProcessedIapTransaction
    {
        public string Store;
        public string TransactionId;
        public string ProductId;
    }

    [Serializable]
    private sealed class ProcessedIapTransactions
    {
        public List<ProcessedIapTransaction> Transactions;
    }

    [Serializable]
    private sealed class IapSnapshotFields
    {
        public double Money = double.NaN;
        public double Diamonds = double.NaN;
        public int ActivePlanet = -1;
    }

    /// <summary>Checks a completed IAP transaction. Throws if recovery is required or the registry is invalid.</summary>
    public bool IsIapTransactionProcessed(string store, string transactionId, string productId)
    {
        ThrowIfIapRecoveryRequired();
        ValidateIapIdentity(store, transactionId, productId);
        return FindProcessedIapTransaction(LoadProcessedIapTransactions(), store, transactionId, productId);
    }

    /// <summary>Returns true for a new save and false for an already processed purchase. Throws on failure.</summary>
    public bool SaveIapTransaction(
        string store,
        string transactionId,
        string productId,
        string planetKey = null,
        string planetSnapshot = null,
        string generalSnapshot = null,
        bool starterPackPurchased = false,
        bool specialOfferPurchased = false,
        bool adsRemoved = false,
        bool coinsBoosterPurchased = false)
    {
        ThrowIfIapSaveBlocked();
        ValidateIapIdentity(store, transactionId, productId);
        var registry = LoadProcessedIapTransactions();
        if (FindProcessedIapTransaction(registry, store, transactionId, productId))
            return false;

        var transaction = new IapTransaction
        {
            Store = store,
            TransactionId = transactionId,
            ProductId = productId,
            PlanetKey = planetKey,
            PlanetJson = planetSnapshot == null ? null : JsonUtility.ToJson(new SavedData
            {
                savedData = new List<string> { planetSnapshot }
            }),
            GeneralJson = generalSnapshot == null ? null : JsonUtility.ToJson(new SavedGeneralData
            {
                savedGeneralData = new List<string> { generalSnapshot }
            }),
            StarterPackPurchased = starterPackPurchased,
            SpecialOfferPurchased = specialOfferPurchased,
            AdsRemoved = adsRemoved,
            CoinsBoosterPurchased = coinsBoosterPurchased
        };

        ValidateIapTransaction(transaction);
        ApplyIapTransaction(transaction, registry, JsonUtility.ToJson(transaction));
        return true;
    }

    private void RecoverIapTransaction()
    {
        if (!PlayerPrefs.HasKey(IapTransactionKey))
            return;

        var transaction = JsonUtility.FromJson<IapTransaction>(PlayerPrefs.GetString(IapTransactionKey));
        if (transaction == null)
            throw new InvalidOperationException("IAP transaction is invalid.");

        ValidateIapIdentity(transaction.Store, transaction.TransactionId, transaction.ProductId);
        ValidateIapTransaction(transaction);

        ApplyIapTransaction(transaction, LoadProcessedIapTransactions());
    }

    internal bool RestoreNoAds(string store, string transactionId)
    {
        ThrowIfIapSaveBlocked();
        ValidateIapIdentity(store, transactionId, NoAdsProductId);
        if (HasRemovedAds)
            return false;

        // Only the entitlement can be repaired when this ID is already registered.
        var transaction = new IapTransaction
        {
            Store = store,
            TransactionId = transactionId,
            ProductId = NoAdsProductId,
            AdsRemoved = true
        };

        ApplyIapTransaction(transaction, LoadProcessedIapTransactions(), JsonUtility.ToJson(transaction));
        return true;
    }

    private static void ThrowIfIapSaveBlocked()
    {
        ThrowIfIapRecoveryRequired();
        if (PlayerPrefs.HasKey(PassiveIncomeTransactionKey) ||
            PlayerPrefs.HasKey(CurrencyExchangeTransactionKey))
        {
            throw new InvalidOperationException("Pending economy transaction recovery is required.");
        }
    }

    private void ApplyIapTransaction(
        IapTransaction transaction,
        ProcessedIapTransactions registry,
        string newJournalJson = null)
    {
        if (!FindProcessedIapTransaction(registry, transaction.Store, transaction.TransactionId, transaction.ProductId))
        {
            registry.Transactions.Add(new ProcessedIapTransaction
            {
                Store = transaction.Store,
                TransactionId = transaction.TransactionId,
                ProductId = transaction.ProductId
            });
        }

        string registryJson = JsonUtility.ToJson(registry);
        bool adsRemovedBeforeSave = HasRemovedAds;
        try
        {
            if (newJournalJson != null)
            {
                PlayerPrefs.SetString(IapTransactionKey, newJournalJson);
                PlayerPrefs.Save();
            }

            if (!string.IsNullOrEmpty(transaction.PlanetJson))
                PlayerPrefs.SetString(transaction.PlanetKey, transaction.PlanetJson);

            if (!string.IsNullOrEmpty(transaction.GeneralJson))
                PlayerPrefs.SetString(_saveGameKeyName, transaction.GeneralJson);

            if (transaction.StarterPackPurchased)
                PlayerPrefs.SetInt("StarterPackPurchased", 1);

            if (transaction.SpecialOfferPurchased)
                PlayerPrefs.SetInt("SpecialOfferPurchased", 1);

            if (transaction.AdsRemoved)
                PlayerPrefs.SetInt(AdsRemovedKey, 1);

            if (transaction.CoinsBoosterPurchased)
                PlayerPrefs.SetInt(CoinsBoosterPurchasedKey, 1);

            PlayerPrefs.SetString(ProcessedIapTransactionsKey, registryJson);
            PlayerPrefs.Save();

            PlayerPrefs.DeleteKey(IapTransactionKey);
            PlayerPrefs.Save();
        }
        catch
        {
            // A failed save can leave the in-memory keys ahead of disk.
            s_AdsRemovedBeforeIapSaveFailure = adsRemovedBeforeSave;
            s_IapSaveFailed = true;
            throw;
        }
    }

    private static ProcessedIapTransactions LoadProcessedIapTransactions()
    {
        if (!PlayerPrefs.HasKey(ProcessedIapTransactionsKey))
            return new ProcessedIapTransactions { Transactions = new List<ProcessedIapTransaction>() };

        var registry = JsonUtility.FromJson<ProcessedIapTransactions>(PlayerPrefs.GetString(ProcessedIapTransactionsKey));
        if (registry?.Transactions == null)
            throw new InvalidOperationException("Processed IAP registry is invalid.");

        var keys = new HashSet<(string Store, string TransactionId)>();
        foreach (var transaction in registry.Transactions)
        {
            if (transaction == null)
                throw new InvalidOperationException("Processed IAP registry contains an invalid entry.");

            ValidateIapIdentity(transaction.Store, transaction.TransactionId, transaction.ProductId);
            if (!keys.Add((transaction.Store, transaction.TransactionId)))
                throw new InvalidOperationException("Processed IAP registry contains a duplicate transaction.");
        }

        return registry;
    }

    private static bool FindProcessedIapTransaction(
        ProcessedIapTransactions registry, string store, string transactionId, string productId)
    {
        var existing = registry.Transactions.Find(entry => entry.Store == store && entry.TransactionId == transactionId);
        if (existing == null)
            return false;

        if (existing.ProductId != productId)
            throw new InvalidOperationException("The processed IAP transaction has a different product ID.");

        return true;
    }

    private static void ValidateIapIdentity(string store, string transactionId, string productId)
    {
        if (string.IsNullOrWhiteSpace(store) || string.IsNullOrWhiteSpace(transactionId) ||
            string.IsNullOrWhiteSpace(productId))
        {
            throw new InvalidOperationException("IAP transaction store, transaction ID and product ID are required.");
        }
    }

    private static void ValidateIapTransaction(IapTransaction transaction)
    {
        bool hasPlanet = !string.IsNullOrEmpty(transaction.PlanetJson);
        bool hasGeneral = !string.IsNullOrEmpty(transaction.GeneralJson);
        if (hasPlanet != !string.IsNullOrEmpty(transaction.PlanetKey))
            throw new InvalidOperationException("IAP planet key and snapshot must be supplied together.");

        if (!hasPlanet && !hasGeneral && !transaction.StarterPackPurchased &&
            !transaction.SpecialOfferPurchased && !transaction.AdsRemoved && !transaction.CoinsBoosterPurchased)
        {
            throw new InvalidOperationException("IAP transaction has no changes.");
        }

        if (hasPlanet)
        {
            if (transaction.PlanetKey != "SavelPlanetLevel1" &&
                transaction.PlanetKey != "SavelPlanetLevel3" &&
                transaction.PlanetKey != "SavelPlanetLevel4")
            {
                throw new InvalidOperationException("IAP planet save key is invalid.");
            }

            string snapshot = GetIapSnapshot(JsonUtility.FromJson<SavedData>(transaction.PlanetJson).savedData);
            var data = JsonUtility.FromJson<GameDataSave>(snapshot);
            var fields = new IapSnapshotFields();
            JsonUtility.FromJsonOverwrite(snapshot, fields);
            if (!IsValidIapBalance(fields.Money) ||
                data.ItemCount == null || data.ItemCount.Count == 0 || data.UpgradeItemCount == null ||
                data.Managers?.Count != data.ItemCount.Count ||
                data.ItemBonusMultiplayer?.Count != data.ItemCount.Count ||
                data.ItemMaxCountHelper?.Count != data.ItemCount.Count)
            {
                throw new InvalidOperationException("IAP planet snapshot is invalid.");
            }
        }

        if (hasGeneral)
        {
            string snapshot = GetIapSnapshot(JsonUtility.FromJson<SavedGeneralData>(transaction.GeneralJson).savedGeneralData);
            var fields = new IapSnapshotFields();
            JsonUtility.FromJsonOverwrite(snapshot, fields);
            if (!IsValidIapBalance(fields.Diamonds) || fields.ActivePlanet < 0)
                throw new InvalidOperationException("IAP general snapshot is invalid.");
        }
    }

    private static string GetIapSnapshot(List<string> data)
    {
        if (data == null || data.Count != 1 || string.IsNullOrWhiteSpace(data[0]))
            throw new InvalidOperationException("IAP snapshot envelope is invalid.");

        return data[0];
    }

    private static bool IsValidIapBalance(double balance)
    {
        return balance >= 0d && !double.IsInfinity(balance);
    }

    private static void ThrowIfIapRecoveryRequired()
    {
        if (s_passiveIncomeSaveFailed)
            throw new InvalidOperationException("Passive income recovery requires a fresh application process.");

        if (IsIapRecoveryRequired)
            throw new InvalidOperationException("Pending IAP transaction recovery is required.");
    }

    public void SavePassiveIncomeReward(List<string> planetData, List<string> generalData)
    {
        ThrowIfIapRecoveryRequired();
        RecoverPendingTransactions();

        if (string.IsNullOrEmpty(_saveKeyName))
            throw new InvalidOperationException(
                "Planet save key is missing.");

        var transaction = new PassiveIncomeTransaction
        {
            PlanetKey = _saveKeyName,
            PlanetJson = JsonUtility.ToJson(new SavedData
            {
                savedData = planetData
            }),
            GeneralJson = JsonUtility.ToJson(new SavedGeneralData
            {
                savedGeneralData = generalData
            })
        };

        try
        {
            PlayerPrefs.SetString(
                PassiveIncomeTransactionKey,
                JsonUtility.ToJson(transaction)
            );

            PlayerPrefs.Save();
        }
        catch
        {
            // Do not retry PlayerPrefs writes after an uncertain storage failure.
            s_passiveIncomeSaveFailed = true;
            throw;
        }

        RecoverPassiveIncomeTransaction();
    }

    private void RecoverPassiveIncomeTransaction()
    {
        if (!PlayerPrefs.HasKey(PassiveIncomeTransactionKey))
            return;

        var transaction =
            JsonUtility.FromJson<PassiveIncomeTransaction>(
                PlayerPrefs.GetString(PassiveIncomeTransactionKey)
            );

        if (transaction == null ||
            string.IsNullOrEmpty(transaction.PlanetKey) ||
            string.IsNullOrEmpty(transaction.PlanetJson) ||
            string.IsNullOrEmpty(transaction.GeneralJson) ||
            string.IsNullOrEmpty(_saveGameKeyName))
        {
            throw new InvalidOperationException(
                "Passive income transaction is invalid.");
        }

        try
        {
            PlayerPrefs.SetString(
                transaction.PlanetKey,
                transaction.PlanetJson
            );

            PlayerPrefs.SetString(
                _saveGameKeyName,
                transaction.GeneralJson
            );

            PlayerPrefs.Save();

            PlayerPrefs.DeleteKey(PassiveIncomeTransactionKey);
            PlayerPrefs.Save();
        }
        catch
        {
            s_passiveIncomeSaveFailed = true;
            throw;
        }
    }

    public void RecoverPendingTransactions()
    {
        if (s_passiveIncomeSaveFailed)
            throw new InvalidOperationException("Passive income recovery requires a fresh application process.");

        if (s_IapSaveFailed)
            throw new InvalidOperationException("IAP transaction recovery requires a fresh application process.");

        bool hasPassive =
            PlayerPrefs.HasKey(PassiveIncomeTransactionKey);

        bool hasExchange =
            PlayerPrefs.HasKey(CurrencyExchangeTransactionKey);

        bool hasIap = PlayerPrefs.HasKey(IapTransactionKey);
        int pendingCount = (hasPassive ? 1 : 0) + (hasExchange ? 1 : 0) + (hasIap ? 1 : 0);
        if (pendingCount > 1)
        {
            throw new InvalidOperationException(
                "Multiple pending economy transactions found.");
        }

        RecoverPassiveIncomeTransaction();
        RecoverCurrencyExchangeTransaction();
        RecoverIapTransaction();
    }

    public void SaveCurrencyExchange(string planetKey, List<string> planetData, List<string> generalData)
    {
        ThrowIfIapRecoveryRequired();
        RecoverPendingTransactions();

        if (string.IsNullOrWhiteSpace(planetKey) || planetKey == _saveGameKeyName || planetKey == PassiveIncomeTransactionKey || planetKey == CurrencyExchangeTransactionKey ||
            planetKey == IapTransactionKey || planetKey == ProcessedIapTransactionsKey)
            throw new ArgumentException("Invalid planet save key.", nameof(planetKey));

        if (!PlayerPrefs.HasKey(planetKey))
            throw new InvalidOperationException("Selected planet save does not exist.");

        if (planetData == null || planetData.Count == 0 || string.IsNullOrWhiteSpace(planetData[0]))
            throw new ArgumentException("Planet snapshot is invalid.", nameof(planetData));

        if (generalData == null || generalData.Count == 0 || string.IsNullOrWhiteSpace(generalData[0]))
            throw new ArgumentException("General snapshot is invalid.", nameof(generalData));


        var transaction = new CurrencyExchangeTransaction
        {
            PlanetKey = planetKey,

            PlanetJson = JsonUtility.ToJson(new SavedData
            {
                savedData = planetData
            }),

            GeneralJson = JsonUtility.ToJson(new SavedGeneralData
            {
                savedGeneralData = generalData
            })
        };

        // First persist the complete prepared transaction.
        PlayerPrefs.SetString(CurrencyExchangeTransactionKey, JsonUtility.ToJson(transaction));
        PlayerPrefs.Save();

        // Apply the snapshots and remove the journal.
        RecoverCurrencyExchangeTransaction();
    }

    private void RecoverCurrencyExchangeTransaction()
    {
        if (!PlayerPrefs.HasKey(CurrencyExchangeTransactionKey))
            return;

        var transaction = JsonUtility.FromJson<CurrencyExchangeTransaction>(PlayerPrefs.GetString(CurrencyExchangeTransactionKey));

        if (transaction == null || string.IsNullOrWhiteSpace(transaction.PlanetKey) || transaction.PlanetKey == _saveGameKeyName || string.IsNullOrWhiteSpace(transaction.PlanetJson) || string.IsNullOrWhiteSpace(transaction.GeneralJson))
            throw new InvalidOperationException("Currency exchange transaction is invalid.");

        // Apply absolute snapshots, never increment balances.
        PlayerPrefs.SetString(transaction.PlanetKey, transaction.PlanetJson);

        PlayerPrefs.SetString(_saveGameKeyName, transaction.GeneralJson);

        PlayerPrefs.Save();

        // Delete only after all three snapshots are saved.
        PlayerPrefs.DeleteKey(CurrencyExchangeTransactionKey);
        PlayerPrefs.Save();
    }


    public void SaveThePlanet(List<string> dataToSave)
    {
        ThrowIfIapRecoveryRequired();
        RecoverPendingTransactions();
        SavedData data = new SavedData
        {
            savedData = dataToSave,
        };
        PlayerPrefs.SetString(_saveKeyName, JsonUtility.ToJson(data));
        //Debug.Log("Planet Saved " + _saveKeyName);
    }

    public List<string> LoadPlanet()
    {
        RecoverPendingTransactions();
        if (PlayerPrefs.HasKey(_saveKeyName) == false)
            return new();
        SavedData data = JsonUtility.FromJson<SavedData>(PlayerPrefs.GetString(_saveKeyName));

        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadPlanet /// SaveKeyName  " + _saveKeyName);
        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadPlanet /// Data " + data.savedData);
        return data.savedData;
    }

    public void SaveTheGame(List<string> dataToSave)
    {
        ThrowIfIapRecoveryRequired();
        RecoverPendingTransactions();
        SavedGeneralData data = new SavedGeneralData
        {
            savedGeneralData = dataToSave,
        };
        PlayerPrefs.SetString(_saveGameKeyName, JsonUtility.ToJson(data));
        //Debug.Log("Game Saved " + _saveGameKeyName);
    }

    public List<string> LoadGame()
    {
        RecoverPendingTransactions();
        if (PlayerPrefs.HasKey(_saveGameKeyName) == false)
            return new();
        SavedGeneralData data = JsonUtility.FromJson<SavedGeneralData>(PlayerPrefs.GetString(_saveGameKeyName));

        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadGame /// SaveGameKeyName " + _saveGameKeyName);
        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadGame /// GeneralData " + data.savedGeneralData);
        return data.savedGeneralData;
    }

    public void ResetData()
    {
        ThrowIfIapRecoveryRequired();
        RecoverPendingTransactions();
        PlayerPrefs.DeleteKey(_saveKeyName);
    }


    [Serializable]
    private struct SavedData
    {
        public List<string> savedData;
    }

    [Serializable]
    private struct SavedGeneralData
    {
        public List<string> savedGeneralData;
    }
}

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
    public void SavePassiveIncomeReward(List<string> planetData, List<string> generalData)
    {
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

        PlayerPrefs.SetString(
            PassiveIncomeTransactionKey,
            JsonUtility.ToJson(transaction)
        );

        PlayerPrefs.Save();

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

    public void RecoverPendingTransactions()
    {
        bool hasPassive =
            PlayerPrefs.HasKey(PassiveIncomeTransactionKey);

        bool hasExchange =
            PlayerPrefs.HasKey(CurrencyExchangeTransactionKey);

        if (hasPassive && hasExchange)
        {
            throw new InvalidOperationException(
                "Multiple pending economy transactions found.");
        }

        RecoverPassiveIncomeTransaction();
        RecoverCurrencyExchangeTransaction();
    }

    public void SaveCurrencyExchange(string planetKey, List<string> planetData, List<string> generalData)
    {
        RecoverPendingTransactions();

        if (string.IsNullOrWhiteSpace(planetKey) || planetKey == _saveGameKeyName || planetKey == PassiveIncomeTransactionKey || planetKey == CurrencyExchangeTransactionKey)
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

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

    private string _saveGameKeyName = "SaveGeneralData";

    private const string PassiveIncomeTransactionKey =
    "PendingPassiveIncomeTransaction_v1";

    [Serializable]
    private sealed class PassiveIncomeTransaction
    {
        public string PlanetKey;
        public string PlanetJson;
        public string GeneralJson;
    }

    public void SavePassiveIncomeReward(List<string> planetData, List<string> generalData)
    {
        RecoverPassiveIncomeTransaction();

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

    public void SaveThePlanet(List<string> dataToSave)
    {
        RecoverPassiveIncomeTransaction();
        SavedData data = new SavedData
        {
            savedData = dataToSave,
        };
        PlayerPrefs.SetString(_saveKeyName, JsonUtility.ToJson(data));
        //Debug.Log("Planet Saved " + _saveKeyName);
    }

    public List<string> LoadPlanet()
    {
        RecoverPassiveIncomeTransaction();
        if (PlayerPrefs.HasKey(_saveKeyName) == false)
            return new();
        SavedData data = JsonUtility.FromJson<SavedData>(PlayerPrefs.GetString(_saveKeyName));

        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadPlanet /// SaveKeyName  " + _saveKeyName);
        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadPlanet /// Data " + data.savedData);
        return data.savedData;
    }

    public void SaveTheGame(List<string> dataToSave)
    {
        RecoverPassiveIncomeTransaction();
        SavedGeneralData data = new SavedGeneralData
        {
            savedGeneralData = dataToSave,
        };
        PlayerPrefs.SetString(_saveGameKeyName, JsonUtility.ToJson(data));
        //Debug.Log("Game Saved " + _saveGameKeyName);
    }

    public List<string> LoadGame()
    {
        RecoverPassiveIncomeTransaction();
        if (PlayerPrefs.HasKey(_saveGameKeyName) == false)
            return new();
        SavedGeneralData data = JsonUtility.FromJson<SavedGeneralData>(PlayerPrefs.GetString(_saveGameKeyName));

        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadGame /// SaveGameKeyName " + _saveGameKeyName);
        //Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! SaveSystem /// LoadGame /// GeneralData " + data.savedGeneralData);
        return data.savedGeneralData;
    }

    public void ResetData()
        => PlayerPrefs.DeleteKey(_saveKeyName);


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

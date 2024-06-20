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

    public void SaveThePlanet(List<string> dataToSave)
    {
        SavedData data = new SavedData
        {
            savedData = dataToSave,
        };
        PlayerPrefs.SetString(_saveKeyName, JsonUtility.ToJson(data));
        Debug.Log("Planet Saved " + _saveKeyName);
    }

    public List<string> LoadPlanet()
    {
        if (PlayerPrefs.HasKey(_saveKeyName) == false)
            return new();
        SavedData data = JsonUtility.FromJson<SavedData>(PlayerPrefs.GetString(_saveKeyName));
        Debug.Log("Planet Load " + _saveKeyName);

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! Load GameData /// LoadData " + data.savedData);
        return data.savedData;
    }

    public void SaveTheGame(List<string> dataToSave)
    {
        SavedGeneralData data = new SavedGeneralData
        {
            savedGeneralData = dataToSave,
        };
        PlayerPrefs.SetString(_saveGameKeyName, JsonUtility.ToJson(data));
        Debug.Log("Game Saved " + _saveGameKeyName);
    }

    public List<string> LoadGame()
    {
        if (PlayerPrefs.HasKey(_saveGameKeyName) == false)
            return new();
        SavedGeneralData data = JsonUtility.FromJson<SavedGeneralData>(PlayerPrefs.GetString(_saveGameKeyName));
        Debug.Log("Game Load " + _saveGameKeyName);

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! Load GameData /// LoadData " + data.savedGeneralData);
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

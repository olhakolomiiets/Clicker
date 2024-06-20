using System;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;

public class UserManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    GeneralGameData _currentGameData;

    private string PlayerPrefsKey = "HasVisitedGame";

    public void PrepareGameData(GeneralGameData gameData)
    {
        _currentGameData = gameData;

        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
            GenerateUserData();
        else
            inputField.text = _currentGameData.UserName;
    }

    public void GenerateUserData()
    {
        _currentGameData.UserId = Guid.NewGuid().ToString();

        string username = "Planet#";
        string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(timestamp));
            string combinedInfo = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 10);
            _currentGameData.UserName = username + combinedInfo;

            inputField.text = _currentGameData.UserName;
        }

        PlayerPrefs.SetInt(PlayerPrefsKey, 1);
    }

    public void UpdateUsername()
    {
        _currentGameData.UserName = inputField.text;
    }
}
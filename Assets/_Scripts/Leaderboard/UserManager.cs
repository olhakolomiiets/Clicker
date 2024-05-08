using System;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;

public class UserManager : MonoBehaviour
{
    [SerializeField] private UserData _userData;
    [SerializeField] private TMP_InputField inputField;

    private const string PlayerPrefsKey = "HasVisitedGame";

    void Start()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            _userData.UserId = Guid.NewGuid().ToString(); 
            GenerateUsername(); 
            PlayerPrefs.SetInt(PlayerPrefsKey, 1); 
        }
        else
        {
            inputField.text = _userData.UserName;
        }

    }
private void GenerateUsername()
    {
            string username = "Planet#";
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(timestamp));
                string combinedInfo  = BitConverter.ToString(bytes).Replace("-", "").Substring(0, 10);
                _userData.UserName = username + combinedInfo;

                inputField.text = _userData.UserName;
            }
    }

    public void UpdateUsername()
    {
        _userData.UserName = inputField.text;
    }
}
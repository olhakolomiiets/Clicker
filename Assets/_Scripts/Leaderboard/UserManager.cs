using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class UserManager : MonoBehaviour
{
    [SerializeField] private Text NicknameLabel;
    [SerializeField] private GameObject UserNamePanel;
    [SerializeField] private GameObject EditNamePanel;
    [SerializeField] private InputField EditInput;

    private string deviceID;
    private string apiUrl = "https://pbie.fatcat.com.ua/api/";
    private string userName;

    [SerializeField] private GameRules _gameRules;
    public double totalScore;

    void Start()
    {
        deviceID = SystemInfo.deviceUniqueIdentifier;

        if (!IsInternetAvailable())
        {
            Debug.LogWarning("No internet connection!");
            return;
        }

        StartCoroutine(Login());
    }

    private void OnApplicationQuit()
    {
        UpdateLeaderboardData();
    }

    private bool IsInternetAvailable()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
    }

    #region LOGIN

    IEnumerator Login()
    {
        string url = apiUrl + "login.php";

        userName = "Planet_" + Random.Range(1000, 9999);
        string json = "{\"device_id\":\"" + deviceID + "\",\"name\":\"" + userName + "\"}";

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                PlayerData data = JsonUtility.FromJson<PlayerData>(response);

                if (NicknameLabel != null)
                    NicknameLabel.text = data.name;

                totalScore = data.total_score;
                Debug.Log("Логін успішний! Гравець: " + data.name + ", Очки: " + data.total_score);
            }
            else
            {
                Debug.LogError("Помилка логіну: " + request.error);
            }
        }
    }

    #endregion

    #region UPDATE USERNAME

    public void UpdateNickname()
    {
        string newName = EditInput.text;

        if (string.IsNullOrEmpty(newName))
        {
            Debug.LogError("Invalid input: Name cannot be empty.");
            return;
        }

        NicknameLabel.text = newName;
        ShowNickname();

        PlayerPrefs.SetString("UserName", newName);
        PlayerPrefs.Save();

        StartCoroutine(UpdateUserNameRequest(deviceID, newName));
    }

    private void ShowNickname()
    {
        UserNamePanel.SetActive(true);
        EditNamePanel.SetActive(false);
    }

    public void ShowEditName()
    {
        EditInput.text = NicknameLabel.text;
        UserNamePanel.SetActive(false);
        EditNamePanel.SetActive(true);
    }

    private IEnumerator UpdateUserNameRequest(string deviceID, string newName)
    {
        WWWForm form = new WWWForm();
        form.AddField("device_id", deviceID);
        form.AddField("name", newName);

        using (UnityWebRequest request = UnityWebRequest.Post(apiUrl + "update_name.php", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error updating username: " + request.error);
            }
            else
            {
                Debug.Log("Username updated successfully");
            }
        }
    }

    #endregion

    #region UPDATE TOTAL SCORE

    private void UpdateLeaderboardData()
    {
        if (!IsInternetAvailable())
        {
            Debug.LogWarning("No internet connection!");
            return;
        }

        totalScore = _gameRules._totalScore;
        StartCoroutine(UpdateLeaderboard());
    }

    private IEnumerator UpdateLeaderboard()
    {
        WWWForm form = new WWWForm();
        form.AddField("device_id", deviceID);
        form.AddField("total_score", totalScore.ToString(System.Globalization.CultureInfo.InvariantCulture));

        using (UnityWebRequest request = UnityWebRequest.Post(apiUrl + "update_score.php", form))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error updating score: " + request.error);
            }
            else
            {
                Debug.Log("Score updated successfully");
            }
        }
    }

    #endregion

    [System.Serializable]
    private class PlayerData
    {
        public string device_id;
        public string name;
        public int total_score;
    }
}
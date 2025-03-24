using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using TMPro;

public class PBIELogin : MonoBehaviour
{
    private string baseUrl = "https://pbie.fatcat.com.ua/api/";
    private string deviceId;
    public TMP_Text playerNameText;  // Поле для відображення імені в UI
    public int totalScore;

    void Start()
    {
        deviceId = SystemInfo.deviceUniqueIdentifier;

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.LogError("Немає підключення до інтернету! Логін не виконується.");
            return;
        }

        StartCoroutine(Login());
    }

    IEnumerator Login()
    {
        string url = baseUrl + "login.php";

        // Створюємо унікальне ім'я при першому вході
        string generatedName = "Planet_" + Random.Range(1000, 9999);
        string json = "{\"device_id\":\"" + deviceId + "\",\"name\":\"" + generatedName + "\"}";

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

                if (playerNameText != null)
                    playerNameText.text = "Гравець: " + data.name;

                totalScore = data.total_score;
                Debug.Log("Логін успішний! Гравець: " + data.name + ", Очки: " + data.total_score);
            }
            else
            {
                Debug.LogError("Помилка логіну: " + request.error);
            }
        }
    }

    [System.Serializable]
    private class PlayerData
    {
        public string device_id;
        public string name;
        public int total_score;
    }
}

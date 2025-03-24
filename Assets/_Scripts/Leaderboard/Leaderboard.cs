using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static LeaderboardUser;

public class Leaderboard : MonoBehaviour
{
    [SerializeField] private string apiUrl = "https://pbie.fatcat.com.ua/api/";
    [SerializeField] private GameObject leaderboardEntryPrefab;
    [SerializeField] private Transform leaderboardContainer;
    [SerializeField] private GameRules _gameRules;
    [SerializeField] private GameObject serviceText;

    private string deviceID;
    public double totalScore;
    private bool isUpdatingLeaderboard = false;

    private void OnEnable()
    {
        _gameRules.OnDataUpdated += OnDataUpdate;
        deviceID = SystemInfo.deviceUniqueIdentifier;
        UpdateLeaderboardData();
    }

    private void OnDataUpdate()
    {
        if (!isUpdatingLeaderboard)
        {
            StartCoroutine(ThrottleUpdateLeaderboard());
        }
    }

    private IEnumerator ThrottleUpdateLeaderboard()
    {
        isUpdatingLeaderboard = true;
        UpdateLeaderboardData();
        yield return new WaitForSeconds(2f);
        isUpdatingLeaderboard = false;
    }

    private void UpdateLeaderboardData()
    {
        if (!IsInternetAvailable())
        {
            serviceText.SetActive(true);
            Debug.LogWarning("No internet connection!");
            return;
        }

        serviceText.SetActive(false);
        totalScore = _gameRules._totalScore;
        StartCoroutine(UpdateLeaderboard());
    }

    private bool IsInternetAvailable()
    {
        return Application.internetReachability != NetworkReachability.NotReachable;
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

        StartCoroutine(LoadLeaderboard());
    }

    private IEnumerator LoadLeaderboard()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl + "leaderboard.php"))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;
                LeaderboardUser.LeaderboardEntry[] leaderboardEntries = JsonHelper.FromJson<LeaderboardUser.LeaderboardEntry>(json);
                DisplayLeaderboard(leaderboardEntries);
            }
            else
            {
                Debug.LogError("Error loading leaderboard: " + request.error);
            }
        }
    }

    private void DisplayLeaderboard(LeaderboardUser.LeaderboardEntry[] leaderboardEntries)
    {
        foreach (Transform child in leaderboardContainer)
        {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < leaderboardEntries.Length; i++)
        {
            GameObject entryObj = Instantiate(leaderboardEntryPrefab, leaderboardContainer);
            LeaderboardUser entryScript = entryObj.GetComponent<LeaderboardUser>();
            entryScript.Display(leaderboardEntries[i], i + 1, deviceID);
        }
    }

    private void OnDisable()
    {
        _gameRules.OnDataUpdated -= OnDataUpdate;
    }
}

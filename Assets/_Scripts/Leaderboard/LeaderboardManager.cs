using UnityEngine;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.SocialPlatforms;


public class LeaderboardManager : MonoBehaviour
{
    private string leaderboardId = "planet_builder_leaderboard";

    public GameObject leaderboardEntryPrefab;
    public Transform leaderboardContainer;

    // Authenticate the player with Google Play Games Services
    private void Start()
    {
        //PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder().Build();
        //PlayGamesPlatform.InitializeInstance(config);
        PlayGamesPlatform.Activate();
        Social.localUser.Authenticate(OnAuthenticationFinished);
    }

    // Callback function for authentication
    private void OnAuthenticationFinished(bool success)
    {
        if (success)
        {
            Debug.Log("Authenticated to Google Play Games Services");
            // Retrieve leaderboard data
            LoadLeaderboard();
        }
        else
        {
            Debug.LogWarning("Failed to authenticate to Google Play Games Services");
        }
    }

    // Retrieve leaderboard data
    private void LoadLeaderboard()
    {
        Social.LoadScores(leaderboardId, scores =>
        {
            if (scores.Length > 0)
            {
                // Clear existing entries from UI
                foreach (Transform child in leaderboardContainer)
                {
                    Destroy(child.gameObject);
                }

                // Populate UI with leaderboard data
                foreach (IScore score in scores)
                {
                    GameObject entry = Instantiate(leaderboardEntryPrefab, leaderboardContainer);
                    // Assuming leaderboardEntryPrefab has a script attached to handle data population
                    entry.GetComponent<LeaderboardEntry>().SetData(score);
                }
            }
            else
            {
                Debug.LogWarning("No scores found for leaderboard: " + leaderboardId);
            }
        });
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SocialPlatforms;

public class LeaderboardEntry : MonoBehaviour
{
    public Text rankText;
    public Text playerNameText;
    public Text scoreText;

    // Set UI elements with data from leaderboard
    public void SetData(IScore score)
    {
        rankText.text = score.rank.ToString();
        playerNameText.text = score.userID;
        scoreText.text = score.value.ToString();
    }
}

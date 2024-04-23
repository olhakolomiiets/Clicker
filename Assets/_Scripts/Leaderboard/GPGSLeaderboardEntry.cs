using UnityEngine;
using UnityEngine.SocialPlatforms;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class GPGSLeaderboardEntry : MonoBehaviour
{
    public TextMeshProUGUI playerNameText;
    public RawImage playerImg;
    public TextMeshProUGUI rankText;    
    public TextMeshProUGUI scoreText;

    public string url;

    private Texture2D userTexture;

    public void SetData(int rank, string userName, Texture2D image, string userID, long value)
    {
        rankText.text = rank.ToString();
        playerNameText.text = userName;
        scoreText.text = value.ToString();

        playerImg.texture = image;

        Debug.Log("!!!!!----- LeaderboardEntry SetData: Rank" + rank + " Player Name " + userName + " User ID " + userID + " Score " + value + " -----!!!!!");
    }

}

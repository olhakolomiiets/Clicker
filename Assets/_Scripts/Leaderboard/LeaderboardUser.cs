using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardUser : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text displayName;
    [SerializeField] private TMP_Text rank;
    [SerializeField] private TMP_Text score;

    [Header("Colors")]
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color activeColor;

    private string playerDeviceId;

    public void Display(LeaderboardEntry data, int position, string id)
    {
        displayName.text = data.name;
        rank.text = position.ToString();
        score.text = data.total_score.ToString();
        bool isMine = data.device_id == id;
        background.color = isMine ? activeColor : defaultColor;
    }

    [System.Serializable]
    public class LeaderboardEntry
    {
        public string device_id;
        public string name;
        public double total_score;
    }
}
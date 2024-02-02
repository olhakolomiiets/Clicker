using TMPro;
using UnityEngine;

public class ScorePanel : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI coreText;


    public void SetScore(double score)
    {
        coreText.text = $"{score.ToString("N0")}";       
    }    
    public void SetDiamondsScore(double score)
    {
        coreText.text = $"{score.ToString("N0")}";
    }
}


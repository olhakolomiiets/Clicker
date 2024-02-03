using TMPro;
using UnityEngine;

public class ScorePanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI coreText;


    public void SetScore(double score)
    {
        if(score > 1000)
            coreText.text = AbbreviateNumber(score);
        else
            coreText.text = $"{score.ToString("N0")}";
    }    
    public void SetDiamondsScore(double score)
    {
        coreText.text = $"{score.ToString("N0")}";
    }


    string AbbreviateNumber(double number)
    {
        string[] suffixes = { "", "K", "M", "B", "T" };
        int suffixIndex = 0;

        while (number >= 1000 && suffixIndex < suffixes.Length - 1)
        {
            number /= 1000;
            suffixIndex++;
        }

        return string.Format("{0:0.##} {1}", number, suffixes[suffixIndex]).Replace(',', '.');
    }

}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class LeaderboardEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text usernameText;
    [SerializeField] private TMP_Text moneyText;
    public string UserName { get; set; }
    public double UserMoney { get; set; }
    public int Rank { get; set; }
    public void NewElement(int _rank, string _username, double _money)
    {
        usernameText.text = _username;
        rankText.text = _rank.ToString();
        
        UserName = _username;
        UserMoney = _money;
        Rank = _rank;

        if (_money > 1000)
            moneyText.text = AbbreviateNumber(_money);
        else
            moneyText.text = $"{_money.ToString("N0")}";
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

    public void UpdateLeaderboardRank(double _money)
    {
        rankText.text = Rank.ToString();

        if (_money > 1000)
            moneyText.text = AbbreviateNumber(_money);
        else
            moneyText.text = $"{_money.ToString("N0")}";
    }

}
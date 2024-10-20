using System;
using UnityEngine;
public class GeneralGameData 
{
    public string UserName { get; set;}
    public string UserId { get; set;}  
    public double TotalScore { get; set;}
    public double Diamonds { get; set;}
    public string ExitTime { get; set;}
    public int PassiveIncomeTime { get; set;} = 180;
    public int ExtraTimePurchasedCount { get; set; }
    public bool IsGameSaved { get; set; }

    public void SetData(string dataString)
    {
        if (String.IsNullOrEmpty(dataString))
            return;
        GeneralGameDataSave data = JsonUtility.FromJson<GeneralGameDataSave>(dataString);
        UserName = data.UserName;
        UserId = data.UserId;
        TotalScore = data.TotalScore;
        Diamonds = data.Diamonds;
        ExitTime = data.ExitTime;
        PassiveIncomeTime = data.PassiveIncomeTime;
        ExtraTimePurchasedCount = data.ExtraTimePurchasedCount;
        IsGameSaved = data.IsGameSaved;

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! General Game Data /// SetData " + dataString);
    }

    public string GetSaveData()
        => JsonUtility.ToJson(new GeneralGameDataSave
        {
            UserName = UserName,
            UserId = UserId,
            TotalScore = TotalScore,
            Diamonds = Diamonds,
            ExitTime = DateTime.Now.ToBinary().ToString(),
            PassiveIncomeTime = PassiveIncomeTime,
            ExtraTimePurchasedCount = ExtraTimePurchasedCount,
            IsGameSaved = IsGameSaved
        });
}

[Serializable]
public struct GeneralGameDataSave
{
    public string UserName;
    public string UserId;
    public double TotalScore;
    public double Diamonds;
    public string ExitTime;
    public int PassiveIncomeTime;
    public int ExtraTimePurchasedCount;
    public bool IsGameSaved;
}


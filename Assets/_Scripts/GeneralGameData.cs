using System;
using UnityEngine;
using System.Collections.Generic;
public class GeneralGameData 
{
    public string UserName { get; set;}
    public string UserId { get; set;}  
    public double TotalScore { get; set;}
    public double Diamonds { get; set;}
    public double DiamondsPerSec { get; set;}
    public string ExitTime { get; set; }
    public int PassiveIncomeTime { get; set;} = 1800;
    public int ExtraTimePurchasedCount { get; set; }
    public bool IsGameSaved { get; set; }
    public int ActivePlanet { get; set; } = 1;
    public int PlanetUnlocked { get; set; } = 1;

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
        ActivePlanet = data.ActivePlanet;
        DiamondsPerSec = data.DiamondsPerSec;

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
            IsGameSaved = IsGameSaved,
            ActivePlanet = ActivePlanet,
            DiamondsPerSec = DiamondsPerSec,
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
    public int ActivePlanet;
    public bool IsGameSaved;
    public double DiamondsPerSec;
}


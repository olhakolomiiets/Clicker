using System;
using UnityEngine;
using System.Collections.Generic;
public class GeneralGameData
{
    public string UserName { get; set; }
    public string UserId { get; set; }
    public double TotalScore { get; set; }
    public double Diamonds { get; set; }
    public double DiamondsPerSec { get; set; }
    public string ExitTime { get; set; }
    public int PassiveIncomeTime { get; set; } = 1800;
    public int ExtraTimePurchasedCount { get; set; }
    public bool IsGameSaved { get; set; }
    public int ActivePlanet { get; set; } = 2;
    public int PlanetUnlocked { get; set; } = 2;
    public MetaUpgradeEffects MetaUpgradeEffects { get; set; }

    public int PendingOfflineSeconds { get; private set; }
    public bool OfflinePlanetRewardsEnabled { get; set; }

    public List<OfflinePlanetRewardData> OfflinePlanetRewards
    {
        get;
        private set;
    } = new();

    public string LastCalculatedExitTime { get; private set; }

    public void ClearPendingOfflineTime()
    {
        PendingOfflineSeconds = 0;
    }
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
        PendingOfflineSeconds = Math.Max(0, data.PendingOfflineSeconds);
        LastCalculatedExitTime = data.LastCalculatedExitTime;

        OfflinePlanetRewardsEnabled = data.OfflinePlanetRewardsEnabled;

        OfflinePlanetRewards = data.OfflinePlanetRewards
            ?? new List<OfflinePlanetRewardData>();

        Debug.Log("!!!!!!!!!!!!-------------!!!!!!!!!! General Game Data /// SetData " + dataString);
    }


    public string GetSaveData()
        => JsonUtility.ToJson(new GeneralGameDataSave
        {
            UserName = UserName,
            UserId = UserId,
            TotalScore = TotalScore,
            Diamonds = Diamonds,
            ExitTime = this.ExitTime,
            PassiveIncomeTime = PassiveIncomeTime,
            ExtraTimePurchasedCount = ExtraTimePurchasedCount,
            IsGameSaved = IsGameSaved,
            ActivePlanet = ActivePlanet,
            DiamondsPerSec = DiamondsPerSec,
            PendingOfflineSeconds = PendingOfflineSeconds,
            LastCalculatedExitTime = LastCalculatedExitTime,

            OfflinePlanetRewardsEnabled = this.OfflinePlanetRewardsEnabled,
            OfflinePlanetRewards = this.OfflinePlanetRewards,
        });

    public bool CaptureOfflineTime(DateTime utcNow) => CaptureOfflineTime(utcNow, out _);

    public bool CaptureOfflineTime(DateTime utcNow, out int newOfflineSeconds)
    {
        newOfflineSeconds = 0;

        Debug.Log(
            $"[PassiveIncomeTime] CAPTURE START | " +
            $"ExitTime={ExitTime} | " +
            $"LastCalculated={LastCalculatedExitTime} | " +
            $"Pending={PendingOfflineSeconds}s | " +
            $"Now={utcNow:O}"
        );

        if (string.IsNullOrEmpty(ExitTime) ||
            LastCalculatedExitTime == ExitTime)
        {
            Debug.Log(
                $"[PassiveIncomeTime] CAPTURE SKIPPED | " +
                $"Reason={(string.IsNullOrEmpty(ExitTime) ? "No ExitTime" : "Already calculated")} | " +
                $"Pending={PendingOfflineSeconds}s"
            );
            return false;
        }

        if (!long.TryParse(ExitTime, out long binaryTime))
        {
            Debug.LogWarning("Invalid ExitTime.");
            return false;
        }

        DateTime exitUtc;

        try
        {
            exitUtc = DateTime.FromBinary(binaryTime).ToUniversalTime();
        }
        catch (ArgumentException)
        {
            Debug.LogWarning("Invalid ExitTime.");
            return false;
        }

        double seconds = Math.Max(0d, (utcNow - exitUtc).TotalSeconds);

        long additionalSeconds = (long)Math.Min(int.MaxValue, Math.Floor(seconds));

        int previousPending = PendingOfflineSeconds;

        newOfflineSeconds = (int)additionalSeconds;

        if (!OfflinePlanetRewardsEnabled)
        {
            PendingOfflineSeconds = (int)Math.Min(
                int.MaxValue,
                (long)PendingOfflineSeconds + additionalSeconds
            );
        }

        LastCalculatedExitTime = ExitTime;

        Debug.Log(
            $"[PassiveIncomeTime] CAPTURE SUCCESS | " +
            $"ExitUTC={exitUtc:O} | " +
            $"NowUTC={utcNow:O} | " +
            $"Added={additionalSeconds}s | " +
            $"Previous={previousPending}s | " +
            $"Pending={PendingOfflineSeconds}s | " +
            $"LastCalculated={LastCalculatedExitTime}"
        );

        return true;
    }
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
    public int PendingOfflineSeconds;
    public string LastCalculatedExitTime;
    public bool OfflinePlanetRewardsEnabled;
    public List<OfflinePlanetRewardData> OfflinePlanetRewards;
}


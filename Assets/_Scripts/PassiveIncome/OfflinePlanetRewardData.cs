using System;

[Serializable]
public class OfflinePlanetRewardData
{
    public string PlanetKey;
    public string RewardId;

    public int CreditedSeconds;

    public double MoneyX1;
    public double MoneyX2;
    public double MoneyX3;

    public string LastCalculatedExitTime;
}
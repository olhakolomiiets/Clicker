using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GameData class stores all the data about the Incremental Game part of our project (not the visuals - enemie health and count)
/// </summary>
public class GameData 
{
    //Money representation should handle a lot but at some point it will get reset to 0
    public double Money { get; set;}
    public double Diamonds { get; set;}

    //Bonus multiplier allows us to better balance the incremental game
    //https://www.gamedeveloper.com/design/the-math-of-idle-games-part-i
    public List<int> ItemCount = new();
    public List<int> UpgradeItemCount = new();
    public List<bool> Managers = new();
    public List<int> ItemBonusMultiplayer = new();
    public List<int> ItemMaxCountHelper = new();

    public List<ItemData> ItemDataList = new();

    public List<UpgradeItemData> UpgradeItemDataList = new();

    public bool IsPremium { get; set; }

    public void SetData(string dataString)
    {
        if (String.IsNullOrEmpty(dataString))
            return;
        GameDataSave data = JsonUtility.FromJson<GameDataSave>(dataString);
        Money = data.Money;
        Diamonds = data.Diamonds;
        ItemCount = data.ItemCount;
        UpgradeItemCount = data.UpgradeItemCount;
        Managers = data.Managers;
        ItemBonusMultiplayer = data.ItemBonusMultiplayer;
        ItemMaxCountHelper = data.ItemMaxCountHelper;
        IsPremium = data.IsPremium;
    }

    public string GetSaveData()
        => JsonUtility.ToJson(new GameDataSave
        {
            Money = Money,
            Diamonds = Diamonds,
            ItemCount = ItemCount,
            UpgradeItemCount = UpgradeItemCount,
            Managers = Managers,
            ItemBonusMultiplayer = ItemBonusMultiplayer,
            ItemMaxCountHelper = ItemMaxCountHelper,
            IsPremium = IsPremium
        });
}

[Serializable]
public struct GameDataSave
{
    public double Money;
    public double Diamonds;
    public List<int> ItemCount;
    public List<int> UpgradeItemCount;
    public List<bool> Managers;
    public List<int> ItemBonusMultiplayer;
    public List<int> ItemMaxCountHelper;
    public bool IsPremium;
}


using System;
using UnityEngine;

public class MetaGameRules : MonoBehaviour
{
    private GeneralGameData _generalGameData;

    public event Action<GeneralGameData, GameData> OnUpdateGameData;

    public void PrepareData(GeneralGameData generalGameData)
    {
        _generalGameData = generalGameData;
        SendDataUpdate();
    }

    public void HandleVariantItem(double price)
    {
        if (_generalGameData.Diamonds < price)
            return;

        _generalGameData.Diamonds -= price;
        SendDataUpdate();
    }

    public void HandleUpgradeLevel(double price)
    {
        if (_generalGameData.Diamonds < price)
            return;

        _generalGameData.Diamonds -= price;
        SendDataUpdate();
    }

    public void SendDataUpdate()
    {
        OnUpdateGameData?.Invoke(_generalGameData, null);
    }

    public void GetPurchasedProduct(double coins, double diamonds)
    {
        _generalGameData.Diamonds += diamonds;

        SendDataUpdate();
    }

    public void GetPurchasedProduct(double diamonds)
    {
        _generalGameData.Diamonds += diamonds;

        SendDataUpdate();
    }
}
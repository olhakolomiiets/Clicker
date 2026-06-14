using System;
using UnityEngine;

public class MetaGameRules : MonoBehaviour
{
    private GeneralGameData _generalGameData;

    public bool IsPrepared => _generalGameData != null;

    public event Action<GeneralGameData, GameData> OnUpdateGameData;

    public void PrepareData(GeneralGameData generalGameData)
    {
        _generalGameData = generalGameData;
        SendDataUpdate();
    }

    public void HandleVariantItem(double price)
    {
        TryHandleVariantItem(price);
    }

    public bool TryHandleVariantItem(double price)
    {
        if (!CanAfford(price))
            return false;

        _generalGameData.Diamonds -= price;
        SendDataUpdate();
        return true;
    }

    public void HandleUpgradeLevel(double price)
    {
        TryHandleUpgradeLevel(price);
    }

    public bool TryHandleUpgradeLevel(double price)
    {
        if (!CanAfford(price))
            return false;

        _generalGameData.Diamonds -= price;
        SendDataUpdate();
        return true;
    }

    public bool CanAfford(double price)
    {
        return _generalGameData != null &&
               price >= 0d &&
               _generalGameData.Diamonds >= price;
    }

    public double CurrentDiamonds => _generalGameData != null
        ? _generalGameData.Diamonds
        : 0d;

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

using System;
using UnityEngine;

[CreateAssetMenu]

public class UpgradeItemData : ScriptableObject
{
    public int ItemIncome(int itemCount) => ItemBaseIncome * itemCount;

    [field: SerializeField]
    public int ItemBaseIncome { get; set; } = 1;

    [field: SerializeField]
    public int ItemCost { get; private set; } = 3;

    [field: SerializeField]
    public float Delay { get; set; } = 0.6f;

    [field: SerializeField]
    public Sprite ItemImage { get; set; }


}

using System;
using UnityEngine;

[CreateAssetMenu]

public class UpgradeItemData : ScriptableObject
{
    public double ItemIncome(int itemCount) => ItemBaseIncome * itemCount;

    [field: SerializeField]
    public double ItemBaseIncome { get; set; } = 1;

    [field: SerializeField]
    public double ItemCost { get; private set; } = 3;

    [field: SerializeField]
    public float Delay { get; set; } = 0.6f;

    [field: SerializeField]
    public Sprite ItemImage { get; set; }

    [field: SerializeField]
    public string TranslationText { get; set; }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class PassiveIncomeData : ScriptableObject
{
    [field: SerializeField]
    public int ExtraTime { get; set; }

    [field: SerializeField]
    public int ExtraTimeCount { get; set; }

    [field: SerializeField]
    public int ExtraTimePurchasedCount { get; set; }

    [field: SerializeField]
    public double ExtraTimePrice { get; set; }

    [field: SerializeField]
    public double ExtraTimePriceMultiplier { get; set; }
}

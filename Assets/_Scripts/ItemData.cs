using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu]
public class ItemData : ScriptableObject
{
    public double ItemIncome(int itemCount, int bonusMultiplier) => ItemBaseIncome * BoosterMultiplier * itemCount * bonusMultiplier;
    public double ItemIncomePerSec(int itemCount, int bonusMultiplier) => ItemBaseIncome * BoosterMultiplier * itemCount * bonusMultiplier / (double)Delay;
    public double DiamondsIncome(int itemCount) => ItemDiamondsIncome * itemCount;
    //A way to set the base price of purchasing item and the upgrade cost in one method
    public double ItemUpgradePrice(int itemCount)
        => itemCount switch
        {
            0 => ItemStartCost,
            _ => ItemStartCost * Math.Pow(ItemCostMultiFactor, (itemCount + 1))
        };

    //Those parameters are editable though the inspector. Originally i have planed to load this data from a file (for example xls)
    [field: SerializeField]
    public double ItemBaseIncome { get; set; }

    [field: SerializeField]
    public int ItemDiamondsIncome { get; set; }
    
    [field: SerializeField]
    public double ItemStartCost { get; private set; }
    [field: SerializeField]
    public double ItemCostMultiFactor { get; private set; }

    [field: SerializeField]
    public double BoosterMultiplier { get; set; }

    //We set the max count as a limit before we increase the bonus multplier for the score
       public int MaxCount(int bonusMultiplier, int maxCountHelper) =>
            bonusMultiplier switch
            {
                1 when MaxCountIncrement > 10 => MaxCountIncrement / 5,
                2 when MaxCountIncrement > 5 => MaxCountIncrement / 2,
                _ => maxCountHelper
            };

    //public int MaxCount(int maxCountHelper) => maxCountHelper;


    //Parameter used to caluclate if we should increase the bonus multiplier
    public int BonusMaxCountThreshold => 4;
    //Parameter used to caluclate if we should increase the bonus multiplier

    [SerializeField] public int MaxCountIncrement;
    
    [field: SerializeField]
    public float Delay { get; set; }
    [field: SerializeField]
    public float ManagerPrice { get; set; }
    [field: SerializeField]
    public Sprite ItemImage { get; set; }
    [field: SerializeField] 
    public bool IsPremium { get; set; }    
    [field: SerializeField]
    public bool Auto { get; set; }  
    
    [field: SerializeField] 
    public string TranslationText { get; set; }

}

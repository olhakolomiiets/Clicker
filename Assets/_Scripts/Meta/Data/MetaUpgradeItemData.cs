using System;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "PlanetBuilder/UpgradeItemData")]
public class MetaUpgradeItemData : ScriptableObject
{
    [Header("Base Settings")]
    [SerializeField] public string ItemName;
    [SerializeField] public double Price;
    [SerializeField] public GameObject BoxPrefab;
    [SerializeField] public List<GameObject> LevelPrefabs = new();
    [SerializeField] public Sprite Icon;

    [Header("Upgrade")]
    [SerializeField] public List<UpgradeLevel> Upgrades;

    [Header("Economic Effect")]
    public MetaUpgradeEffectType EffectType = MetaUpgradeEffectType.None;

    public List<ItemData> TargetItems = new();

    public string SaveId;

    public double UpgradeLevelPrice(int index)
    => Upgrades[index].UpgradeCost;
}

[Serializable]
public class UpgradeLevel
{
    public string LevelName;
    public float UpgradeCost;
    public float BonusValue;
    public string BonusDescription;
    public Sprite ItemIcon;
}

public enum MetaUpgradeEffectType
{
    None = 0,
    IncomeMultiplier = 1,
    ProductionTimeMultiplier = 2
}

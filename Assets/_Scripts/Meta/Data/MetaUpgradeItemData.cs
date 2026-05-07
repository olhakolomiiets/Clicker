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

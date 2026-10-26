using System;
using System.Collections.Generic;
using UnityEngine;

public class MetaUpgradeEffects
{
    private const string ES3_FILE = "meta_upgrades.es3";

    private readonly Dictionary<ItemData, double> _incomeMultipliers = new();
    private readonly Dictionary<ItemData, double> _timeMultipliers = new();

    public void Rebuild(IEnumerable<MetaUpgradeItemData> configs)
    {
        _incomeMultipliers.Clear();
        _timeMultipliers.Clear();

        if (configs == null)
            return;

        HashSet<string> processedIds = new();

        foreach (var config in configs)
        {
            if (config == null ||
                config.EffectType == MetaUpgradeEffectType.None ||
                config.Upgrades == null ||
                config.Upgrades.Count == 0 ||
                config.TargetItems == null)
                continue;

            if (string.IsNullOrWhiteSpace(config.SaveId))
            {
                Debug.LogWarning($"[MetaUpgradeEffects] Missing SaveId: {config.name}");
                continue;
            }

            if (!processedIds.Add(config.SaveId))
            {
                Debug.LogWarning($"[MetaUpgradeEffects] Duplicate SaveId: {config.SaveId}");
                continue;
            }

            string key = $"upgrade.{config.SaveId}";

            if (!ES3.KeyExists(key, ES3_FILE))
                continue;

            ItemUpgradeSaveData save;

            try
            {
                save = ES3.Load<ItemUpgradeSaveData>(key, ES3_FILE);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MetaUpgradeEffects] Load failed: {key} | {e.Message}");
                continue;
            }

            if (save == null || !save.isActive)
                continue;

            int level = save.upgradeLevel;

            if (level < 0 || level >= config.Upgrades.Count)
            {
                Debug.LogWarning($"[MetaUpgradeEffects] Invalid level: {key} ({level})");
                continue;
            }

            double multiplier = config.Upgrades[level].BonusValue;

            if (double.IsNaN(multiplier) ||
                double.IsInfinity(multiplier) ||
                multiplier <= 0)
            {
                Debug.LogWarning($"[MetaUpgradeEffects] Invalid multiplier: {key}");
                continue;
            }

            Dictionary<ItemData, double> targetDictionary;

            switch (config.EffectType)
            {
                case MetaUpgradeEffectType.IncomeMultiplier:
                    targetDictionary = _incomeMultipliers;
                    break;

                case MetaUpgradeEffectType.ProductionTimeMultiplier:
                    targetDictionary = _timeMultipliers;
                    break;

                default:
                    continue;
            }

            HashSet<ItemData> uniqueItems = new(config.TargetItems);

            foreach (ItemData item in uniqueItems)
            {
                if (item == null)
                    continue;

                double current = targetDictionary.TryGetValue(
                    item, out double value) ? value : 1d;

                targetDictionary[item] = current * multiplier;
            }
        }
    }

    public double GetIncomeMultiplier(ItemData item)
    {
        return item != null &&
               _incomeMultipliers.TryGetValue(item, out double value)
            ? value
            : 1d;
    }

    public double GetTimeMultiplier(ItemData item)
    {
        return item != null &&
               _timeMultipliers.TryGetValue(item, out double value)
            ? value
            : 1d;
    }
}
using System;
using UnityEngine;

public static class OfflinePlanetIncomeCalculator
{
    public static bool TryCalculate(
        OfflinePlanetEntry planet,
        GameDataSave save,
        MetaUpgradeEffects effects,
        out double moneyPerSec,
        out string error,
        bool? permanentBooster = null)
    {
        moneyPerSec = 0d;
        error = null;

        if (planet == null ||
            planet.Items == null ||
            planet.Items.Count == 0 ||
            effects == null)
        {
            error = "Missing planet configuration or effects.";
            return false;
        }

        int length = planet.Items.Count;

        if (save.ItemCount == null ||
            save.ItemBonusMultiplayer == null ||
            save.ItemMaxCountHelper == null ||
            save.ItemCount.Count != length ||
            save.ItemBonusMultiplayer.Count != length ||
            save.ItemMaxCountHelper.Count != length)
        {
            error = "Save lists do not match catalog Items.";
            return false;
        }

        double booster = (permanentBooster ?? save.IsBoosterPurchased) ? 2d : 1d;

        double total = 0d;

        for (int i = 0; i < length; i++)
        {
            ItemData item = planet.Items[i];

            if (item == null)
            {
                error = $"Missing ItemData at index {i}.";
                return false;
            }

            if (item.IsPremium)
                continue;

            int count = save.ItemCount[i];
            int bonus = save.ItemBonusMultiplayer[i];

            if (count < 0 || bonus <= 0)
            {
                error = $"Invalid item state at index {i}.";
                return false;
            }

            double incomeMultiplier =
                effects.GetIncomeMultiplier(item);

            double timeMultiplier =
                effects.GetTimeMultiplier(item);

            if (!IsFinite(incomeMultiplier) ||
                incomeMultiplier <= 0d ||
                !IsFinite(timeMultiplier) ||
                timeMultiplier <= 0d ||
                !IsFinite(item.ItemBaseIncome) ||
                !IsFinite(item.Delay))
            {
                error = $"Invalid income configuration at index {i}.";
                return false;
            }

            float effectiveDelay = Mathf.Max(
                0.1f,
                (float)(item.Delay * timeMultiplier)
            );

            if (float.IsNaN(effectiveDelay) ||
                float.IsInfinity(effectiveDelay))
            {
                error = $"Invalid delay at index {i}.";
                return false;
            }

            double incomePerCycle =
                item.ItemBaseIncome * booster * count * bonus;

            double itemIncomePerSec =
                incomePerCycle * incomeMultiplier / effectiveDelay;

            total += itemIncomePerSec;

            if (!IsFinite(total) || total < 0d)
            {
                error = $"Invalid result at index {i}.";
                return false;
            }
        }

        moneyPerSec = total;
        return true;
    }

    public static bool TryCalculateDiamonds(
        OfflinePlanetEntry planet,
        GameDataSave save,
        MetaUpgradeEffects effects,
        int offlineSeconds,
        out double diamonds,
        out string error,
        double passiveDiamondsMultiplier = 1d)
    {
        diamonds = 0d;
        error = null;

        if (planet == null || planet.Items == null ||
            planet.Items.Count == 0 || effects == null || offlineSeconds < 0 ||
            !IsFinite(passiveDiamondsMultiplier) || passiveDiamondsMultiplier < 0d ||
            save.ItemCount == null || save.ItemCount.Count != planet.Items.Count ||
            (save.Managers != null && save.Managers.Count != planet.Items.Count))
        {
            error = "Invalid premium income input.";
            return false;
        }

        double total = 0d;
        for (int i = 0; i < planet.Items.Count; i++)
        {
            ItemData item = planet.Items[i];
            if (item == null)
            {
                error = $"Missing ItemData at index {i}.";
                return false;
            }

            if (!item.IsPremium)
                continue;

            int count = save.ItemCount[i];
            if (count < 0)
            {
                error = $"Invalid premium count at index {i}.";
                return false;
            }

            // Missing legacy manager data does not establish automation ownership.
            if (count == 0 || save.Managers == null || !save.Managers[i])
                continue;

            double incomeMultiplier = effects.GetIncomeMultiplier(item);
            double timeMultiplier = effects.GetTimeMultiplier(item);
            if (item.ItemDiamondsIncome < 0 ||
                !IsFinite(item.Delay) || item.Delay <= 0f ||
                !IsFinite(incomeMultiplier) || incomeMultiplier <= 0d ||
                !IsFinite(timeMultiplier) || timeMultiplier <= 0d)
            {
                error = $"Invalid premium configuration at index {i}.";
                return false;
            }

            float effectiveDelay = Mathf.Max(0.1f,
                (float)(item.Delay * timeMultiplier));
            if (float.IsNaN(effectiveDelay) || float.IsInfinity(effectiveDelay))
            {
                error = $"Invalid premium delay at index {i}.";
                return false;
            }

            double cycles = Math.Floor(offlineSeconds / (double)effectiveDelay);
            double incomePerCycle =
                (double)item.ItemDiamondsIncome * count * incomeMultiplier;
            total += cycles * incomePerCycle;
            if (!IsFinite(total) || total < 0d)
            {
                error = $"Premium income overflow at index {i}.";
                return false;
            }
        }

        double scaledDiamonds = total * passiveDiamondsMultiplier;
        if (!IsFinite(scaledDiamonds))
        {
            error = "Passive premium income overflow.";
            return false;
        }

        diamonds = total > 0d
            ? Math.Max(1d, Math.Floor(scaledDiamonds))
            : 0d;
        return true;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value);
    }
}
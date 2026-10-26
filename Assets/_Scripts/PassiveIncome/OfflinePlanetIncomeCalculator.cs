using UnityEngine;

public static class OfflinePlanetIncomeCalculator
{
    public static bool TryCalculate(
        OfflinePlanetEntry planet,
        GameDataSave save,
        MetaUpgradeEffects effects,
        out double moneyPerSec,
        out string error)
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

        double booster = save.BoosterMultiplier;

        if (!IsFinite(booster) || booster < 0d)
        {
            error = "Invalid BoosterMultiplier.";
            return false;
        }

        if (booster == 0d)
            booster = 1d;

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

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value);
    }
}
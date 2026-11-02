
using System;
using System.Collections.Generic;
using UnityEngine;

public class MetaPlanetCurrencyExchange : MonoBehaviour
{
    [Serializable]
    public class PlanetSettings
    {
        public int planetId;
        public double baseIncomePerSecond = 1;
        public double baseDiamondPrice = 1800;
    }

    [Serializable]
    private class RateState
    {
        public int planetId;
        public double price;
        public long nextUpdateTicks;
    }

    [Serializable]
    private class SaveData
    {
        public List<RateState> rates = new List<RateState>();
        public long windowStartTicks;
        public int exchangedDiamonds;
    }

    public struct ExchangeQuote
    {
        public double pricePerDiamond;
        public int remainingDiamonds;
        public DateTime nextUpdateUtc;
    }

    public enum ExchangeResult
    {
        Success,
        InvalidAmount,
        PlanetNotConfigured,
        DailyLimitReached,
        InvalidRate,
        WalletRejected
    }

    [Header("Planet currencies")]
    [SerializeField] private List<PlanetSettings> planets =
        new List<PlanetSettings>();

    [Header("Exchange settings")]
    [SerializeField, Min(1)]
    private int rateUpdateHours = 24;

    [SerializeField, Range(1f, 2f)]
    private float inflationPower = 1.15f;

    [SerializeField, Min(1)]
    private int maxDiamondsPer24Hours = 3;

    private const string SaveKey = "MetaExchange_V1";
    private SaveData data;

    private void Awake()
    {
        Load();
    }

    public bool TryGetQuote(
        int planetId,
        double incomePerSecond,
        out ExchangeQuote quote)
    {
        quote = default;

        PlanetSettings settings =
            planets.Find(p => p.planetId == planetId);

        if (settings == null ||
            settings.baseIncomePerSecond <= 0 ||
            settings.baseDiamondPrice <= 0 ||
            double.IsNaN(incomePerSecond) ||
            double.IsInfinity(incomePerSecond) ||
            incomePerSecond < 0)
        {
            return false;
        }

        DateTime now = DateTime.UtcNow;
        bool changed = RefreshDailyLimit(now);

        RateState state =
            data.rates.Find(r => r.planetId == planetId);

        if (state == null)
        {
            double price = CalculatePrice(
                settings, incomePerSecond);

            if (!IsValidPrice(price))
                return false;

            state = new RateState
            {
                planetId = planetId,
                price = price,
                nextUpdateTicks =
                    now.AddHours(rateUpdateHours).Ticks
            };

            data.rates.Add(state);
            changed = true;
        }
        else if (now.Ticks >= state.nextUpdateTicks)
        {
            double newPrice = CalculatePrice(
                settings, incomePerSecond);

            if (!IsValidPrice(newPrice))
                return false;

            // The exchange rate never decreases.
            state.price = Math.Max(state.price, newPrice);
            state.nextUpdateTicks =
                now.AddHours(rateUpdateHours).Ticks;

            changed = true;
        }

        if (changed)
            Save();

        quote = new ExchangeQuote
        {
            pricePerDiamond = state.price,
            remainingDiamonds = Math.Max(
                0,
                maxDiamondsPer24Hours -
                data.exchangedDiamonds),
            nextUpdateUtc =
                new DateTime(
                    state.nextUpdateTicks,
                    DateTimeKind.Utc)
        };

        return true;
    }

    public ExchangeResult TryExchange(
        int planetId,
        int diamonds,
        double incomePerSecond,
        Func<int, double, int, bool> commitTransaction,
        out double spentCurrency)
    {
        spentCurrency = 0;

        if (diamonds <= 0 || commitTransaction == null)
            return ExchangeResult.InvalidAmount;

        if (!TryGetQuote(
                planetId, incomePerSecond, out var quote))
        {
            return ExchangeResult.PlanetNotConfigured;
        }

        if (diamonds > quote.remainingDiamonds)
            return ExchangeResult.DailyLimitReached;

        double cost = Math.Ceiling(
            quote.pricePerDiamond * diamonds);

        if (!IsValidPrice(cost))
            return ExchangeResult.InvalidRate;

        // The callback must atomically spend planet
        // currency and grant global diamonds.
        bool committed = commitTransaction(
            planetId, cost, diamonds);

        if (!committed)
            return ExchangeResult.WalletRejected;

        if (data.windowStartTicks == 0)
            data.windowStartTicks = DateTime.UtcNow.Ticks;

        data.exchangedDiamonds += diamonds;
        spentCurrency = cost;

        Save();

        return ExchangeResult.Success;
    }

    private double CalculatePrice(
        PlanetSettings settings,
        double incomePerSecond)
    {
        double growth = Math.Max(
            1d,
            incomePerSecond / settings.baseIncomePerSecond);

        return Math.Ceiling(
            settings.baseDiamondPrice *
            Math.Pow(growth, inflationPower));
    }

    private bool RefreshDailyLimit(DateTime now)
    {
        if (data.windowStartTicks == 0)
            return false;

        long elapsed = now.Ticks - data.windowStartTicks;

        if (elapsed < TimeSpan.FromHours(24).Ticks)
            return false;

        data.windowStartTicks = 0;
        data.exchangedDiamonds = 0;

        return true;
    }

    private static bool IsValidPrice(double price)
    {
        return price > 0 &&
               !double.IsNaN(price) &&
               !double.IsInfinity(price);
    }

    private void Save()
    {
        PlayerPrefs.SetString(
            SaveKey,
            JsonUtility.ToJson(data));

        PlayerPrefs.Save();
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, "");

        try
        {
            data = string.IsNullOrEmpty(json)
                ? new SaveData()
                : JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception)
        {
            data = new SaveData();
        }

        if (data == null)
            data = new SaveData();

        if (data.rates == null)
            data.rates = new List<RateState>();
    }
}

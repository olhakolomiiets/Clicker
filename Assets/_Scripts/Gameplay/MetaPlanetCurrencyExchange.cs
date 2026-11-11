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
        public Sprite currencyIcon;
    }

    public struct ExchangeQuote
    {
        public int planetId;
        public double pricePerDiamond;
    }

    public enum ExchangeResult
    {
        Success,
        NotInitialized,
        InvalidAmount,
        PlanetNotConfigured,
        InvalidRate,
        QuoteChanged,
        WalletRejected,
        TransactionFailed
    }

    [Header("Planet currencies")]
    [SerializeField] private List<PlanetSettings> planets = new List<PlanetSettings>();

    [Header("Exchange settings")]
    [SerializeField, Range(1f, 2f)] private float inflationPower = 1.15f;
    private readonly Dictionary<int, double> _openRates = new();
    private bool _isOpen;
    private bool isInitialized;
    public bool IsInitialized => isInitialized;
    public bool IsOpen => _isOpen;

    public bool Initialize()
    {
        isInitialized = false;

        try
        {
            ValidateConfiguration();

            _openRates.Clear();
            _isOpen = false;

            isInitialized = true;
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return false;
        }
    }

    public void OpenExchange()
    {
        if (_isOpen)
            return;

        _openRates.Clear();
        _isOpen = isInitialized;
    }

    public void CloseExchange()
    {
        _openRates.Clear();
        _isOpen = false;
    }

    public bool SetRate(int planetId, double incomePerSecond)
    {
        if (!isInitialized || !_isOpen || _openRates.ContainsKey(planetId) || !IsFiniteNonNegative(incomePerSecond))
            return false;

        PlanetSettings settings = planets.Find(
            p => p != null && p.planetId == planetId);

        if (settings == null)
            return false;

        double price = CalculatePrice(settings, incomePerSecond);

        if (!IsValidPrice(price))
            return false;

        _openRates.Add(planetId, price);
        return true;
    }


    public bool TryGetQuote(int planetId, out ExchangeQuote quote)
    {
        quote = default;

        if (!isInitialized || !_isOpen ||
            !_openRates.TryGetValue(planetId, out double price))
            return false;

        quote = new ExchangeQuote
        {
            planetId = planetId,
            pricePerDiamond = price
        };

        return true;
    }

    public Sprite GetCurrencyIcon(int planetId)
    {
        PlanetSettings settings = planets.Find(
            p => p != null && p.planetId == planetId);

        return settings != null ? settings.currencyIcon : null;
    }

    public ExchangeResult TryExchange(ExchangeQuote approvedQuote, int diamonds, Func<int, double, int, bool> commitTransaction, out double spentCurrency)
    {
        spentCurrency = 0d;

        if (!isInitialized || !_isOpen)
            return ExchangeResult.NotInitialized;

        if (diamonds <= 0 || commitTransaction == null)
            return ExchangeResult.InvalidAmount;

        if (!TryGetQuote(approvedQuote.planetId, out ExchangeQuote currentQuote))
            return ExchangeResult.PlanetNotConfigured;

        if (approvedQuote.pricePerDiamond != currentQuote.pricePerDiamond)
            return ExchangeResult.QuoteChanged;

        double cost = Math.Ceiling(
            currentQuote.pricePerDiamond * diamonds);

        if (!IsValidPrice(cost))
            return ExchangeResult.InvalidRate;

        try
        {
            if (!commitTransaction(currentQuote.planetId, cost, diamonds))
                return ExchangeResult.WalletRejected;

            spentCurrency = cost;
            return ExchangeResult.Success;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            isInitialized = false;
            return ExchangeResult.TransactionFailed;
        }
    }

    private double CalculatePrice(PlanetSettings settings, double incomePerSecond)
    {
        double growth = Math.Max(1d, incomePerSecond / settings.baseIncomePerSecond);

        return Math.Ceiling(settings.baseDiamondPrice * Math.Pow(growth, inflationPower));
    }

    private void ValidateConfiguration()
    {
        if (float.IsNaN(inflationPower) || float.IsInfinity(inflationPower) || inflationPower < 1f || inflationPower > 2f || planets == null || planets.Count == 0)
        {
            throw new InvalidOperationException("Exchange Inspector configuration is invalid.");
        }

        var knownIds = new HashSet<int>();
        foreach (PlanetSettings settings in planets)
        {
            if (settings == null || !knownIds.Add(settings.planetId) || !IsValidPrice(settings.baseIncomePerSecond) || !IsValidPrice(settings.baseDiamondPrice))
            {
                throw new InvalidOperationException("Planet exchange configuration is invalid.");
            }
        }
    }

    private static bool IsFiniteNonNegative(double value)
    {
        return value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsValidPrice(double price)
    {
        return price > 0 && !double.IsNaN(price) && !double.IsInfinity(price);
    }
}

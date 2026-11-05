using UnityEngine;
using System;
using System.Collections.Generic;

public class MetaPlanetMode : MonoBehaviour, IPlanetMode
{
    [SerializeField] private MetaGameRules _metaGameRules;
    [SerializeField] private MetaPlanetManager _metaPlanetManager;
    [SerializeField] private CurrencyUI _currencyUI;
    [SerializeField] private LevelController _levelController;
    [Header("Meta Planet Currency Exchange")]
    [SerializeField] private MetaPlanetCurrencyExchange _currencyExchange;

    private GeneralGameData _generalGameData;
    private SaveSystem _exchangeSaveSystem;
    private OfflinePlanetCatalog _exchangeCatalog;
    private MetaUpgradeEffects _exchangeEffects;
    private bool _isInitialized;
    private bool _isLevelControllerSubscribed;
    public bool IsCurrencyExchangeReady =>
    _isInitialized &&
    _currencyExchange != null &&
    _currencyExchange.IsInitialized &&
    _exchangeSaveSystem != null &&
    _exchangeCatalog != null &&
    _exchangeEffects != null;

    private void OnEnable()
    {
        SubscribeLevelController();
    }

    public void ConfigureCurrencyExchange(SaveSystem saveSystem, OfflinePlanetCatalog catalog)
    {
        _exchangeSaveSystem = saveSystem;
        _exchangeCatalog = catalog;
    }

    public void Initialize(GeneralGameData generalGameData)
    {
        if (_isInitialized)
            return;

        _generalGameData = generalGameData;

        ConnectSystems();

        _metaGameRules.PrepareData(_generalGameData);
        _metaPlanetManager.Initialize(_generalGameData);

        if (_currencyExchange == null || _exchangeSaveSystem == null || _exchangeCatalog == null || _exchangeCatalog.Planets == null || _exchangeCatalog.MetaUpgradeConfigs == null || _exchangeCatalog.MetaUpgradeConfigs.Count == 0)
        {
            Debug.LogError(
                "[CurrencyExchange] Missing component, SaveSystem or catalog.",
                this);
        }
        else
        {
            _exchangeEffects = new MetaUpgradeEffects();

            if (!_currencyExchange.Initialize())
            {
                Debug.LogError(
                    "[CurrencyExchange] Initialization failed.",
                    this);
            }
        }

        _isInitialized = true;
    }

    public List<int> GetExchangeAvailablePlanetIds()
    {
        var result = new List<int>();

        if (!IsCurrencyExchangeReady || _generalGameData == null)
            return result;

        foreach (OfflinePlanetEntry entry in _exchangeCatalog.Planets)
        {
            if (entry == null || entry.SceneBuildIndex <= 0 || string.IsNullOrWhiteSpace(entry.SaveKey) || !IsPlanetUnlocked(entry) || !PlayerPrefs.HasKey(entry.SaveKey))
                continue;

            result.Add(entry.SceneBuildIndex);
        }

        return result;
    }

    public List<int> OpenCurrencyExchange()
    {
        var availablePlanets = new List<int>();

        if (!IsCurrencyExchangeReady)
            return availablePlanets;


        if (_currencyExchange.IsOpen)
        {
            foreach (int planetId in GetExchangeAvailablePlanetIds())
            {
                if (_currencyExchange.TryGetQuote(planetId, out _))
                    availablePlanets.Add(planetId);
            }

            return availablePlanets;
        }

        _currencyExchange.OpenExchange();
        _exchangeEffects.Rebuild(_exchangeCatalog.MetaUpgradeConfigs);


        foreach (int planetId in GetExchangeAvailablePlanetIds())
        {
            if (!TryReadExchangePlanet(planetId, out OfflinePlanetEntry entry, out GameDataSave planetSave, out string error))
            {
                Debug.LogWarning($"[CurrencyExchange] Planet {planetId}: {error}");
                continue;
            }

            if (!OfflinePlanetIncomeCalculator.TryCalculate(entry, planetSave, _exchangeEffects, out double moneyPerSecond, out error))
            {
                Debug.LogWarning($"[CurrencyExchange] Planet {planetId}: {error}");
                continue;
            }

            if (_currencyExchange.SetRate(planetId, moneyPerSecond))
                availablePlanets.Add(planetId);
        }


        return availablePlanets;
    }

    public void CloseCurrencyExchange()
    {
        _currencyExchange?.CloseExchange();
    }

    public bool TryGetCurrencyExchangeQuote(int planetId, out MetaPlanetCurrencyExchange.ExchangeQuote quote, out double planetMoney, out string error)
    {
        quote = default;
        planetMoney = 0d;

        if (!TryReadExchangePlanet(planetId, out _, out GameDataSave planetSave, out error))
            return false;

        planetMoney = planetSave.Money;

        if (!_currencyExchange.TryGetQuote(planetId, out quote))
        {
            error = "Could not calculate the exchange rate.";
            return false;
        }

        return true;
    }


    private bool TryReadExchangePlanet(
        int planetId,
        out OfflinePlanetEntry entry,
        out GameDataSave save,
        out string error)
    {
        entry = null;
        save = default;
        error = null;

        if (!IsCurrencyExchangeReady || _generalGameData == null || _exchangeCatalog.Planets == null)
        {
            error = "Currency exchange is not ready.";
            return false;
        }

        entry = _exchangeCatalog.Planets.Find(
            p => p != null && p.SceneBuildIndex == planetId);

        if (entry == null || entry.SceneBuildIndex <= 0 || string.IsNullOrWhiteSpace(entry.SaveKey) || !IsPlanetUnlocked(entry))
        {
            error = "Planet is unavailable for exchange.";
            return false;
        }

        if (!OfflinePlanetSaveReader.TryRead(entry.SaveKey, out save, out error))
            return false;

        if (!IsFinite(save.Money) || save.Money < 0d)
        {
            error = "Saved planet balance is invalid.";
            return false;
        }

        return true;
    }


    public MetaPlanetCurrencyExchange.ExchangeResult TryExchangeCurrency(
        MetaPlanetCurrencyExchange.ExchangeQuote approvedQuote,
        int diamonds,
        out double spentCurrency,
        out string error)
    {
        spentCurrency = 0d;
        error = null;

        if (!IsCurrencyExchangeReady)
        {
            error = "Currency exchange is not initialized.";
            return MetaPlanetCurrencyExchange.ExchangeResult.NotInitialized;
        }


        string callbackError = null;

        var result = _currencyExchange.TryExchange(
            approvedQuote,
            diamonds,
            (planetId, cost, amount) =>
                CommitCurrencyExchange(planetId, cost, amount, out callbackError),
            out spentCurrency);

        if (result == MetaPlanetCurrencyExchange.ExchangeResult.Success)
        {
            _metaGameRules.SendDataUpdate();
        }
        else
        {
            error = callbackError ?? result.ToString();
        }

        return result;

    }

    private bool CommitCurrencyExchange(int planetId, double cost, int diamonds, out string error)
    {
        error = null;

        if (!IsFinite(cost) ||
            cost <= 0d ||
            diamonds <= 0)
        {
            error = "Invalid exchange transaction.";
            return false;
        }

        if (!TryReadExchangePlanet(planetId, out OfflinePlanetEntry entry, out GameDataSave planetSave, out error))
        {
            return false;
        }

        if (planetSave.Money < cost)
        {
            error = "Not enough planet currency.";
            return false;
        }

        double updatedMoney = planetSave.Money - cost;
        double updatedDiamonds = _generalGameData.Diamonds + diamonds;

        // Prevent invalid or unrepresentable double operations.
        if (!IsFinite(updatedMoney) || updatedMoney < 0d || updatedMoney >= planetSave.Money || !IsFinite(_generalGameData.Diamonds) || _generalGameData.Diamonds < 0d || !IsFinite(updatedDiamonds) || updatedDiamonds <= _generalGameData.Diamonds)
        {
            error = "Currency balances cannot represent this exchange.";
            return false;
        }

        planetSave.Money = updatedMoney;

        GeneralGameDataSave generalSave =
            JsonUtility.FromJson<GeneralGameDataSave>(
                _generalGameData.GetSaveData());

        generalSave.Diamonds = updatedDiamonds;
        string planetJson = JsonUtility.ToJson(planetSave);
        string generalJson = JsonUtility.ToJson(generalSave);

        try
        {
            _exchangeSaveSystem.SaveCurrencyExchange(entry.SaveKey, new List<string> { planetJson }, new List<string> { generalJson });
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            try
            {
                _exchangeSaveSystem.RecoverPendingTransactions();

                List<string> saved = _exchangeSaveSystem.LoadGame();

                bool generalSaved = saved != null && saved.Count > 0 && saved[0] == generalJson;

                bool planetSaved = OfflinePlanetSaveReader.TryRead(entry.SaveKey, out GameDataSave savedPlanet, out _) &&
                    savedPlanet.Money == updatedMoney;

                if (generalSaved && planetSaved)
                {
                    _generalGameData.Diamonds = updatedDiamonds;
                    return true;
                }

                if (generalSaved != planetSaved)
                {
                    throw new InvalidOperationException("Currency exchange save is inconsistent.");
                }

                error = "Currency exchange was not saved.";
                return false;
            }
            catch (Exception recoveryException)
            {
                Debug.LogException(recoveryException, this);
                throw;
            }
        }

        _generalGameData.Diamonds = updatedDiamonds;
        return true;
    }

    private bool IsPlanetUnlocked(OfflinePlanetEntry entry)
    {
        return entry.SceneBuildIndex == 1 ||
               _generalGameData.ActivePlanet > entry.SceneBuildIndex;
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) &&
               !double.IsInfinity(value);
    }


    public void UpdateData()
    {
        _metaGameRules.SendDataUpdate();
    }

    public void Load(string data)
    {
        // Meta progress пока не сохраняется отдельно.
    }

    public string Save()
    {
        return string.Empty;
    }

    private void ConnectSystems()
    {
        _metaGameRules.OnUpdateGameData += _currencyUI.UpdateCurrency;
    }

    private void OnDisable()
    {
        CloseCurrencyExchange();
        UnsubscribeLevelController();

        if (!_isInitialized)
            return;

        _metaGameRules.OnUpdateGameData -= _currencyUI.UpdateCurrency;
    }

    private void SubscribeLevelController()
    {
        if (_isLevelControllerSubscribed)
            return;

        if (_metaGameRules == null)
        {
            Debug.LogWarning("MetaPlanetMode cannot subscribe LevelController because MetaGameRules is missing.", this);
            return;
        }

        if (_levelController == null)
        {
            Debug.LogWarning("MetaPlanetMode cannot subscribe LevelController because the reference is missing.", this);
            return;
        }

        _metaGameRules.OnUpdateGameData += _levelController.PrepareGameData;
        _isLevelControllerSubscribed = true;
    }

    private void UnsubscribeLevelController()
    {
        if (!_isLevelControllerSubscribed)
            return;

        if (_metaGameRules != null && _levelController != null)
            _metaGameRules.OnUpdateGameData -= _levelController.PrepareGameData;

        _isLevelControllerSubscribed = false;
    }

    public void AddPurchasedPack(double coins, double diamonds)
    {
        _metaGameRules.GetPurchasedProduct(coins, diamonds);
    }

    public void AddPurchasedDiamonds(double diamonds)
    {
        _metaGameRules.GetPurchasedProduct(diamonds);
    }

    public void ActivatePurchasedBooster()
    {
        // meta booster пока не нужен
    }
}

using UnityEngine;

public class MetaPlanetMode : MonoBehaviour, IPlanetMode
{
    [SerializeField] private MetaGameRules _metaGameRules;
    [SerializeField] private MetaPlanetManager _metaPlanetManager;
    [SerializeField] private CurrencyUI _currencyUI;

    private GeneralGameData _generalGameData;
    private bool _isInitialized;

    public void Initialize(GeneralGameData generalGameData)
    {
        if (_isInitialized)
            return;

        _generalGameData = generalGameData;

        ConnectSystems();

        _metaGameRules.PrepareData(_generalGameData);
        _metaPlanetManager.Initialize(_generalGameData);

        _isInitialized = true;
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
        if (!_isInitialized)
            return;

        _metaGameRules.OnUpdateGameData -= _currencyUI.UpdateCurrency;
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
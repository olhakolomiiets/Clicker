using UnityEngine;

public class MetaPlanetMode : MonoBehaviour, IPlanetMode
{
    [SerializeField] private MetaGameRules _metaGameRules;
    [SerializeField] private MetaPlanetManager _metaPlanetManager;
    [SerializeField] private CurrencyUI _currencyUI;
    [SerializeField] private LevelController _levelController;

    private GeneralGameData _generalGameData;
    private bool _isInitialized;
    private bool _isLevelControllerSubscribed;

    private void OnEnable()
    {
        SubscribeLevelController();
    }

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

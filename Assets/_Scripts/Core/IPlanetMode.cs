public interface IPlanetMode
{
    void Initialize(GeneralGameData generalGameData);
    void UpdateData();
    void Load(string data);
    string Save();

    void AddPurchasedPack(double coins, double diamonds);
    void AddPurchasedDiamonds(double diamonds);
    void ActivatePurchasedBooster();
}
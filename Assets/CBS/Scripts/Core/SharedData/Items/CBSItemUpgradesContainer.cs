using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSItemUpgradesContainer
    {
        public Dictionary<string, List<CBSItemUpgradeState>> Upgrades;

        public bool HasUpgrade(string itemID)
        {
            if (Upgrades == null)
                return false;
            return Upgrades.ContainsKey(itemID);
        }

        public List<CBSItemUpgradeState> GetUpdgrades(string itemID)
        {
            if (Upgrades == null)
                return null;
            try
            {
                return Upgrades[itemID];
            }
            catch
            {
                return null;
            }
        }

        public void AddOrUpdateUpgradeInfo(string itemID, List<CBSItemUpgradeState> upgradeInfo)
        {
            if (Upgrades == null)
                Upgrades = new Dictionary<string, List<CBSItemUpgradeState>>();
            Upgrades[itemID] = upgradeInfo;
        }

        public void RemoveUpgrade(string itemID)
        {
            if (Upgrades == null || string.IsNullOrEmpty(itemID))
                return;
            if (Upgrades.ContainsKey(itemID))
            {
                Upgrades.Remove(itemID);
            }
        }
        
        public void Duplicate(string fromItemID, string toItemID)
        {
            if (Upgrades == null)
                return;
            if (Upgrades.TryGetValue(fromItemID, out var upgradeData))
            {
                Upgrades[toItemID] = upgradeData;
            }
        }
    }
}

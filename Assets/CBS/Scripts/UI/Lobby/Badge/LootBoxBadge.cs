using CBS.Models;

namespace CBS.UI
{
    public class LootBoxBadge : BaseBadge
    {
        private ICBSInventory CBSInventory { get; set; }

        private void Awake()
        {
            CBSInventory = CBSModule.Get<CBSInventoryModule>();
            // try get from cache
            var badgeCount = CBSInventory.GetLootboxesBadgeFromCache().Count;
            UpdateCount(badgeCount);
        }

        private void OnEnable()
        {
            CBSInventory.OnLootboxAdded += OnLootBoxAdded;
            CBSInventory.OnLootboxOpen += OnLootBoxOpen;
            GetLootboxesBadge();
        }

        private void OnDisable()
        {
            CBSInventory.OnLootboxAdded -= OnLootBoxAdded;
            CBSInventory.OnLootboxOpen -= OnLootBoxOpen;
        }

        private void GetLootboxesBadge()
        {
            CBSInventory.GetLootboxesBadge(OnGetBadge);
        }

        private void OnGetBadge(CBSBadgeResult result)
        {
            if (result.IsSuccess)
            {
                var badgeCount = result.Count;
                UpdateCount(badgeCount);
            }
            else
            {
                UpdateCount(0);
            }
        }

        private void OnLootBoxAdded(CBSInventoryItem lootbox)
        {
            var lootBoxes = CBSInventory.GetLootboxesFromCache().Lootboxes;
            UpdateCount(lootBoxes == null ? 0 : lootBoxes.Count);
        }

        private void OnLootBoxOpen(LootboxBundle result)
        {
            GetLootboxesBadge();
        }
    }
}

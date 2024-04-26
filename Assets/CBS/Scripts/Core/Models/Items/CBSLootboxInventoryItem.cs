using System;
using System.Collections.Generic;
using CBS.SharedData.Lootbox;
using PlayFab.ClientModels;

namespace CBS
{
    public class CBSLootboxInventoryItem : CBSInventoryItem
    {
        public bool LockedByTimer => GetLeftSecondsToUnlock() > 0;
        public DateTime? UnlockDate { get; private set; }
        public List<DropSlot> RewardSlots { get; private set; }

        private LootBoxEntity Entity { get; set; }
        private bool ForceUnlocked { get; set; }

        public CBSLootboxInventoryItem(ItemInstance inventoryItem, CBSBaseItem baseItem) : base(inventoryItem, baseItem)
        {
            var cbsLootbox = baseItem as CBSLootbox;
            if (cbsLootbox == null)
                return;
            var lootboxTable = cbsLootbox.LootboxTable;
            var lootEntity = lootboxTable.GetDropEntityForItem(ItemID);
            if (lootEntity == null)
                return;
            bool hasTimerData = inventoryItem.CustomData != null && inventoryItem.CustomData.Count > 0 && inventoryItem.CustomData.ContainsKey(ItemDataKeys.TimerUnlockKey);
            ForceUnlocked = hasTimerData ? bool.Parse(inventoryItem.CustomData[ItemDataKeys.TimerUnlockKey]) : false;
            RewardSlots = lootEntity.Slots;
            Entity = lootEntity;
            var timerLocked = lootEntity.TimerLocked;
            if (timerLocked)
            {
                var lockedSeconds = lootEntity.LockSeconds;
                if (PurchaseDate != null)
                {
                    UnlockDate = PurchaseDate.GetValueOrDefault().AddSeconds(lockedSeconds);
                }
            }
        }

        public int GetLeftSecondsToUnlock()
        {
            if (ForceUnlocked)
                return 0;
            if (UnlockDate == null)
                return 0;
            var dateNow = DateTime.UtcNow;
            var timerSpan = UnlockDate - dateNow;
            return timerSpan.GetValueOrDefault().Seconds;
        }
    }
}
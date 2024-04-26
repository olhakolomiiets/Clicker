namespace CBS
{
    public static class ItemDataKeys
    {
        public const string InventoryEquippedKey = "IsEquipped";
        public const string InventoryTradeKey = "IsInTrading";
        public const string UpgradeIndexKey = "UpgradeIndex";
        public const string InventoryBaseDataKey = "InventoryBaseData";
        public const string TimerUnlockKey = "TimerUnlock";

        public static string[] DontCopyProperties = new string[]
        {
            InventoryEquippedKey
        };
    }
}

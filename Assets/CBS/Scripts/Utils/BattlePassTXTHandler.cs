namespace CBS.Utils
{
    public static class BattlePassTXTHandler
    {
        public const string PurchaseTitle = "Success Purchase";
        public const string PurchaseBody = "Congratulations! you have successfully purchased a ticket.";
        public const string TasksNotEnableTitle = "Tasks is not enabled for you. Purchase ticket to unlock tasks";
        public const string BankSlotTitle = "Available at ";

        public static string GetBankSlotTitle(int bankLevelIndex)
        {
            return BankSlotTitle + bankLevelIndex.ToString();
        }
    }
}

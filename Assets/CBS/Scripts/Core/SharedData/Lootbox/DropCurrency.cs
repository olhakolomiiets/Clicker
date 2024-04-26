using System.Collections.Generic;
using System.Linq;

namespace CBS.SharedData.Lootbox
{
    public class DropCurrency
    {
        public CurrencyDropType DropType;
        public string CurrencyCode;
        public List<CurrencyDropSlot> DropTable;
        public int Min;
        public int Max;

        public void AddToTable(int value)
        {
            if (DropTable == null)
                DropTable = new List<CurrencyDropSlot>();
            if (DropTable.Any(x=>x.Amount == value))
                return;
            DropTable.Add(new CurrencyDropSlot
            {
                Amount = value,
                Weight = 1
            });
        }

        public void RemoveFromTable(int value)
        {
            if (DropTable == null)
                return;
            if (DropTable.Any(x=>x.Amount == value))
            {
                var slot = DropTable.FirstOrDefault(x=>x.Amount == value);
                DropTable.Remove(slot);
            }
        }
    }
}
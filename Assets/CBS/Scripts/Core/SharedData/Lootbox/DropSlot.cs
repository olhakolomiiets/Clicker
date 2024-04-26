using System.Collections.Generic;

namespace CBS.SharedData.Lootbox
{
    public class DropSlot
    {
        public DropBehavior DropBehavior;
        public SlotDropType SlotDropType;
        public float DropChance;
        public List<DropItem> ItemsToDrop;
        public List<DropCurrency> CurrenciesToDrop;

        public void AddItem(DropItem item)
        {
            if (ItemsToDrop == null)
                ItemsToDrop = new List<DropItem>();
            ItemsToDrop.Add(item);
        }
        
        public void RemoveItem(DropItem item)
        {
            ItemsToDrop?.Remove(item);
        }
        
        public void AddCurrency(DropCurrency item)
        {
            if (CurrenciesToDrop == null)
                CurrenciesToDrop = new List<DropCurrency>();
            CurrenciesToDrop.Add(item);
        }

        public void RemoveCurrency(DropCurrency item)
        {
            CurrenciesToDrop?.Remove(item);
        }
    }
}
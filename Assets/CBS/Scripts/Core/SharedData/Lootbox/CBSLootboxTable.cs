using System.Collections.Generic;

namespace CBS.SharedData.Lootbox
{
    public class CBSLootboxTable
    {
        public Dictionary<string, LootBoxEntity> LootBoxDropTable;

        public LootBoxEntity GetDropEntityForItem(string itemID)
        {
            if (LootBoxDropTable == null)
                LootBoxDropTable = new Dictionary<string, LootBoxEntity>();
            LootBoxDropTable.TryAdd(itemID, new LootBoxEntity());
            var dropEntity = LootBoxDropTable[itemID];
            dropEntity.ItemID = itemID;
            return dropEntity;
        }

        public void RemoveEntity(string itemID)
        {
            if (LootBoxDropTable == null)
                return;
            if (LootBoxDropTable.ContainsKey(itemID))
                LootBoxDropTable.Remove(itemID);
        }

        public bool ContainItem(string itemID)
        {
            if (LootBoxDropTable == null)
                return false;
            var result = LootBoxDropTable.TryGetValue(itemID, out var lootbox);
            return result;
        }
    }
}
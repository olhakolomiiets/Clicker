using PlayFab.ClientModels;
using System.Collections.Generic;

namespace CBS.Models
{
    public class FunctionCraftFromRecipeResult
    {
        public ItemInstance CraftedInstance;
        public List<string> SpendedInstanesIDs;
        public Dictionary<string, uint> SpendedCurrencies;
        public Dictionary<string, uint> ConsumedItems;
    }
}

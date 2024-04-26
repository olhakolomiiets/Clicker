using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public interface IFabCrafting
    {
        void GetRecipeDependencyState(string profileID, string itemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void CraftItemFromRecipe(string profileID, string recipeInventoryID, Action<ExecuteFunctionResult> onCraft, Action<PlayFabError> onFailed);

        void CraftItemWithoutRecipe(string profileID, string recipeID, Action<ExecuteFunctionResult> onCraft, Action<PlayFabError> onFailed);

        void GetItemNextUpgradeState(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed);

        void UpgradeItemWithNextState(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onUpgrade, Action<PlayFabError> onFailed);
    }
}

using CBS.Models;
using PlayFab;
using PlayFab.CloudScriptModels;
using System;

namespace CBS.Playfab
{
    public class FabCrafting : FabExecuter, IFabCrafting
    {
        public void GetRecipeDependencyState(string profileID, string itemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetRecipeDependencyStateMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = itemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void CraftItemFromRecipe(string profileID, string recipeInventoryID, Action<ExecuteFunctionResult> onCraft, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.CraftItemFromRecipeMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = recipeInventoryID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onCraft, onFailed);
        }

        public void CraftItemWithoutRecipe(string profileID, string recipeID, Action<ExecuteFunctionResult> onCraft, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.CraftItemWithoutRecipeMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = recipeID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onCraft, onFailed);
        }

        public void GetItemNextUpgradeState(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onGet, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.GetItemNextUpgradeStateMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = inventoryItemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onGet, onFailed);
        }

        public void UpgradeItemWithNextState(string profileID, string inventoryItemID, Action<ExecuteFunctionResult> onUpgrade, Action<PlayFabError> onFailed)
        {
            var request = new ExecuteFunctionRequest
            {
                FunctionName = AzureFunctions.UpgradeItemWithNextStateMethod,
                FunctionParameter = new FunctionIDRequest
                {
                    ProfileID = profileID,
                    ID = inventoryItemID
                }
            };
            PlayFabCloudScriptAPI.ExecuteFunction(request, onUpgrade, onFailed);
        }
    }
}

using CBS.Models;
using PlayFab;
using PlayFab.ClientModels;

public class CBSError
{
    public PlayFabErrorCode FabCode;
    public ErrorCode CBSCode;
    public string Message;

    public bool Exist()
    {
        return !string.IsNullOrEmpty(Message);
    }

    public static CBSError FromTemplate(PlayFabError error)
    {
        return new CBSError
        {
            FabCode = error.Error,
            Message = error.ErrorMessage
        };
    }

    public static CBSError FromTemplate(ScriptExecutionError error)
    {
        return new CBSError
        {
            CBSCode = ErrorCode.UNKNOWN,
            FabCode = PlayFabErrorCode.CloudScriptAPIRequestError,
            Message = error.Message
        };
    }

    public static CBSError FromTemplate(PlayFab.CloudScriptModels.FunctionExecutionError error)
    {
        return new CBSError
        {
            CBSCode = ErrorCode.UNKNOWN,
            FabCode = PlayFabErrorCode.CloudScriptAPIRequestError,
            Message = error.Message,
        };
    }

    public static CBSError FailedToGrandPack()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.UNKNOWN,
            FabCode = PlayFabErrorCode.ItemNotFound,
            Message = "Failed to grand currency pack"
        };
    }

    public static CBSError CredentialNotFound()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.UNKNOWN,
            FabCode = PlayFabErrorCode.InvalidAuthToken,
            Message = "Credential Not Found"
        };
    }

    public static CBSError IAPInitializeFailed()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.IAP_INITIALIZE_FAILED,
            FabCode = PlayFabErrorCode.Unknown,
            Message = "Failed to initialize in app purchase module."
        };
    }

    public static CBSError IAPNotEnabledError()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.IAP_NOT_ENABLES,
            FabCode = PlayFabErrorCode.Unknown,
            Message = "CBS In App Purchase not enabled. Go to CBS Configurator and enable it."
        };
    }

    public static CBSError IAPNotInitializedError()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.IAP_NOT_ENABLES,
            FabCode = PlayFabErrorCode.Unknown,
            Message = "CBS App Purchase not initialized yet"
        };
    }

    public static CBSError IAPPurchaseError(string message)
    {
        return new CBSError
        {
            CBSCode = ErrorCode.IAP_PURCHASE_ERROR,
            FabCode = PlayFabErrorCode.Unknown,
            Message = "Failed to purchase item. " + message
        };
    }

    public static CBSError RecipeNotFoundError()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.RECIPE_NOT_FOUND,
            FabCode = PlayFabErrorCode.Unknown,
            Message = "Recipe not found"
        };
    }

    public static CBSError UpgradeNotFoundError()
    {
        return new CBSError
        {
            CBSCode = ErrorCode.ITEM_NOT_UPGRADABLE,
            FabCode = PlayFabErrorCode.Unknown,
            Message = "Upgrate data for this item not found"
        };
    }
}

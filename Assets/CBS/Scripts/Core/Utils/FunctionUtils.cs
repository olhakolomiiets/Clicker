using CBS.Models;
using PlayFab.CloudScriptModels;

namespace CBS.Utils
{
    public static class FunctionUtils
    {
        public static CBSError GetCBSError(this ExecuteFunctionResult result)
        {
            var fabError = result.Error;
            if (fabError != null)
            {
                return CBSError.FromTemplate(fabError);
            }
            var resultObj = result.FunctionResult ?? JsonPlugin.EMPTY_JSON;
            var rawData = resultObj.ToString();
            var cbsResult = JsonPlugin.FromJsonDecompress<BaseErrorResult>(rawData);
            if (cbsResult != null && cbsResult.Error != null && cbsResult.Error.Exist())
            {
                return cbsResult.Error;
            }
            return null;
        }

        public static T GetResult<T>(this ExecuteFunctionResult result)
        {
            var rawData = result.FunctionResult.ToString();
            return JsonPlugin.FromJsonDecompress<T>(rawData);
        }
    }
}

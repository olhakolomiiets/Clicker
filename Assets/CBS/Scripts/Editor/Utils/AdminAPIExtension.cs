#if ENABLE_PLAYFABADMIN_API && !DISABLE_PLAYFAB_STATIC_API
using PlayFab.AdminModels;
using PlayFab.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace PlayFab
{
    public static class AdminAPIExtension
    {
        /// <summary>
        /// Create a Azure Functions task, which can run a CloudScript on a schedule.
        /// </summary>
        public static void CreateAzureCloudScriptTask(CreateCloudScriptTaskRequest request, Action<CreateTaskResult> resultCallback, Action<PlayFabError> errorCallback, object customData = null, Dictionary<string, string> extraHeaders = null)
        {
            var context = (request == null ? null : request.AuthenticationContext) ?? PlayFabSettings.staticPlayer;
            var callSettings = PlayFabSettings.staticSettings;
            if (string.IsNullOrEmpty(callSettings.DeveloperSecretKey)) { throw new PlayFabException(PlayFabExceptionCode.DeveloperKeyNotSet, "Must set DeveloperSecretKey in settings to call this method"); }

            var playfabHttpType = typeof(PlayFabHttp);
            var methodInfo = playfabHttpType.GetMethod("MakeApiCall", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var genericInfo = methodInfo.MakeGenericMethod(typeof(CreateTaskResult));
            genericInfo.Invoke(null, new object[] { "/Admin/CreateCloudScriptAzureFunctionsTask", request, AuthType.DevSecretKey, resultCallback, errorCallback, customData, extraHeaders, context, callSettings, null });
        }
    }
}
#endif
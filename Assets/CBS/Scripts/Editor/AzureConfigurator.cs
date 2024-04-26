#if ENABLE_PLAYFABADMIN_API
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.AdminModels;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Action = System.Action;

namespace CBS.Editor
{
    public class AzureConfigurator : BaseConfigurator
    {
        protected override string Title => "Azure Congiguration";

        protected override bool DrawScrollView => true;

        private string StorageConnectionString { get; set; }
        private string FunctionMasterKey { get; set; }
        private string FunctionURL { get; set; }

        private string TitleEntityToken;

        private EditorData EditorData { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            GetApiKey();
        }

        protected override void OnDrawInside()
        {
            var levelTitleStyle = new GUIStyle(GUI.skin.label);
            levelTitleStyle.fontStyle = FontStyle.Bold;
            levelTitleStyle.fontSize = 12;
            // draw storage
            EditorGUILayout.LabelField("Storage Connection String", levelTitleStyle);
            GUILayout.BeginHorizontal();
            StorageConnectionString = EditorGUILayout.TextField(StorageConnectionString, new GUILayoutOption[] { GUILayout.Width(900) });
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12))
            {
                SaveStorageConnectionString(StorageConnectionString);
            }
            GUILayout.EndHorizontal();
            // draw master key
            EditorGUILayout.LabelField("Azure Master Key", levelTitleStyle);
            GUILayout.BeginHorizontal();
            FunctionMasterKey = EditorGUILayout.TextField(FunctionMasterKey, new GUILayoutOption[] { GUILayout.Width(900) });
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12))
            {
                SaveMasterKey(FunctionMasterKey);
            }
            GUILayout.EndHorizontal();
            // draw function URL
            EditorGUILayout.LabelField("Azure Function URL", levelTitleStyle);
            GUILayout.BeginHorizontal();
            FunctionURL = EditorGUILayout.TextField(FunctionURL, new GUILayoutOption[] { GUILayout.Width(900) });
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12))
            {
                SaveFunctionURL(FunctionURL);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            if (EditorUtils.DrawButton("Check health", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.Width(120) }))
            {
                ShowProgress();
                GetEntityToken(() =>
                {
                    var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                    {
                        FunctionName = AzureFunctions.CheckHealthMethod
                    };
                    PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet =>
                    {
                        var cbsError = OnGet.GetCBSError();
                        if (cbsError != null)
                        {
                            AddErrorLog(cbsError);
                            EditorUtility.DisplayDialog("Falied", cbsError.Message, "OK");
                            HideProgress();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Success", "Everything is set up correctly", "OK");
                            HideProgress();
                        }
                    }, OnFailed =>
                    {
                        if (OnFailed.ErrorMessage == "No function named CheckHealth was found to execute")
                        {
                            EditorUtility.DisplayDialog("Falied", "You playfab functions is not registered. To fix this - navigate to 'PlayFab' tab and click 'Register Azure Functions'", "OK");
                        }
                        else if (OnFailed.Error == PlayFabErrorCode.CloudScriptAzureFunctionsHTTPRequestError)
                        {
                            EditorUtility.DisplayDialog("Falied", "Your functions is not deployed. To fix it - deploy functions from VSCode", "OK");
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Falied", OnFailed.ErrorMessage, "OK");
                        }
                        AddErrorLog(OnFailed);
                        HideProgress();
                    });
                });
            }
        }

        private void GetApiKey(List<string> keyToLoad = null)
        {
            ShowProgress();
            var keys = new List<string>();
            if (keyToLoad == null)
            {
                keys.Add(TitleKeys.FunctionStorageConnectionStringKey);
                keys.Add(TitleKeys.FunctionMasterKey);
                keys.Add(TitleKeys.FunctionURLKey);
            }
            else
            {
                keys = keyToLoad;
            }
            var request = new GetTitleDataRequest
            {
                Keys = keys
            };
            PlayFabAdminAPI.GetTitleInternalData(request, OnInternalDataGetted, OnGetDataFailed);
        }

        private void OnInternalDataGetted(GetTitleDataResult result)
        {
            HideProgress();
            var dictionary = result.Data;
            bool storageExist = dictionary.ContainsKey(TitleKeys.FunctionStorageConnectionStringKey);
            StorageConnectionString = storageExist ? dictionary[TitleKeys.FunctionStorageConnectionStringKey] : StorageConnectionString;
            bool masterKeyExist = dictionary.ContainsKey(TitleKeys.FunctionMasterKey);
            FunctionMasterKey = masterKeyExist ? dictionary[TitleKeys.FunctionMasterKey] : FunctionMasterKey;
            bool functionsURLKeyExist = dictionary.ContainsKey(TitleKeys.FunctionURLKey);
            FunctionURL = functionsURLKeyExist ? dictionary[TitleKeys.FunctionURLKey] : FunctionURL;
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveStorageConnectionString(string storage)
        {
            ShowProgress();
            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.FunctionStorageConnectionStringKey,
                Value = storage
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveConnectionString, OnSetDataFailed);
        }

        private void SaveMasterKey(string masterKey)
        {
            ShowProgress();
            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.FunctionMasterKey,
                Value = masterKey
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveMasterKey, OnSetDataFailed);
        }

        private void SaveFunctionURL(string functionURL)
        {
            ShowProgress();
            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.FunctionURLKey,
                Value = functionURL
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveFunctionURL, OnSetDataFailed);
        }

        private void OnSaveFunctionURL(SetTitleDataResult result)
        {
            HideProgress();
            GetApiKey(new List<string> { TitleKeys.FunctionURLKey });
        }

        private void OnSaveMasterKey(SetTitleDataResult result)
        {
            HideProgress();
            GetApiKey(new List<string> { TitleKeys.FunctionMasterKey });
        }

        private void OnSaveConnectionString(SetTitleDataResult result)
        {
            HideProgress();
            GetApiKey(new List<string> { TitleKeys.FunctionStorageConnectionStringKey });
        }

        private void OnSetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void GetEntityToken(Action onGet)
        {
            var request = new PlayFab.AuthenticationModels.GetEntityTokenRequest();

            TitleEntityToken = null;

            PlayFabAuthenticationAPI.GetEntityToken(
                request,
                result =>
                {
                    TitleEntityToken = result.EntityToken;
                    onGet?.Invoke();
                },
                error =>
                {
                    AddErrorLog(error);
                    HideProgress();
                }
            );
        }
    }
}
#endif

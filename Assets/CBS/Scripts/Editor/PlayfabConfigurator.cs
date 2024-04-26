
#if ENABLE_PLAYFABADMIN_API
using CBS.Scriptable;
using PlayFab;
using PlayFab.AdminModels;
using PlayFab.CloudScriptModels;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Action = System.Action;

namespace CBS.Editor
{
    public class PlayfabConfigurator : BaseConfigurator
    {
        private readonly string PlayfabCloudScriptPath = "Assets/CBS/Scripts/ServerScript/PlayfabServer.txt";
        private readonly string PlayfabCustomCloudScriptPath = "Assets/CBS_External/ServerScript/CustomServerScript.txt";
        private readonly string PlayfabCustomCloudPath = "Assets/CBS_External/ServerScript";

        protected override string Title => "Playfab Congiguration";

        protected override bool DrawScrollView => true;

        private EditorData EditorData { get; set; }
        private PlayfabData PlayfabData { get; set; }
        private PlayFabConfigData PlayFabConfigData { get; set; }

        private string TitleEntityToken;
        private string FunctionMasterKey { get; set; }
        private string FunctionURL { get; set; }
        private string StorageConnectionString { get; set; }

        private string ProgressTitle { get; set; }
        private float ProgressValue { get; set; }
        private bool IsShowFunctionProgress { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            PlayfabData = CBSScriptable.Get<PlayfabData>();
            PlayFabConfigData = CBSScriptable.Get<PlayFabConfigData>();
        }

        protected override void OnDrawInside()
        {
            var levelTitleStyle = new GUIStyle(GUI.skin.label);
            levelTitleStyle.fontStyle = FontStyle.Bold;
            levelTitleStyle.fontSize = 12;

            EditorGUILayout.LabelField("Azure Functions", levelTitleStyle);

            GUILayout.Space(10);

            if (EditorUtils.DrawButton("Register Azure Functions", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.Width(250) }))
            {
                RegisterAzureFunctions();
            }

            GUILayout.Space(10);

            if (EditorUtils.DrawButton("Import Azure Functions Project", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.Width(250) }))
            {
                ImportAzureFunctionsProject();
            }

            if (IsShowFunctionProgress)
            {
                EditorUtility.DisplayProgressBar("Register Azure Functions", ProgressTitle, ProgressValue);
            }
            else
            {
                EditorUtility.ClearProgressBar();
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("External class to register azure functions", levelTitleStyle);
            PlayFabConfigData.ExternalClassToRegisterAzureFunction = EditorGUILayout.TextField(PlayFabConfigData.ExternalClassToRegisterAzureFunction, GUILayout.Width(300));

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Azure Project Path", levelTitleStyle);
            PlayFabConfigData.AzureProjectPath = EditorGUILayout.TextField(PlayFabConfigData.GetAzureProjectPath(), GUILayout.Width(300));

            PlayFabConfigData.Save();
        }

        private void ImportAzureFunctionsProject()
        {
            ShowProgress();
            try
            {
                ZipUtils.UnzipAzureProject();
                EditorUtility.DisplayDialog("Success", "Importing Azure Functions Project finished!", "Ok");
            }
            catch
            {
                EditorUtility.DisplayDialog("Failed", "Failed to import Azure Functions project!", "Ok");
            }
            HideProgress();
        }

        private void RegisterAzureFunctions()
        {
            var allMethods = AzureFunctions.AllMethods;

            ShowFunctionProgress("Playfab login", 0);

            GetEntityToken(() =>
            {
                // get azure data
                var keys = new List<string>();
                keys.Add(TitleKeys.FunctionMasterKey);
                keys.Add(TitleKeys.FunctionURLKey);
                keys.Add(TitleKeys.FunctionStorageConnectionStringKey);
                var request = new GetTitleDataRequest
                {
                    Keys = keys
                };
                PlayFabAdminAPI.GetTitleInternalData(request, OnInternalDataGetted =>
                {
                    var dictionary = OnInternalDataGetted.Data;
                    bool masterKeyExist = dictionary.ContainsKey(TitleKeys.FunctionMasterKey);
                    FunctionMasterKey = masterKeyExist ? dictionary[TitleKeys.FunctionMasterKey] : string.Empty;
                    bool functionsURLKeyExist = dictionary.ContainsKey(TitleKeys.FunctionURLKey);
                    FunctionURL = functionsURLKeyExist ? dictionary[TitleKeys.FunctionURLKey] : string.Empty;
                    bool storageConnectionExist = dictionary.ContainsKey(TitleKeys.FunctionStorageConnectionStringKey);
                    StorageConnectionString = storageConnectionExist ? dictionary[TitleKeys.FunctionStorageConnectionStringKey] : string.Empty;

                    if (!masterKeyExist || !functionsURLKeyExist)
                    {
                        HideFunctionsProgress();
                        EditorUtility.DisplayDialog("Falied", "There is something wrong with your Azure configurations", "OK");
                    }
                    else
                    {
                        RegisterFunctionLoop(0);
                    }

                }, OnGetDataFailed =>
                {
                    AddErrorLog(OnGetDataFailed);
                    HideFunctionsProgress();
                });
            });
        }

        private void RegisterFunctionLoop(int index)
        {
            var allQueues = AzureQueues.AllQueues;
            var allMethods = AzureFunctions.AllMethods;
            var titleID = PlayFabSettings.TitleId;
            var allCount = allQueues.Count + allMethods.Count;

            if (index >= allCount)
            {
                HideFunctionsProgress();
                EditorUtility.DisplayDialog("Success", "Register Azure Function finished!", "Ok");
            }
            else if (index >= allMethods.Count)
            {
                var functionName = allQueues[index - allMethods.Count];
                var queueContainer = AzureQueues.GetQueueContainerName(functionName);

                var registerRequest = new RegisterQueuedFunctionRequest
                {
                    FunctionName = functionName,
                    QueueName = queueContainer,
                    ConnectionString = StorageConnectionString,
                    AuthenticationContext = new PlayFabAuthenticationContext
                    {
                        EntityToken = TitleEntityToken,
                        PlayFabId = PlayFabSettings.TitleId
                    }
                };

                var progressValue = (float)index / (float)allCount;
                ShowFunctionProgress("Register Queue = " + functionName, progressValue);

                PlayFabCloudScriptAPI.RegisterQueuedFunction(registerRequest, onRegister =>
                {
                    index++;
                    RegisterFunctionLoop(index);
                }, onFailed =>
                {
                    HideFunctionsProgress();
                    Debug.LogError(onFailed.ErrorMessage);
                    OnCloudScriptFailed(onFailed);
                });
            }
            else
            {
                var functionName = allMethods[index];

                var registerRequest = new RegisterHttpFunctionRequest
                {
                    FunctionName = functionName,
                    FunctionUrl = AzureFunctions.GetFunctionFullURL(FunctionURL, functionName, FunctionMasterKey),
                    AuthenticationContext = new PlayFabAuthenticationContext
                    {
                        EntityToken = TitleEntityToken,
                        PlayFabId = PlayFabSettings.TitleId
                    }
                };

                var progressValue = (float)index / (float)allCount;
                ShowFunctionProgress("Register Function = " + functionName, progressValue);

                PlayFabCloudScriptAPI.RegisterHttpFunction(registerRequest, onRegister =>
                {
                    index++;
                    RegisterFunctionLoop(index);
                }, onFailed =>
                {
                    HideFunctionsProgress();
                    Debug.LogError(onFailed.ErrorMessage);
                    OnCloudScriptFailed(onFailed);
                });
            }
        }

        private void GetEntityToken(Action onGet)
        {
            var request = new PlayFab.AuthenticationModels.GetEntityTokenRequest { };
            PlayFabAuthenticationAPI.ForgetAllCredentials();

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
                    HideFunctionsProgress();
                }
            );
        }

        private string GetCloudScriptText()
        {
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(PlayfabCloudScriptPath);
            return textAsset == null ? string.Empty : textAsset.text;
        }

        private string GetCloudCustomScriptText()
        {
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(PlayfabCustomCloudScriptPath);
            return textAsset == null ? string.Empty : textAsset.text;
        }

        private void UploadCloudScript()
        {
            var scriptText = GetCloudScriptText();

            var cloudFile = new CloudScriptFile
            {
                FileContents = scriptText,
                Filename = "PlayfabServer"
            };

            var fileList = new List<CloudScriptFile>();
            fileList.Add(cloudFile);

            if (PlayfabData.EnableCustomCloudScript)
            {
                CreateCustomScriptIfNotExist();
                var customScriptText = GetCloudCustomScriptText();
                var customCloudFile = new CloudScriptFile
                {
                    FileContents = customScriptText,
                    Filename = "PlayfabCustomServer"
                };
                fileList.Add(customCloudFile);
            }

            var request = new UpdateCloudScriptRequest
            {
                Files = fileList,
                Publish = true
            };
            PlayFabAdminAPI.UpdateCloudScript(request, OnCloudScriptUpdated, OnCloudScriptFailed);

            ShowProgress();
        }

        private void OnCloudScriptUpdated(UpdateCloudScriptResult result)
        {
            HideProgress();
            EditorUtility.DisplayDialog("Success", "Upload cloud script finished!", "Ok");
        }

        private void OnCloudScriptFailed(PlayFabError error)
        {
            HideProgress();
            EditorUtility.DisplayDialog("PlayFab Error", "Failed to upload cloud script. " + error.ErrorMessage, "Ok");
        }

        private void CreateCustomScriptIfNotExist()
        {
            var pathExist = Directory.Exists(PlayfabCustomCloudPath);
            if (!pathExist)
            {
                var directory = Directory.CreateDirectory(PlayfabCustomCloudPath);
                AssetDatabase.Refresh();
            }

            var pathToAsset = PlayfabCustomCloudPath + "/" + "CustomServerScript.txt";
            var fileExists = File.Exists(pathToAsset);

            if (!fileExists)
            {
                var asset = Environment.NewLine + "// custom cloud script" + Environment.NewLine;
                File.WriteAllText(pathToAsset, asset);
                AssetDatabase.SaveAssets();

                AssetDatabase.Refresh();
            }
        }

        private void ShowFunctionProgress(string title, float progress)
        {
            ProgressValue = progress;
            ProgressTitle = title;
            ShowProgress();
            IsShowFunctionProgress = true;
        }

        private void HideFunctionsProgress()
        {
            IsShowFunctionProgress = false;
            HideProgress();
        }
    }
}
#endif

#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
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
    public class ChatConfigurator : BaseConfigurator
    {
        protected override string Title => "Chat Configurator";

        protected override bool DrawScrollView => true;
        private int SelectedToolBar { get; set; }

        private GUIStyle BoldStyle
        {
            get
            {
                var boldStyle = new GUIStyle(GUI.skin.label);
                boldStyle.fontStyle = FontStyle.Bold;
                return boldStyle;
            }
        }

        private ChatLocalConfig ChatLocalData { get; set; }
        private ChatConfigData ChatConfigData { get; set; }
        private EditorData EditorData { get; set; }
        private List<ProfileEntity> ModeratorsList { get; set; }
        private StickersIcons Icons { get; set; }
        private SerializedObject ChatColorTableProperty { get; set; }

        private string CurrentInputWord;
        private string CurrentInputModerator;
        private string TitleEntityToken;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            ChatLocalData = CBSScriptable.Get<ChatLocalConfig>();
            EditorData = CBSScriptable.Get<EditorData>();
            Icons = CBSScriptable.Get<StickersIcons>();
            ChatColorTableProperty = new SerializedObject(ChatLocalData);
            GetChatData();
        }

        protected override void OnDrawInside()
        {
            var lastSelectedBar = SelectedToolBar;
            SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, new string[] { "General", "Moderation", "Moderators", "Stickers" });
            if (ChatConfigData == null)
                return;
            switch (SelectedToolBar)
            {
                case 0:
                    DrawGeneral();
                    break;
                case 1:
                    DrawModerations();
                    break;
                case 2:
                    DrawModerators();
                    break;
                case 3:
                    DrawStickers();
                    break;
                default:
                    break;
            }
            if (SelectedToolBar != lastSelectedBar)
            {
                GUI.FocusControl(null);
                if (SelectedToolBar == 2)
                {
                    GetModetatorsList();
                }
                else
                {
                    ModeratorsList = null;
                    CurrentInputModerator = string.Empty;
                }
            }
        }

        private void DrawGeneral()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            // title
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("General options", titleStyle);
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
            {
                int option = EditorUtility.DisplayDialogComplex("Warning",
                                "All changes to the chat configuration are cached on the server for performance reasons. In order for the changes to take effect, you need to redeploy functions again",
                                "I understand. Save",
                                "Cancel",
                                string.Empty);
                switch (option)
                {
                    // ok.
                    case 0:
                        SaveChataData(ChatConfigData);
                        break;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // max message char
            int maxMessageLength = ChatConfigData.GetMaxCharCount();
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max char count per message", GUILayout.Width(200));
            maxMessageLength = EditorGUILayout.IntField(maxMessageLength, new GUILayoutOption[] { GUILayout.Width(100) });
            EditorGUILayout.LabelField("chars", GUILayout.Width(35));
            GUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("The maximum length of a message that the user can send", MessageType.Info);
            ChatConfigData.MaxCharCountPerMessage = maxMessageLength;
            GUILayout.Space(10);

            // general ttl
            EditorGUILayout.LabelField("General Chat TTL", titleStyle);
            ChatConfigData.GeneralChatTTL = (CBSTTL)EditorGUILayout.EnumPopup(ChatConfigData.GeneralChatTTL, new GUILayoutOption[] { GUILayout.Width(200) });
            if (ChatConfigData.GeneralChatTTL == CBSTTL.CUSTOM_VALUE)
            {
                GUILayout.Space(5);
                if (ChatConfigData.GeneralChatSecondsTTL == null)
                {
                    ChatConfigData.GeneralChatSecondsTTL = ChatConfigData.DefaultMessageTTL;
                }
                EditorGUILayout.LabelField("Life time in seconds");
                ChatConfigData.GeneralChatSecondsTTL = EditorGUILayout.IntField((int)ChatConfigData.GeneralChatSecondsTTL, GUILayout.Width(200));

                var dontRemoveCount = ChatConfigData.GetDontRemoveCount(ChatAccess.GROUP);
                EditorGUILayout.LabelField("Do not delete the last N messages");
                ChatConfigData.GeneralChatDontRemoveCount = EditorGUILayout.IntField(dontRemoveCount, GUILayout.Width(200));
            }
            EditorGUILayout.HelpBox("The lifetime of messages on the server of general chats (Global, Regional, Clan ect). Limiting helps optimize azure cost.", MessageType.Info);
            GUILayout.Space(10);

            // private ttl
            EditorGUILayout.LabelField("Private Chat TTL", titleStyle);
            ChatConfigData.PrivateChatTTL = (CBSTTL)EditorGUILayout.EnumPopup(ChatConfigData.PrivateChatTTL, new GUILayoutOption[] { GUILayout.Width(200) });
            if (ChatConfigData.PrivateChatTTL == CBSTTL.CUSTOM_VALUE)
            {
                GUILayout.Space(5);
                if (ChatConfigData.PrivateChatSecondsTTL == null)
                {
                    ChatConfigData.PrivateChatSecondsTTL = ChatConfigData.DefaultMessageTTL;
                }
                EditorGUILayout.LabelField("Life time in seconds");
                ChatConfigData.PrivateChatSecondsTTL = EditorGUILayout.IntField((int)ChatConfigData.PrivateChatSecondsTTL, GUILayout.Width(200));

                var dontRemoveCount = ChatConfigData.GetDontRemoveCount(ChatAccess.PRIVATE);
                EditorGUILayout.LabelField("Do not delete the last N messages");
                ChatConfigData.PrivateChatDontRemoveCount = EditorGUILayout.IntField(dontRemoveCount, GUILayout.Width(200));
            }
            EditorGUILayout.HelpBox("The lifetime of messages on the server of private chats. Limiting helps optimize azure cost.", MessageType.Info);
            GUILayout.Space(10);

            // visual
            EditorGUILayout.LabelField("Visual", titleStyle);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Show Date ?");
            ChatLocalData.ShowDate = EditorGUILayout.Toggle(ChatLocalData.ShowDate);
            EditorGUILayout.HelpBox("Display the creation date of messages in chat.", MessageType.Info);
            GUILayout.Space(10);

            // color option
            EditorGUILayout.LabelField("Bubble mode text color");
            var serializeObject = new SerializedObject(ChatLocalData);
            var bubbleTextColor = serializeObject.FindProperty("BubbleTextColor");
            EditorGUILayout.PropertyField(bubbleTextColor);
            serializeObject.ApplyModifiedProperties();
            GUILayout.Space(10);

            EditorGUILayout.LabelField("System text color");
            serializeObject = new SerializedObject(ChatLocalData);
            var systemTextColor = serializeObject.FindProperty("SystemTextColor");
            EditorGUILayout.PropertyField(systemTextColor);
            serializeObject.ApplyModifiedProperties();
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Tag color");
            serializeObject = new SerializedObject(ChatLocalData);
            var tagTextColor = serializeObject.FindProperty("TagTextColor");
            EditorGUILayout.PropertyField(tagTextColor);
            serializeObject.ApplyModifiedProperties();
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Owner bubble color");
            serializeObject = new SerializedObject(ChatLocalData);
            var bubbleColor = serializeObject.FindProperty("OwnerBubbleColor");
            EditorGUILayout.PropertyField(bubbleColor);
            serializeObject.ApplyModifiedProperties();
            GUILayout.Space(10);

            EditorGUILayout.LabelField("System bubble color");
            serializeObject = new SerializedObject(ChatLocalData);
            var systemBubbleColor = serializeObject.FindProperty("SystemBubbleColor");
            EditorGUILayout.PropertyField(systemBubbleColor);
            serializeObject.ApplyModifiedProperties();
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Use unique color for profile ?");
            ChatLocalData.UseUniqueColor = EditorGUILayout.Toggle(ChatLocalData.UseUniqueColor);
            EditorGUILayout.HelpBox("Each chat profile will have its own unique message color.", MessageType.Info);
            if (ChatLocalData.UseUniqueColor)
            {
                var colorTable = ChatColorTableProperty.FindProperty("ColorTable");
                EditorGUILayout.PropertyField(colorTable);
                ChatColorTableProperty.ApplyModifiedProperties();
                ChatColorTableProperty.Update();
            }
            else
            {
                serializeObject = new SerializedObject(ChatLocalData);
                var defaultTextColor = serializeObject.FindProperty("DefaultTextColor");
                EditorGUILayout.PropertyField(defaultTextColor);
                serializeObject.ApplyModifiedProperties();
            }
            GUILayout.Space(10);

            ChatLocalData.Save();
        }

        private void DrawModerations()
        {
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enable automatic moderation ?");
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
            {
                int option = EditorUtility.DisplayDialogComplex("Warning",
                                "All changes to the chat configuration are cached on the server for performance reasons. In order for the changes to take effect, you need to redeploy functions again",
                                "I understand. Save",
                                "Cancel",
                                string.Empty);
                switch (option)
                {
                    // ok.
                    case 0:
                        SaveChataData(ChatConfigData);
                        break;
                }
            }
            GUILayout.EndHorizontal();
            ChatConfigData.AutomaticModeration = EditorGUILayout.Toggle(ChatConfigData.AutomaticModeration);
            EditorGUILayout.HelpBox("Check this option to enable automatic profanity filter and also ban profile for it. Ban only applies to chat.", MessageType.Info);

            if (ChatConfigData.AutomaticModeration)
            {
                /*GUILayout.Space(10);
                EditorGUILayout.LabelField("Censor char");
                var censorChar = ChatConfigData.GetCensorChar().ToString();
                censorChar = EditorGUILayout.TextField(censorChar.ToString(), GUILayout.Width(35));
                if (censorChar.Length > 1)
                {
                    censorChar = censorChar[0].ToString();
                }
                ChatConfigData.CensorChar = censorChar.Length > 0 ? censorChar[0].ToString() : string.Empty;*/

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Ban profile while profanity check ?");
                ChatConfigData.AutoBan = EditorGUILayout.Toggle(ChatConfigData.AutoBan);
                if (ChatConfigData.AutoBan)
                {
                    var banSeconds = ChatConfigData.GetAutoBanDuration();
                    EditorGUILayout.LabelField("Auto ban seconds");
                    banSeconds = EditorGUILayout.IntField(banSeconds, GUILayout.Width(150));
                    ChatConfigData.AutoBanSeconds = banSeconds;

                    GUILayout.Space(10);
                    var autoBanReason = ChatConfigData.GetAutoBanReason();
                    EditorGUILayout.LabelField("Auto ban reason");
                    ChatConfigData.AutoBanReason = EditorGUILayout.TextField(autoBanReason, GUILayout.Width(600));
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Additional bad word list to check");
                EditorGUILayout.HelpBox("CBS already has a list of bad words to check, but you can add to it here.", MessageType.Info);
                var wordList = ChatConfigData.GetBadWordList();
                for (int i = 0; i < wordList.Count; i++)
                {
                    var word = wordList[i];
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(word, GUILayout.Width(400));
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        ChatConfigData.RemoveBadWord(word);
                    }
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Add new bad word");
                GUILayout.BeginHorizontal();
                CurrentInputWord = EditorGUILayout.TextField(CurrentInputWord, GUILayout.Width(400));
                if (GUILayout.Button("Add", GUILayout.Width(35)))
                {
                    if (!string.IsNullOrEmpty(CurrentInputWord))
                    {
                        ChatConfigData.AddBadWord(CurrentInputWord);
                        CurrentInputWord = string.Empty;
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Notify about profile ban in chat?");
            var notificationTemplate = ChatConfigData.GetBanNotificationTemplate();
            ChatConfigData.BanNotification = EditorGUILayout.Toggle(ChatConfigData.BanNotification);
            if (ChatConfigData.BanNotification)
            {
                ChatConfigData.BanNotificationTemplate = EditorGUILayout.TextField(notificationTemplate, GUILayout.Width(600));
            }
            EditorGUILayout.HelpBox("Every time a user is banned, a message will be generated in the chat about it. {0} - moderator name, {1} banned profile name, {2} - banned hours, {3} - ban reason.", MessageType.Info);
        }

        private void DrawModerators()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            EditorGUILayout.LabelField("Moderator list", titleStyle);
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Enter Profile ID(PlayFabID) or Nickname");
            GUILayout.BeginHorizontal();
            CurrentInputModerator = EditorGUILayout.TextField(CurrentInputModerator);
            if (EditorUtils.DrawButton("Add moderator", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(200) }))
            {
                if (!string.IsNullOrEmpty(CurrentInputModerator))
                {
                    AddToModerator(CurrentInputModerator);
                    CurrentInputModerator = string.Empty;
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(20);

            // draw moderator list
            if (ModeratorsList == null)
                return;
            for (int i = 0; i < ModeratorsList.Count; i++)
            {
                var moderator = ModeratorsList[i];
                GUILayout.BeginHorizontal();

                var profileTexture = ResourcesUtils.GetProfileImage();
                GUILayout.Button(profileTexture, GUILayout.Width(50), GUILayout.Height(50));

                // draw profile id
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("ProfileID", BoldStyle, GUILayout.Width(55));
                EditorGUILayout.LabelField(moderator.ProfileID, GUILayout.Width(150));
                GUILayout.EndVertical();

                // draw expires
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Display name", BoldStyle, GUILayout.Width(100));
                EditorGUILayout.LabelField(moderator.DisplayName, GUILayout.Width(150));
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // draw save button
                if (EditorUtils.DrawButton("Remove", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(50) }))
                {
                    int option = EditorUtility.DisplayDialogComplex("Warning",
                                "Are you sure you want to remove moderator?",
                                "Yes",
                                "No",
                                string.Empty);
                    switch (option)
                    {
                        // ok.
                        case 0:
                            RemoveFromModeratorList(moderator.ProfileID);
                            break;
                    }
                }
                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.grey, 1, 20);

                GUILayout.Space(10);
            }
        }

        private void DrawStickers()
        {
            if (ChatConfigData == null)
                return;
            var stickers = ChatConfigData.Stiсkers ?? new List<ChatSticker>();

            if (stickers.Count == 0)
            {
                GUILayout.Space(10);
                if (EditorUtils.DrawButton("Import default stickers", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.Width(250), GUILayout.Height(30) }))
                {
                    ImportDefaultStickers();
                }
                if (EditorUtils.DrawButton("Add new sticker", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
                {
                    AddStickerWindow.Show(ItemAction.ADD, new ChatSticker(), onAdd =>
                    {
                        ChatConfigData.AddSticker(onAdd);
                    });
                    GUIUtility.ExitGUI();
                }
                return;
            }

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sticker List", BoldStyle, GUILayout.Width(150));
            GUILayout.FlexibleSpace();

            if (EditorUtils.DrawButton("Add new sticker", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                AddStickerWindow.Show(ItemAction.ADD, new ChatSticker(), onAdd =>
                {
                    ChatConfigData.AddSticker(onAdd);
                });
                GUIUtility.ExitGUI();
            }

            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
            {
                int option = EditorUtility.DisplayDialogComplex("Warning",
                                "All changes to the chat configuration are cached on the server for performance reasons. In order for the changes to take effect, you need to redeploy functions again",
                                "I understand. Save",
                                "Cancel",
                                string.Empty);
                switch (option)
                {
                    // ok.
                    case 0:
                        SaveChataData(ChatConfigData);
                        break;
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            for (int i = 0; i < stickers.Count; i++)
            {
                var sticker = stickers[i];
                var stickerSprite = sticker.GetSprite();
                var stickerTexture = stickerSprite == null ? null : stickerSprite.texture;

                GUILayout.BeginHorizontal();
                GUILayout.Button(stickerTexture, GUILayout.Width(85), GUILayout.Height(85));
                GUILayout.Label(sticker.ID);

                GUILayout.FlexibleSpace();

                GUILayout.BeginVertical();
                if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                {
                    AddStickerWindow.Show(ItemAction.EDIT, sticker, onAdd =>
                    {
                        stickers[i] = onAdd;
                    });
                    GUIUtility.ExitGUI();
                }
                if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                {
                    int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove this sticker?",
                            "Yes",
                            "No",
                            string.Empty);
                    switch (option)
                    {
                        // ok.
                        case 0:
                            ChatConfigData.RemoveSticker(sticker);
                            Icons.RemoveSprite(sticker.ID);
                            Icons.Save();
                            break;
                    }
                }
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();
            }
        }

        private void GetChatData()
        {
            ShowProgress();
            var keys = new List<string>();
            keys.Add(TitleKeys.ChatDataKey);

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
            bool keyExist = dictionary.ContainsKey(TitleKeys.ChatDataKey);
            var rawData = keyExist ? dictionary[TitleKeys.ChatDataKey] : JsonPlugin.EMPTY_JSON;
            ChatConfigData = new ChatConfigData();
            try
            {
                ChatConfigData = JsonPlugin.FromJsonDecompress<ChatConfigData>(rawData);
            }
            catch
            {
                ChatConfigData = JsonPlugin.FromJson<ChatConfigData>(rawData);
            }
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveChataData(ChatConfigData chatData)
        {
            ShowProgress();
            string rawData = JsonPlugin.ToJsonCompress(chatData);

            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.ChatDataKey,
                Value = rawData
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveChatData, OnSaveDataFailed);
        }

        private void OnSaveChatData(SetTitleDataResult result)
        {
            HideProgress();
            GetChatData();
        }

        private void OnSaveDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void GetModetatorsList()
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.GetModeratorsListMethod,
                    FunctionParameter = new FunctionBaseRequest()
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, onGet =>
                {
                    var cbsError = onGet.GetCBSError();
                    if (cbsError != null)
                    {
                        HideProgress();
                        AddErrorLog(cbsError);
                    }
                    else
                    {
                        HideProgress();
                        var functionResult = onGet.GetResult<FunctionModeratorListResult>();
                        ModeratorsList = functionResult.Moderators;
                    }
                }, onFailed =>
                {
                    HideProgress();
                    AddErrorLog(onFailed);
                });
            });
        }

        private void AddToModerator(string moderatorInput)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.AddToModeratorListMethod,
                    FunctionParameter = new FunctionModifyModeratorRequest
                    {
                        ProfileID = moderatorInput
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet =>
                {
                    var cbsError = OnGet.GetCBSError();
                    if (cbsError != null)
                    {
                        request.FunctionParameter = new FunctionModifyModeratorRequest
                        {
                            Nickname = moderatorInput
                        };
                        PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet2 =>
                        {
                            cbsError = OnGet2.GetCBSError();
                            if (cbsError != null)
                            {
                                AddErrorLog(cbsError);
                                EditorUtility.DisplayDialog("Failed!", cbsError.Message, "OK");
                                HideProgress();
                            }
                            else
                            {
                                HideProgress();
                                EditorUtility.DisplayDialog("Success!", "Profile added to moderator list!", "OK");
                                GetModetatorsList();
                            }
                        }, onFailed =>
                        {
                            AddErrorLog(cbsError);
                            EditorUtility.DisplayDialog("Failed!", cbsError.Message, "OK");
                            HideProgress();
                        });
                    }
                    else
                    {
                        HideProgress();
                        EditorUtility.DisplayDialog("Success!", "Profile added to moderator list!", "OK");
                        GetModetatorsList();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void RemoveFromModeratorList(string profileID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.RemoveFromModeratorListMethod,
                    FunctionParameter = new FunctionBaseRequest
                    {
                        ProfileID = profileID
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, onGet =>
                {
                    var cbsError = onGet.GetCBSError();
                    if (cbsError != null)
                    {
                        HideProgress();
                        AddErrorLog(cbsError);
                    }
                    else
                    {
                        HideProgress();
                        GetModetatorsList();
                    }
                }, onFailed =>
                {
                    HideProgress();
                    AddErrorLog(onFailed);
                });
            });
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

        private void ImportDefaultStickers()
        {
            for (int i = 0; i < 8; i++)
            {
                var id = "sticker_" + (i + 1).ToString();
                var sticker = new ChatSticker
                {
                    ID = id
                };
                ChatConfigData.AddSticker(sticker);
                var sprite = ResourcesUtils.GetSticker(id);
                Icons.SaveSprite(id, sprite);
            }
            Icons.Save();
        }
    }
}
#endif

#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.ServerModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ProfileConfigurator : BaseConfigurator
    {
        private readonly string LEVEL_TITLE_ID = TitleKeys.LevelTitleDataKey;
        private readonly string AVATAR_TITLE_ID = TitleKeys.ProfileAvatarsTableKey;

        private int SelectedToolBar { get; set; }

        private ProfileLevelTable LevelTable { get; set; } = new ProfileLevelTable();
        private AvatarsTable AvatarsTable { get; set; }

        private List<PlayFab.AdminModels.CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private Categories CachedLootBoxCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }

        private EditorData EditorData { get; set; }
        private ProfileConfigData ProfileData { get; set; }
        private AvatarIcons AvatarsData { get; set; }

        private List<string> AvailableAzureMethods { get; set; }

        private string CurrentBanInput { get; set; }
        private List<PlayFab.AdminModels.BanInfo> CurrentBans { get; set; }
        private string SelectedProfileIDForBan { get; set; }
        private int LastSelectedAvatarToolbar = -1;

        private GUIStyle TitleStyle
        {
            get
            {
                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 14;
                return titleStyle;
            }
        }

        private GUIStyle BoldStyle
        {
            get
            {
                var boldStyle = new GUIStyle(GUI.skin.label);
                boldStyle.fontStyle = FontStyle.Bold;
                return boldStyle;
            }
        }

        private GUILayoutOption[] AddButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Height(36.3f), GUILayout.Width(164f) };
            }
        }

        private GUILayoutOption[] SaveButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Width(50) };
            }
        }

        protected override string Title => "Profile Congiguration";

        protected override bool DrawScrollView => true;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            ProfileData = CBSScriptable.Get<ProfileConfigData>();
            AvatarsData = CBSScriptable.Get<AvatarIcons>();
            AvailableAzureMethods = AzureFunctions.AllMethods;
            AvailableAzureMethods.Remove(AzureFunctions.UpdateProfileOnlineStateMethod);
            AvailableAzureMethods.TrimExcess();

            GetLevelTable();
        }

        protected override void OnDrawInside()
        {
            // draw sub titles
            var lastSelectedBar = GUILayout.Toolbar(SelectedToolBar, new string[] { "Level/Expirience", "Online Status", "Profile Avatars", "Player Management" });
            switch (SelectedToolBar)
            {
                case 0:
                    DrawLevels();
                    break;
                case 1:
                    DrawOnlineStatus();
                    break;
                case 2:
                    DrawAvatars();
                    break;
                case 3:
                    DrawManagement();
                    break;
                default:
                    break;
            }
            if (lastSelectedBar != SelectedToolBar)
            {
                GUI.FocusControl(null);
                CurrentBanInput = string.Empty;
                CurrentBans = null;
                SelectedProfileIDForBan = string.Empty;

                if (lastSelectedBar == 2)
                {

                }
            }
            SelectedToolBar = lastSelectedBar;
        }

        private void DrawOnlineStatus()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Online status config", TitleStyle);
            GUILayout.Space(20);

            bool onlineEnabled = EditorGUILayout.Toggle("Enable online status", ProfileData.EnableOnlineStatus);
            var onlineBehavior = ProfileData.OnlineUpdateBehavior;
            var onlineThreshold = ProfileData.ConsiderInactiveAfter;
            var updateInterval = ProfileData.UpdateInterval;
            var triggers = ProfileData.TriggerMethods;

            if (onlineEnabled)
            {
                onlineThreshold = EditorGUILayout.IntField("Online Threshold", ProfileData.ConsiderInactiveAfter, new GUILayoutOption[] { GUILayout.Width(300) });
                EditorGUILayout.HelpBox("A parameter that determines how many seconds after the last status update to consider the user inactive.", MessageType.Info);
                GUILayout.Space(10);
                onlineBehavior = (OnlineStatusBehavior)EditorGUILayout.EnumPopup("Update Behavior", ProfileData.OnlineUpdateBehavior, new GUILayoutOption[] { GUILayout.Width(350) });
                if (onlineBehavior == OnlineStatusBehavior.CUSTOM)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("In this mode, you must update the user's online status yourself. For you need to call the CBSProfileModule.UpdateOnlineState method.", MessageType.Info);
                }
                else if (onlineBehavior == OnlineStatusBehavior.LOOP_UPDATE)
                {
                    GUILayout.Space(10);
                    updateInterval = EditorGUILayout.IntField("Update Interval", ProfileData.UpdateInterval, new GUILayoutOption[] { GUILayout.Width(300) });
                    if (updateInterval < 1)
                        updateInterval = 1;
                    if (updateInterval >= onlineThreshold)
                    {
                        EditorGUILayout.HelpBox("'Update interval' cannot be greater than 'Online Threshold', otherwise online status may not always show up-to-date data.", MessageType.Warning);
                    }
                }
                else if (onlineBehavior == OnlineStatusBehavior.WHEN_SPECIFIC_CALLS)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("Select a list of methods that will update the online status when called.", MessageType.Info);
                    triggers = triggers ?? new List<string>();

                    foreach (var method in AvailableAzureMethods)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(method, new GUILayoutOption[] { GUILayout.Width(250) });
                        var lastTrigger = triggers.Contains(method);
                        var enableTrigger = EditorGUILayout.Toggle(lastTrigger);
                        if (lastTrigger != enableTrigger)
                        {
                            if (enableTrigger)
                            {
                                if (!triggers.Contains(method))
                                {
                                    triggers.Add(method);
                                }
                            }
                            else
                            {
                                if (triggers.Contains(method))
                                {
                                    triggers.Remove(method);
                                }
                            }
                            triggers.TrimExcess();
                        }
                        GUILayout.EndHorizontal();
                    }
                }
            }

            ProfileData.EnableOnlineStatus = onlineEnabled;
            ProfileData.OnlineUpdateBehavior = onlineBehavior;
            ProfileData.ConsiderInactiveAfter = onlineThreshold;
            ProfileData.TriggerMethods = triggers;
            ProfileData.UpdateInterval = updateInterval;

            ProfileData.Save();
        }

        private void DrawAvatars()
        {
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Avatar Display Options");
            var displayOptions = (AvatarDisplayOptions)EditorGUILayout.EnumPopup(ProfileData.AvatarDisplay, new GUILayoutOption[] { GUILayout.Width(150) });

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Default Avatar Sprite");
            ProfileData.DefaultAvatar = (Sprite)EditorGUILayout.ObjectField((ProfileData.DefaultAvatar as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            var defaultAvatar = ProfileData.DefaultAvatar == null ? null : ProfileData.DefaultAvatar.texture;
            GUILayout.Button(defaultAvatar, GUILayout.Width(100), GUILayout.Height(100));

            if (displayOptions == AvatarDisplayOptions.LOAD_AVATAR_URL)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Use Cache For Avatars");
                ProfileData.UseCacheForAvatars = EditorGUILayout.Toggle(ProfileData.UseCacheForAvatars);
            }

            if (LastSelectedAvatarToolbar != (int)displayOptions)
            {
                AvatarsTable = null;
                if (displayOptions == AvatarDisplayOptions.LOAD_AVATAR_SPRITE)
                {
                    GetAvatarsTable();
                }
            }
            LastSelectedAvatarToolbar = (int)displayOptions;

            // draw avatars table
            if (displayOptions == AvatarDisplayOptions.LOAD_AVATAR_SPRITE)
            {
                GUILayout.Space(40);

                GUILayout.BeginHorizontal();

                // draw id
                GUILayout.Space(27);
                EditorGUILayout.LabelField("ID", BoldStyle, new GUILayoutOption[] { GUILayout.MaxWidth(200) });

                // draw level
                GUILayout.Space(120);
                EditorGUILayout.LabelField("Level", BoldStyle, new GUILayoutOption[] { GUILayout.MaxWidth(200) });

                // draw price
                GUILayout.Space(60);
                EditorGUILayout.LabelField("Price", BoldStyle, new GUILayoutOption[] { GUILayout.MaxWidth(200) });

                GUILayout.FlexibleSpace();
                if (EditorUtils.DrawButton("Add new avatar", EditorData.AddColor, 12, AddButtonOptions))
                {
                    ShowProgress();
                    var itemConfig = new ItemsConfigurator();
                    itemConfig.GetAllCurrencies(currency =>
                    {
                        HideProgress();
                        var currencyList = currency.VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                        AddAvatarWindow.Show(ItemAction.ADD, new CBSSpriteAvatar(), currencyList, onAdd =>
                        {
                            AddAvatar(onAdd);
                        });
                        //GUIUtility.ExitGUI();
                    });
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.black, 2, 20);

                if (AvatarsTable != null && AvatarsTable.Avatars != null && AvatarsTable.Avatars.Count != 0)
                {
                    var avatarTable = AvatarsTable.Avatars;
                    for (int i = 0; i < avatarTable.Count; i++)
                    {
                        var avatar = avatarTable[i];
                        GUILayout.BeginHorizontal();

                        // draw id
                        GUILayout.Space(15);
                        EditorGUILayout.LabelField(avatar.ID, GUILayout.Width(80));

                        // draw sprite
                        var sprite = avatar.GetSprite();
                        var spriteTexture = sprite == null ? null : sprite.texture;
                        GUILayout.Button(spriteTexture, GUILayout.Width(50), GUILayout.Height(50));

                        // draw level
                        GUILayout.Space(210);
                        var hasLevel = avatar.HasLevelLimit;
                        var levelLabel = hasLevel ? avatar.LevelLimit.ToString() : "--";
                        EditorGUILayout.LabelField(levelLabel, GUILayout.Width(80));

                        // draw price
                        GUILayout.Space(170);
                        var hasPrice = avatar.Purchasable;
                        if (!hasPrice)
                        {
                            EditorGUILayout.LabelField("Free", GUILayout.Width(80));
                        }
                        else
                        {
                            var price = avatar.Price;
                            if (price.IsValid())
                            {
                                var curSprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(price.CurrencyID);
                                var curTexture = curSprite == null ? null : curSprite.texture;
                                GUILayout.Button(curTexture, GUILayout.Width(25), GUILayout.Height(25));
                                EditorGUILayout.LabelField(price.CurrencyID, GUILayout.Width(25));
                                EditorGUILayout.LabelField(price.CurrencyValue.ToString(), GUILayout.Width(100));
                            }
                        }

                        // draw button

                        // remove
                        GUILayout.FlexibleSpace();
                        if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, GUILayout.Width(100), GUILayout.Height(50)))
                        {
                            int option = EditorUtility.DisplayDialogComplex("Warning",
                                "Are you sure you want to remove this avatar?",
                                "Yes",
                                "No",
                                string.Empty);
                            switch (option)
                            {
                                // ok.
                                case 0:
                                    RemoveAvatar(avatar);
                                    break;
                            }
                        }

                        // edit
                        if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, GUILayout.Width(100), GUILayout.Height(50)))
                        {
                            ShowProgress();
                            var itemConfig = new ItemsConfigurator();
                            itemConfig.GetAllCurrencies(currency =>
                            {
                                HideProgress();
                                var currencyList = currency.VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                                AddAvatarWindow.Show(ItemAction.EDIT, avatar, currencyList, onAdd =>
                                {
                                    UpdateAvatar(onAdd);
                                });
                                //GUIUtility.ExitGUI();
                            });
                            GUIUtility.ExitGUI();
                        }

                        GUILayout.EndHorizontal();
                        EditorUtils.DrawUILine(Color.grey, 2, 20);
                    }
                }
                else
                {
                    // import default preset
                    if (EditorUtils.DrawButton("Import default avatars", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(200) }))
                    {
                        ImportDefaultAvatars();
                    }
                }
            }

            ProfileData.AvatarDisplay = displayOptions;
            ProfileData.Save();
        }

        private void DrawManagement()
        {
            EditorGUILayout.LabelField("Ban management", TitleStyle);
            GUILayout.Space(20);
            EditorGUILayout.LabelField("Enter Profile ID(PlayFabID) or Nickname");
            GUILayout.BeginHorizontal();
            CurrentBanInput = EditorGUILayout.TextField(CurrentBanInput);
            if (EditorUtils.DrawButton("Search", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(200) }))
            {
                GetProfileBans();
            }
            GUILayout.EndHorizontal();

            if (CurrentBans == null)
                return;
            if (!string.IsNullOrEmpty(SelectedProfileIDForBan))
            {
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (EditorUtils.DrawButton("Revoke All Bans", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(150), GUILayout.Height(30) }))
                {
                    RevokeAllBans();
                }
                if (EditorUtils.DrawButton("Add Ban", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(30) }))
                {
                    AddBanWindow.Show(SelectedProfileIDForBan, onAdd =>
                    {
                        BanProfile(onAdd);
                    });
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
            }
            if (CurrentBans.Count == 0 && !string.IsNullOrEmpty(SelectedProfileIDForBan))
            {
                GUILayout.Space(50);
                EditorGUILayout.LabelField("No bans found", TitleStyle);
            }

            GUILayout.Space(10);
            for (int i = 0; i < CurrentBans.Count; i++)
            {
                var banInfo = CurrentBans[i];
                GUILayout.BeginHorizontal();

                var profileTexture = ResourcesUtils.GetProfileImage();
                GUILayout.Button(profileTexture, GUILayout.Width(50), GUILayout.Height(50));

                // draw status
                var statusIcon = banInfo.Active ? ResourcesUtils.GetRedDotImage() : ResourcesUtils.GetGreyDotImage();
                var statusText = banInfo.Active.ToString();
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Active ?", BoldStyle, GUILayout.Width(55));
                GUILayout.BeginHorizontal();
                GUILayout.Button(statusIcon, GUILayout.Width(30), GUILayout.Height(30));
                EditorGUILayout.LabelField(statusText, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();

                // draw ban id
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("BanID", BoldStyle, GUILayout.Width(55));
                EditorGUILayout.LabelField(banInfo.BanId, GUILayout.Width(150));
                GUILayout.EndVertical();

                // draw expires
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Expires", BoldStyle, GUILayout.Width(55));
                EditorGUILayout.LabelField(banInfo.Expires?.ToString(), GUILayout.Width(150));
                GUILayout.EndVertical();

                // draw reason
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Reason", BoldStyle, GUILayout.Width(55));
                EditorGUILayout.LabelField(banInfo.Reason?.ToString());
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                EditorGUI.BeginDisabledGroup(!banInfo.Active);
                // draw save button
                if (EditorUtils.DrawButton("Revoke Ban", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(50) }))
                {
                    RevokeBan(banInfo.BanId);
                }
                EditorGUI.EndDisabledGroup();

                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.grey, 1, 20);

                GUILayout.Space(10);
            }
        }

        private void DrawLevels()
        {
            GUIStyle btnStyle = new GUIStyle("Label");

            // draw level table
            if (LevelTable != null)
            {
                var levelTitleStyle = new GUIStyle(GUI.skin.label);
                levelTitleStyle.fontStyle = FontStyle.Bold;

                if (LevelTable.Table.Count == 0)
                {
                    // import default preset
                    if (EditorUtils.DrawButton("Import default level data", EditorData.AddColor, 12, new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(200) }))
                    {
                        ImportDefaultLevels();
                    }
                }
                else
                {
                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Expirience table", TitleStyle);
                    GUILayout.FlexibleSpace();
                    if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        UpdateLevelDetail(LevelTable);
                    }
                    GUILayout.EndHorizontal();

                    // draw exp multiply
                    EditorGUILayout.LabelField("Experience multiplier", EditorUtils.TitleStyle);
                    LevelTable.ExpMultiply = EditorGUILayout.FloatField(LevelTable.GetExpMultiply(), GUILayout.Width(300));
                    GUILayout.Space(10);

                    // draw reward devilery
                    LevelTable.RewardDelivery = EditorUtils.DrawRewardDelivery(LevelTable.RewardDelivery);
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("0", levelTitleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(20), GUILayout.Height(35) });
                    EditorGUILayout.LabelField("Null level is not configurable", new GUILayoutOption[] { GUILayout.MaxWidth(270), GUILayout.Height(35) });
                    GUILayout.FlexibleSpace();

                    if (EditorUtils.DrawButton("Registration reward", EditorData.AddColor, 14, new GUILayoutOption[] { GUILayout.Width(250), GUILayout.Height(30) }))
                    {
                        var prize = LevelTable.RegistrationPrize;
                        ShowPrizeDialog(prize, false, result =>
                        {
                            LevelTable.RegistrationPrize = result;
                            UpdateLevelDetail(LevelTable);
                        });
                    }
                    if (EditorUtils.DrawButton("Registration events", EditorData.EventColor, 14, new GUILayoutOption[] { GUILayout.Width(250), GUILayout.Height(30) }))
                    {
                        EditorUtils.ShowProfileEventWindow(LevelTable.RegistrationEvents, onAdd =>
                        {
                            LevelTable.RegistrationEvents = onAdd;
                            UpdateLevelDetail(LevelTable);
                        });
                    }
                    GUILayout.EndHorizontal();
                    EditorUtils.DrawUILine(Color.grey, 1, 20);
                }

                for (int i = 0; i < LevelTable.Table.Count; i++)
                {
                    var level = LevelTable.Table[i];
                    GUILayout.BeginHorizontal();
                    string levelString = (i + 1).ToString();
                    var levelDetail = level;
                    // display exp

                    EditorGUILayout.LabelField(levelString, levelTitleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(20) });
                    EditorGUILayout.LabelField("Experience is needed to reach the level " + levelString, new GUILayoutOption[] { GUILayout.MaxWidth(270) });
                    GUIStyle expStyle = new GUIStyle("Label");
                    var texture = ResourcesUtils.GetTextureByPath("Profile/exp.png");
                    GUILayout.BeginVertical(new GUILayoutOption[] { GUILayout.MaxWidth(40) });
                    GUILayout.Space(-10);
                    GUILayout.Button(texture, expStyle, new GUILayoutOption[] { GUILayout.MaxWidth(40) });
                    GUILayout.EndVertical();
                    levelDetail.Expirience = EditorGUILayout.IntField(levelDetail.Expirience, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                    // draw save button
                    if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, SaveButtonOptions))
                    {
                        var rawDataToSave = JsonUtility.ToJson(levelDetail);
                        UpdateLevelDetail(LevelTable);
                    }
                    // draw price button
                    GUILayout.Space(50);

                    if (EditorUtils.DrawButton("Edit rewards", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100) }))
                    {
                        ShowPrizeDialog(level.Reward, true, result =>
                        {
                            level.Reward = result;
                            UpdateLevelDetail(LevelTable);
                        });
                    }

                    if (EditorUtils.DrawButton("Edit events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100) }))
                    {
                        EditorUtils.ShowProfileEventWindow(LevelTable.LevelEvents[i], onAdd =>
                        {
                            LevelTable.LevelEvents[i] = onAdd;
                            UpdateLevelDetail(LevelTable);
                        });
                    }

                    GUILayout.EndHorizontal();
                    EditorUtils.DrawUILine(Color.grey, 1, 20);
                    GUILayout.Space(10);
                }

                GUILayout.Space(20);

                GUILayout.BeginHorizontal();
                // add new level
                if (EditorUtils.DrawButton("Add new level", EditorData.AddColor, 14, AddButtonOptions))
                {
                    AddNewLevel();
                }

                if (LevelTable.Table.Count != 0)
                {
                    // remove last level
                    if (EditorUtils.DrawButton("Remove last level", EditorData.RemoveColor, 14, AddButtonOptions))
                    {
                        if (LevelTable == null || LevelTable.Table.Count == 0)
                            return;
                        int lastLevelKey = LevelTable.Table.Count - 1;
                        RemoveLevelDetail(lastLevelKey);
                    }
                    // remove all level
                    if (EditorUtils.DrawButton("Remove all levels", EditorData.RemoveColor, 14, AddButtonOptions))
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove all levels data?",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveLevelGroup();
                                break;
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        private void AddAvatar(CBSSpriteAvatar avatar)
        {
            AvatarsTable.Avatars.Add(avatar);
            UpdateAvatarTable(AvatarsTable);
        }

        private void UpdateAvatar(CBSSpriteAvatar avatar)
        {
            UpdateAvatarTable(AvatarsTable);
        }

        private void RemoveAvatar(CBSSpriteAvatar avatar)
        {
            if (AvatarsTable.Avatars.Contains(avatar))
            {
                AvatarsData.RemoveSprite(avatar.ID);
                AvatarsTable.Avatars.Remove(avatar);
                AvatarsTable.Avatars.TrimExcess();
            }
            UpdateAvatarTable(AvatarsTable);
        }

        // get avatars
        private void GetAvatarsTable()
        {
            ShowProgress();
            var keyList = new List<string>();
            keyList.Add(AVATAR_TITLE_ID);
            var dataRequest = new PlayFab.AdminModels.GetTitleDataRequest
            {
                Keys = keyList
            };
            PlayFabAdminAPI.GetTitleInternalData(dataRequest, OnAvatarsTableGetted, OnTableError);
        }

        private void OnAvatarsTableGetted(PlayFab.AdminModels.GetTitleDataResult result)
        {
            bool tableExist = result.Data.ContainsKey(AVATAR_TITLE_ID);
            if (tableExist)
            {
                string tableRaw = result.Data[AVATAR_TITLE_ID];
                var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
                var table = jsonPlugin.DeserializeObject<AvatarsTable>(tableRaw);

                HideProgress();
                AvatarsTable = table ?? new AvatarsTable();
                AvatarsTable.Avatars = AvatarsTable.Avatars ?? new List<CBSSpriteAvatar>();
            }
            else
            {
                AvatarsTable = new AvatarsTable();
                AvatarsTable.Avatars = new List<CBSSpriteAvatar>();
            }
            HideProgress();
        }

        private void UpdateAvatarTable(AvatarsTable avatarTable)
        {
            ShowProgress();
            var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
            string listRaw = jsonPlugin.SerializeObject(avatarTable);

            var dataRequest = new SetTitleDataRequest
            {
                Key = AVATAR_TITLE_ID,
                Value = listRaw
            };

            PlayFabServerAPI.SetTitleInternalData(dataRequest, OnAvataTableUpdated, OnUpdateFailed);
        }

        private void OnAvataTableUpdated(SetTitleDataResult result)
        {
            HideProgress();
            GetAvatarsTable();
        }

        // get level table
        private void GetLevelTable()
        {
            ShowProgress();
            var keyList = new List<string>();
            keyList.Add(LEVEL_TITLE_ID);
            var dataRequest = new PlayFab.AdminModels.GetTitleDataRequest
            {
                Keys = keyList
            };
            PlayFabAdminAPI.GetTitleInternalData(dataRequest, OnLevelTableGetted, OnTableError);
        }

        private void OnLevelTableGetted(PlayFab.AdminModels.GetTitleDataResult result)
        {
            bool tableExist = result.Data.ContainsKey(LEVEL_TITLE_ID);
            if (tableExist)
            {
                string tableRaw = result.Data[LEVEL_TITLE_ID];
                var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
                var table = jsonPlugin.DeserializeObject<ProfileLevelTable>(tableRaw);

                HideProgress();
                LevelTable = table;
            }
            else
            {
                LevelTable = new ProfileLevelTable();
            }
            HideProgress();
        }

        private void OnTableError(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // remove level group
        private void RemoveLevelGroup()
        {
            ShowProgress();
            LevelTable = new ProfileLevelTable();
            UpdateLevelDetail(LevelTable);
        }

        // add empty level
        private void AddNewLevel()
        {
            var levelDetail = new LevelDetail
            {
                Expirience = 0
            };
            LevelTable.AddLevelDetail(levelDetail);
            UpdateLevelDetail(LevelTable);
        }

        private void UpdateLevelDetail(ProfileLevelTable levelTable)
        {
            ShowProgress();
            var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
            string listRaw = jsonPlugin.SerializeObject(levelTable);

            var dataRequest = new SetTitleDataRequest
            {
                Key = LEVEL_TITLE_ID,
                Value = listRaw
            };

            PlayFabServerAPI.SetTitleInternalData(dataRequest, OnLevelDataUpdated, OnUpdateFailed);
        }

        private void RemoveLevelDetail(int index)
        {
            ShowProgress();
            LevelTable.RemoveLevelDetailAt(index);
            UpdateLevelDetail(LevelTable);
        }

        private void OnLevelDataUpdated(SetTitleDataResult result)
        {
            HideProgress();
            GetLevelTable();
        }

        private void OnUpdateFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        private void ShowPrizeDialog(RewardObject prize, bool includeCurrencies, Action<RewardObject> modifyCallback)
        {
            if (CachedItemCategories == null || CachedItems == null || CacheCurrencies == null || CachedLootBoxCategories == null)
            {
                ShowProgress();
                var itemConfig = new ItemsConfigurator();
                itemConfig.GetTitleData(categoriesResult =>
                {
                    if (categoriesResult.Data.ContainsKey(TitleKeys.ItemsCategoriesKey))
                    {
                        var rawData = categoriesResult.Data[TitleKeys.ItemsCategoriesKey];
                        CachedItemCategories = JsonUtility.FromJson<Categories>(rawData);
                    }
                    else
                    {
                        CachedItemCategories = new Categories();
                    }

                    if (categoriesResult.Data.ContainsKey(TitleKeys.LootboxesCategoriesKey))
                    {
                        var rawData = categoriesResult.Data[TitleKeys.LootboxesCategoriesKey];
                        CachedLootBoxCategories = JsonUtility.FromJson<Categories>(rawData);
                    }
                    else
                    {
                        CachedLootBoxCategories = new Categories();
                    }

                    // get item catalog
                    itemConfig.GetItemsCatalog(itemsResult =>
                    {
                        HideProgress();
                        CachedItems = itemsResult.Catalog;
                        itemConfig.GetAllCurrencies(curResult =>
                        {
                            CacheCurrencies = curResult.VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                            // show prize windows
                            AddRewardWindow.Show(new RewardWindowRequest
                            {
                                currencies = CacheCurrencies,
                                includeCurencies = includeCurrencies,
                                itemCategories = CachedItemCategories,
                                lootboxCategories = CachedLootBoxCategories,
                                items = CachedItems,
                                modifyCallback = modifyCallback,
                                reward = prize
                            });
                            //GUIUtility.ExitGUI();
                        });
                    });
                });
            }
            else
            {
                // show prize windows
                AddRewardWindow.Show(new RewardWindowRequest
                {
                    currencies = CacheCurrencies,
                    includeCurencies = includeCurrencies,
                    itemCategories = CachedItemCategories,
                    lootboxCategories = CachedLootBoxCategories,
                    items = CachedItems,
                    modifyCallback = modifyCallback,
                    reward = prize
                });
                GUIUtility.ExitGUI();
            }
        }

        private void RevokeAllBans()
        {
            if (string.IsNullOrEmpty(SelectedProfileIDForBan))
                return;
            ShowProgress();
            var request = new PlayFab.AdminModels.RevokeAllBansForUserRequest
            {
                PlayFabId = SelectedProfileIDForBan
            };
            PlayFabAdminAPI.RevokeAllBansForUser(request, onRevoke =>
            {
                HideProgress();
                GetProfileBans(SelectedProfileIDForBan);
            }, onFailed =>
            {
                EditorUtility.DisplayDialog(onFailed.Error.ToString(), onFailed.ErrorMessage, "OK");
            });
        }

        private void RevokeBan(string banID)
        {
            ShowProgress();
            var bansIDs = new List<string>();
            bansIDs.Add(banID);
            var request = new PlayFab.AdminModels.RevokeBansRequest
            {
                BanIds = bansIDs
            };
            PlayFabAdminAPI.RevokeBans(request, onRevoke =>
            {
                HideProgress();
                GetProfileBans(SelectedProfileIDForBan);
            }, onFailed =>
            {
                EditorUtility.DisplayDialog(onFailed.Error.ToString(), onFailed.ErrorMessage, "OK");
            });
        }

        private void BanProfile(PlayFab.AdminModels.BanUsersRequest banRequest)
        {
            ShowProgress();
            PlayFabAdminAPI.BanUsers(banRequest, onRevoke =>
            {
                HideProgress();
                GetProfileBans(SelectedProfileIDForBan);
            }, onFailed =>
            {
                EditorUtility.DisplayDialog(onFailed.Error.ToString(), onFailed.ErrorMessage, "OK");
            });
        }

        private void GetProfileBans(string id = "")
        {
            var banInput = string.IsNullOrEmpty(id) ? CurrentBanInput : id;
            if (string.IsNullOrEmpty(banInput))
                return;
            ShowProgress();
            var request = new PlayFab.AdminModels.GetUserBansRequest
            {
                PlayFabId = banInput
            };
            PlayFabAdminAPI.GetUserBans(request, onGet =>
            {
                HideProgress();
                CurrentBans = onGet.BanData;
                SelectedProfileIDForBan = banInput;
            }, onError =>
            {
                Debug.LogError(onError.GenerateErrorReport());
                if (onError.Error == PlayFabErrorCode.InvalidParams && string.IsNullOrEmpty(id))
                {
                    var profileRequest = new PlayFab.AdminModels.LookupUserAccountInfoRequest
                    {
                        TitleDisplayName = banInput
                    };
                    PlayFabAdminAPI.GetUserAccountInfo(profileRequest, onGet =>
                    {
                        var profileID = onGet.UserInfo.PlayFabId;
                        SelectedProfileIDForBan = profileID;
                        GetProfileBans(profileID);
                    }, onFailed =>
                    {
                        HideProgress();
                        OnFailedToFindUser();
                    });
                }
                else
                {
                    HideProgress();
                    OnFailedToFindUser();
                }
            });
        }

        private void OnFailedToFindUser()
        {
            EditorUtility.DisplayDialog("Falied", "Unable to find profile", "OK");
            CurrentBans = null;
            SelectedProfileIDForBan = string.Empty;
        }

        // default set
        private void ImportDefaultAvatars()
        {
            AvatarsTable = GetDefaultAvatarPreset();
            UpdateAvatarTable(AvatarsTable);
        }

        private void ImportDefaultLevels()
        {
            var defaultPreset = GetDefaultLevelPreset();
            UpdateLevelDetail(defaultPreset);
        }

        private ProfileLevelTable GetDefaultLevelPreset()
        {
            var newSet = new ProfileLevelTable();

            newSet.AddLevelDetail(new LevelDetail { Expirience = 100 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 200 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 500 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 1000 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 1700 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 3000 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 5000 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 8000 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 15000 });
            newSet.AddLevelDetail(new LevelDetail { Expirience = 30000 });

            return newSet;
        }

        private AvatarsTable GetDefaultAvatarPreset()
        {
            AvatarsTable = new AvatarsTable();
            AvatarsTable.Avatars = new List<CBSSpriteAvatar>();

            for (int i = 0; i < 10; i++)
            {
                var index = i + 1;
                var avatarID = "avatar_" + index;
                AvatarsTable.Avatars.Add(new CBSSpriteAvatar { ID = avatarID });
                var avatarSprite = ResourcesUtils.GetAvatar(avatarID);
                AvatarsData.SaveSprite(avatarID, avatarSprite);
            }
            return AvatarsTable;
        }
    }
}
#endif

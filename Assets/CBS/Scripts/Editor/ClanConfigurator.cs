#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using CBS.Scriptable;
using PlayFab;
using PlayFab.AdminModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ClanConfigurator : BaseConfigurator
    {
        protected override string Title => "Clan Configuration";

        protected override bool DrawScrollView => true;
        private int SelectedToolBar { get; set; }
        private int IconsToolBar { get; set; }
        private List<CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private Categories CachedLootBoxCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }
        private string RoleIDInput { get; set; }
        private string IconIDInput { get; set; }
        private GUILayoutOption[] SaveButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Width(50) };
            }
        }
        private GUILayoutOption[] AddButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Height(36.3f), GUILayout.Width(164f) };
            }
        }

        private Vector2 VerticalPos { get; set; }
        private ClanMetaData ClanData { get; set; }
        private EditorData EditorData { get; set; }
        private ClanIcons ClanIcons { get; set; }
        private List<ClanRolePermission> RolePermissions { get; set; }
        private ClanTaskConfigurator ClanTask { get; set; }

        private SerializedObject IconsColorsProperty { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            ClanTask = new ClanTaskConfigurator();
            ClanTask.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            ClanIcons = CBSScriptable.Get<ClanIcons>();
            RolePermissions = Enum.GetValues(typeof(ClanRolePermission)).Cast<ClanRolePermission>().ToList();
            IconsColorsProperty = new SerializedObject(ClanIcons);
            GetClanData();
        }

        protected override void OnDrawInside()
        {
            if (ClanData == null)
                return;

            SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, new string[] { "General", "Roles", "Levels", "Tasks", "Icons" });

            switch (SelectedToolBar)
            {
                case 0:
                    DrawGeneral();
                    break;
                case 1:
                    DrawRoles();
                    break;
                case 2:
                    DrawLevels();
                    break;
                case 3:
                    DrawTasks();
                    break;
                case 4:
                    DrawIcons();
                    break;
                default:
                    break;
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
                SaveClanData(ClanData);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // draw max members
            EditorGUILayout.LabelField("Max members in clan");
            ClanData.ClanMemberCount = EditorGUILayout.IntField(ClanData.GetClanMemberCount(), new GUILayoutOption[] { GUILayout.Width(100) });
            EditorGUILayout.HelpBox("Members count in clan. Max value = 100", MessageType.Info);
            GUILayout.Space(10);
            
            EditorGUILayout.LabelField("Clan name profanity check ?");
            ClanData.DisplayNameProfanityCheck = EditorGUILayout.Toggle(ClanData.DisplayNameProfanityCheck);
            EditorGUILayout.HelpBox("Enable this option for the system to enable profanity check for clan display name.", MessageType.Info);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Send join message ?");
            ClanData.SendJoinMessage = EditorGUILayout.Toggle(ClanData.SendJoinMessage);
            EditorGUILayout.HelpBox("If true - then the user will join the clan - a message will be sent to the clan chat about this.", MessageType.Info);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Send leave message ?");
            ClanData.SendLeaveMessage = EditorGUILayout.Toggle(ClanData.SendLeaveMessage);
            EditorGUILayout.HelpBox("If true - then the user leave the clan - a message will be sent to the clan chat about this.", MessageType.Info);
            GUILayout.Space(10);

            EditorGUILayout.LabelField("Send change role message ?");
            ClanData.SendChangeRoleMessage = EditorGUILayout.Toggle(ClanData.SendChangeRoleMessage);
            EditorGUILayout.HelpBox("If true - then the user change a role - a message will be sent to the clan chat about this.", MessageType.Info);
            GUILayout.Space(10);

            GUILayout.Space(20);
        }

        private void DrawRoles()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var boltStyle = new GUIStyle(GUI.skin.label);
            boltStyle.fontStyle = FontStyle.Bold;
            boltStyle.richText = true;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Role management", titleStyle);
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
            {
                SaveClanData(ClanData);
            }
            GUILayout.EndHorizontal();

            var roleList = ClanData.GetRoleList();

            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Role ID", boltStyle, GUILayout.Width(100));
            EditorGUILayout.LabelField("Disaply Name", boltStyle, GUILayout.Width(200));
            GUILayout.EndHorizontal();

            for (int i = 0; i < roleList.Count; i++)
            {
                var roleInfo = roleList[i];
                var isDefaultRole = ClanData.IsDefaultRole(roleInfo.RoleID);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(roleInfo.RoleID, GUILayout.Width(100));
                roleInfo.DisplayName = EditorGUILayout.TextField(roleInfo.DisplayName, GUILayout.Width(200));
                EditorGUI.BeginDisabledGroup(isDefaultRole);
                if (GUILayout.Button("↑", GUILayout.Width(25)))
                {
                    ClanData.MoveRoleUp(roleInfo.RoleID);
                }
                if (GUILayout.Button("↓", GUILayout.Width(25)))
                {
                    ClanData.MoveRoleDown(roleInfo.RoleID);
                }
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    ClanData.RemoveRole(roleInfo.RoleID);
                }
                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            EditorGUILayout.LabelField("Enter Role ID", boltStyle, GUILayout.Width(200));
            GUILayout.BeginHorizontal();
            RoleIDInput = EditorGUILayout.TextField(RoleIDInput, GUILayout.Width(200));
            if (GUILayout.Button("Add new Role", GUILayout.Width(100)))
            {
                ClanData.AddNewRole(RoleIDInput);
                RoleIDInput = string.Empty;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(20);

            EditorGUILayout.LabelField("Role permissions", titleStyle);
            GUILayout.Space(10);

            VerticalPos = EditorGUILayout.BeginScrollView(VerticalPos);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(100));
            GUILayout.Space(35);
            foreach (var persmission in RolePermissions)
            {
                EditorGUILayout.LabelField("<color=green>" + persmission.ToString() + "</color>", boltStyle);
            }
            GUILayout.EndVertical();

            for (int i = 0; i < roleList.Count; i++)
            {
                GUILayout.BeginVertical(GUILayout.Width(100));

                var roleInfo = roleList[i];
                var roleID = roleInfo.RoleID;
                EditorGUILayout.LabelField("<color=yellow>" + roleID + "</color>", boltStyle, GUILayout.Width(100));

                GUILayout.Space(15);

                foreach (var persmission in RolePermissions)
                {
                    var hasPermission = ClanData.HasPermissionForAction(roleID, persmission);
                    var lastPermision = hasPermission;
                    hasPermission = EditorGUILayout.Toggle(hasPermission);
                    if (lastPermision != hasPermission)
                    {
                        if (hasPermission)
                            ClanData.AddPermissionForAction(roleID, persmission);
                        else
                            ClanData.RemovePermissionForAction(roleID, persmission);
                    }
                }
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.EndScrollView();
        }

        private void DrawLevels()
        {
            GUIStyle btnStyle = new GUIStyle("Label");
            var levelTable = ClanData.GetLevelTable();
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            // draw level table
            if (levelTable != null)
            {
                var levelTitleStyle = new GUIStyle(GUI.skin.label);
                levelTitleStyle.fontStyle = FontStyle.Bold;

                if (levelTable.Table.Count == 0)
                {
                    GUILayout.Space(10);
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
                    EditorGUILayout.LabelField("Expirience table", titleStyle);
                    GUILayout.FlexibleSpace();
                    if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        SaveClanData(ClanData);
                    }
                    GUILayout.EndHorizontal();

                    // draw exp multiply
                    EditorGUILayout.LabelField("Expirience multiply", EditorUtils.TitleStyle);
                    levelTable.ExpMultiply = EditorGUILayout.FloatField(levelTable.GetExpMultiply(), GUILayout.Width(300));
                    GUILayout.Space(10);

                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("0", levelTitleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(20), GUILayout.Height(35) });
                    EditorGUILayout.LabelField("Null level is not configurable", new GUILayoutOption[] { GUILayout.MaxWidth(270), GUILayout.Height(35) });
                    GUILayout.FlexibleSpace();

                    if (EditorUtils.DrawButton("Registration reward", EditorData.AddColor, 14, new GUILayoutOption[] { GUILayout.Width(250), GUILayout.Height(30) }))
                    {
                        var prize = levelTable.RegistrationPrize;
                        ShowPrizeDialog(prize, false, result =>
                        {
                            levelTable.RegistrationPrize = result;
                            SaveClanData(ClanData);
                        });
                    }
                    if (EditorUtils.DrawButton("Registration events", EditorData.EventColor, 14, new GUILayoutOption[] { GUILayout.Width(250), GUILayout.Height(30) }))
                    {
                        EditorUtils.ShowClanEventWindow(levelTable.RegistrationEvents, onAdd =>
                        {
                            levelTable.RegistrationEvents = onAdd;
                            SaveClanData(ClanData);
                        });
                    }
                    GUILayout.EndHorizontal();
                    EditorUtils.DrawUILine(Color.grey, 1, 20);
                }

                for (int i = 0; i < levelTable.Table.Count; i++)
                {
                    var level = levelTable.Table[i];
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
                    GUILayout.Space(50);

                    if (EditorUtils.DrawButton("Edit rewards", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100) }))
                    {
                        ShowPrizeDialog(level.Reward, true, result =>
                        {
                            level.Reward = result;
                        });
                    }

                    if (EditorUtils.DrawButton("Edit events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100) }))
                    {
                        EditorUtils.ShowClanEventWindow(levelTable.LevelEvents[i], onAdd =>
                        {
                            levelTable.LevelEvents[i] = onAdd;
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

                if (levelTable.Table.Count != 0)
                {
                    // remove last level
                    if (EditorUtils.DrawButton("Remove last level", EditorData.RemoveColor, 14, AddButtonOptions))
                    {
                        if (levelTable == null || levelTable.Table.Count == 0)
                            return;
                        int lastLevelKey = levelTable.Table.Count - 1;
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

        private void DrawTasks()
        {
            ClanTask.ReDraw();
        }

        private void DrawIcons()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Display Mode", titleStyle, new GUILayoutOption[] { GUILayout.Width(100) });
            ClanIcons.DisplayMode = (ClanAvatarViewMode)EditorGUILayout.EnumPopup(ClanIcons.DisplayMode, new GUILayoutOption[] { GUILayout.Width(100) });
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Import Default", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                if (ClanIcons.DisplayMode == ClanAvatarViewMode.SIMPLE)
                {
                    if (ClanIcons.SimpleIconsExist())
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to import the default icons? Previous data will be overwritten",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                ImportDefaultSimpleIcons();
                                break;
                        }
                    }
                    else
                    {
                        ImportDefaultSimpleIcons();
                    }
                }
                else
                {
                    if (ClanIcons.ComplexIconsExist())
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to import the default icons? Previous data will be overwritten",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                ImportDefaultComplexIcons();
                                break;
                        }
                    }
                    else
                    {
                        ImportDefaultComplexIcons();
                    }
                }
            }
            GUILayout.EndHorizontal();

            if (ClanIcons.DisplayMode == ClanAvatarViewMode.SIMPLE)
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Default Clan Icon");
                ClanIcons.DefaultAvatar = (Sprite)EditorGUILayout.ObjectField((ClanIcons.DefaultAvatar as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                var defaultAvatar = ClanIcons.DefaultAvatar == null ? null : ClanIcons.DefaultAvatar.texture;
                GUILayout.Button(defaultAvatar, GUILayout.Width(100), GUILayout.Height(100));

                GUILayout.Space(10);
                for (int i = 0; i < ClanIcons.AllSprites.Count; i++)
                {
                    var iconData = ClanIcons.AllSprites[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(iconData.ID, GUILayout.Width(125));
                    if (GUILayout.Button("X", GUILayout.Width(23)))
                    {
                        ClanIcons.RemoveSprite(iconData.ID);
                        continue;
                    }
                    GUILayout.EndHorizontal();
                    iconData.Sprite = (Sprite)EditorGUILayout.ObjectField((iconData.Sprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                    var avatarTexture = iconData.Sprite == null ? null : iconData.Sprite.texture;
                    GUILayout.Button(avatarTexture, GUILayout.Width(100), GUILayout.Height(100));
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Enter Icon ID", titleStyle, GUILayout.Width(200));
                GUILayout.BeginHorizontal();
                IconIDInput = EditorGUILayout.TextField(IconIDInput, GUILayout.Width(200));
                if (GUILayout.Button("Add new Icon", GUILayout.Width(100)))
                {
                    if (string.IsNullOrEmpty(IconIDInput))
                        return;
                    ClanIcons.AllSprites.Add(new IconData
                    {
                        ID = IconIDInput
                    });
                    IconIDInput = string.Empty;
                    GUI.FocusControl(null);
                }
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Space(10);
                EditorGUILayout.LabelField("Colors Set", titleStyle);
                var colors = IconsColorsProperty.FindProperty("ColorsToSet");
                EditorGUILayout.PropertyField(colors);
                IconsColorsProperty.ApplyModifiedProperties();
                IconsColorsProperty.Update();

                GUILayout.Space(20);
                IconsToolBar = GUILayout.Toolbar(IconsToolBar, new string[] { "Background", "Foreground", "Emblem" });

                List<IconData> iconsList = null;

                switch (IconsToolBar)
                {
                    case 0:
                        iconsList = ClanIcons.Backgrounds;
                        break;
                    case 1:
                        iconsList = ClanIcons.Foregrounds;
                        break;
                    case 2:
                        iconsList = ClanIcons.Emblems;
                        break;
                    default:
                        break;
                }

                if (iconsList == null)
                    return;

                GUILayout.Space(10);
                for (int i = 0; i < iconsList.Count; i++)
                {
                    var iconData = iconsList[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(iconData.ID, GUILayout.Width(125));
                    if (GUILayout.Button("X", GUILayout.Width(23)))
                    {
                        switch (IconsToolBar)
                        {
                            case 0:
                                ClanIcons.RemoveBackground(iconData.ID);
                                break;
                            case 1:
                                ClanIcons.RemoveForeground(iconData.ID);
                                break;
                            case 2:
                                ClanIcons.RemoveEmblems(iconData.ID);
                                break;
                            default:
                                break;
                        }
                        continue;
                    }
                    GUILayout.EndHorizontal();
                    iconData.Sprite = (Sprite)EditorGUILayout.ObjectField((iconData.Sprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                    var avatarTexture = iconData.Sprite == null ? null : iconData.Sprite.texture;
                    GUILayout.Button(avatarTexture, GUILayout.Width(100), GUILayout.Height(100));
                }

                GUILayout.Space(10);
                EditorGUILayout.LabelField("Enter Icon ID", titleStyle, GUILayout.Width(200));
                GUILayout.BeginHorizontal();
                IconIDInput = EditorGUILayout.TextField(IconIDInput, GUILayout.Width(200));
                if (GUILayout.Button("Add new Icon", GUILayout.Width(100)))
                {
                    iconsList.Add(new IconData
                    {
                        ID = IconIDInput
                    });
                    IconIDInput = string.Empty;
                    GUI.FocusControl(null);
                }
                GUILayout.EndHorizontal();
            }

            ClanIcons.Save();
        }

        private void GetClanData()
        {
            ShowProgress();
            var keys = new List<string>();
            keys.Add(TitleKeys.ClanMetaDataKey);
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
            bool keyExist = dictionary.ContainsKey(TitleKeys.ClanMetaDataKey);
            string rawData = keyExist ? dictionary[TitleKeys.ClanMetaDataKey] : JsonPlugin.EMPTY_JSON;
            try
            {
                ClanData = JsonPlugin.FromJsonDecompress<ClanMetaData>(rawData);
            }
            catch
            {
                ClanData = JsonPlugin.FromJson<ClanMetaData>(rawData);
            }
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveClanData(ClanMetaData data)
        {
            ShowProgress();
            var rawData = JsonPlugin.ToJsonCompress(data);
            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.ClanMetaDataKey,
                Value = rawData
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveClanData, OnSetDataFailed);
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

        private void OnSaveClanData(SetTitleDataResult result)
        {
            HideProgress();
            GetClanData();
        }

        private void OnSetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void ImportDefaultLevels()
        {
            var defaultPreset = GetDefaultLevelPreset();
            ClanData.LevelTable = defaultPreset;
            SaveClanData(ClanData);
        }

        private ClanLevelTable GetDefaultLevelPreset()
        {
            var newSet = new ClanLevelTable();

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

        private void AddNewLevel()
        {
            var levelDetail = new LevelDetail
            {
                Expirience = 0
            };
            var levelTable = ClanData.GetLevelTable();
            levelTable.AddLevelDetail(levelDetail);
        }

        private void RemoveLevelDetail(int index)
        {
            var levelTable = ClanData.GetLevelTable();
            levelTable.RemoveLevelDetailAt(index);
        }

        private void RemoveLevelGroup()
        {
            ClanData.LevelTable = new ClanLevelTable();
        }

        private void ImportDefaultComplexIcons()
        {
            ClanIcons.ColorsToSet.Clear();
            ClanIcons.ColorsToSet.TrimExcess();
            ClanIcons.ColorsToSet.Add(Color.red);
            ClanIcons.ColorsToSet.Add(Color.green);
            ClanIcons.ColorsToSet.Add(Color.yellow);
            ClanIcons.ColorsToSet.Add(Color.blue);
            ClanIcons.ColorsToSet.Add(Color.black);
            ClanIcons.ColorsToSet.Add(Color.white);

            ClanIcons.Backgrounds.Clear();
            ClanIcons.Backgrounds.TrimExcess();

            ClanIcons.Foregrounds.Clear();
            ClanIcons.Foregrounds.TrimExcess();

            ClanIcons.Emblems.Clear();
            ClanIcons.Emblems.TrimExcess();

            for (int i = 0; i < 6; i++)
            {
                var index = i + 1;
                var avatarID = "background" + index;
                var clanSprite = ResourcesUtils.GetBackgroundClanIcon(avatarID);
                ClanIcons.Backgrounds.Add(new IconData
                {
                    ID = avatarID,
                    Sprite = clanSprite
                });
            }

            for (int i = 0; i < 5; i++)
            {
                var index = i + 1;
                var avatarID = "foreground" + index;
                var clanSprite = ResourcesUtils.GetForegroundClanIcon(avatarID);
                ClanIcons.Foregrounds.Add(new IconData
                {
                    ID = avatarID,
                    Sprite = clanSprite
                });
            }

            for (int i = 0; i < 6; i++)
            {
                var index = i + 1;
                var avatarID = "emblem" + index;
                var clanSprite = ResourcesUtils.GetEmblemsClanIcon(avatarID);
                ClanIcons.Emblems.Add(new IconData
                {
                    ID = avatarID,
                    Sprite = clanSprite
                });
            }

            ClanIcons.Save();
        }

        private void ImportDefaultSimpleIcons()
        {
            ClanIcons.AllSprites.Clear();
            ClanIcons.AllSprites.TrimExcess();

            for (int i = 0; i < 6; i++)
            {
                var index = i + 1;
                var avatarID = "clan_icon_" + index;
                var clanSprite = ResourcesUtils.GetSimpleClanIcon(avatarID);
                ClanIcons.SaveSprite(avatarID, clanSprite);
            }
            ClanIcons.DefaultAvatar = ResourcesUtils.GetSimpleClanIcon("clan_icon_default");
            ClanIcons.Save();
        }
    }
}
#endif

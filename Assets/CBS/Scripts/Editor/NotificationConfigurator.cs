#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.AdminModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Action = System.Action;

namespace CBS.Editor
{
    public class NotificationConfigurator : BaseConfigurator
    {
        protected override string Title => "Notification Congiguration";
        protected string[] Titles => new string[] { "Notifications", "Limitations", "Logs" };

        protected override bool DrawScrollView => true;

        private Rect CategoriesRect = new Rect(0, 0, 150, 700);
        private Rect ItemsRect = new Rect(200, 100, 855, 700);
        private Vector2 PositionScroll { get; set; }

        private NotificationsData NotificationData { get; set; }
        private string SelectedCategory { get; set; }
        private int SelectedToolBar { get; set; }
        private int SelectedTitleBar { get; set; }
        private int CategoryIndex { get; set; }

        private List<CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }
        private Categories CachedLootBoxCategories { get; set; }

        private string TitleEntityToken;
        private EditorData EditorData { get; set; }
        private Dictionary<string, ObjectCustomDataDrawer<CBSNotificationCustomData>> CustomDataDrawer { get; set; }

        private Dictionary<string, bool> ExtendStates;
        private List<CBSNotification> LogList;

        private Texture2D NotificationTitleTexture;
        private Texture2D NotificationExtendTexture;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            ExtendStates = new Dictionary<string, bool>();
            EditorData = CBSScriptable.Get<EditorData>();
            CustomDataDrawer = new Dictionary<string, ObjectCustomDataDrawer<CBSNotificationCustomData>>();
            NotificationTitleTexture = EditorUtils.MakeColorTexture(EditorData.NotificationTitle);
            NotificationExtendTexture = EditorUtils.MakeColorTexture(EditorData.NotificationExtend);
            GetNotificationData();
        }

        protected override void OnDrawInside()
        {
            DrawTitles();
            DrawToggles();
        }

        private void DrawToggles()
        {
            if (NotificationData == null)
                return;

            using (var areaScope = new GUILayout.AreaScope(ItemsRect))
            {
                var lastTitleIndex = SelectedTitleBar;
                SelectedTitleBar = GUILayout.Toolbar(SelectedTitleBar, Titles, GUILayout.MaxWidth(1200));
                GUILayout.Space(10);
                PositionScroll = GUILayout.BeginScrollView(PositionScroll);
                if (SelectedTitleBar == 0)
                    DrawNotifications();
                else if (SelectedTitleBar == 1)
                    DrawLimitations();
                else if (SelectedTitleBar == 2)
                    DrawLogs();

                if (lastTitleIndex != SelectedTitleBar)
                {
                    if (SelectedTitleBar == 2)
                    {
                        UpdateLogList();
                    }
                    else
                    {
                        LogList = null;
                    }
                }

                GUILayout.Space(110);
                GUILayout.EndScrollView();
            }
        }

        private void DrawLimitations()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                SaveNotification(NotificationData);
            }

            GUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Notification TTL", titleStyle);
            NotificationData.TTL = (CBSTTL)EditorGUILayout.EnumPopup(NotificationData.TTL, new GUILayoutOption[] { GUILayout.Width(200) });
            if (NotificationData.TTL == CBSTTL.CUSTOM_VALUE)
            {
                GUILayout.Space(5);
                if (NotificationData.NotificationSecondsTTL == null)
                {
                    NotificationData.NotificationSecondsTTL = 1000;
                }
                EditorGUILayout.LabelField("Life time in seconds", titleStyle);
                NotificationData.NotificationSecondsTTL = EditorGUILayout.IntField((int)NotificationData.NotificationSecondsTTL, GUILayout.Width(200));
            }
            EditorGUILayout.HelpBox("The lifetime of messages on the server. Limiting helps optimize azure cost.", MessageType.Info);
        }

        private void DrawLogs()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;
            GUIStyle iconStyle = new GUIStyle("Label");
            // draw titles
            GUILayout.BeginHorizontal();
            GUILayout.Space(67);
            EditorGUILayout.LabelField("ID", titleStyle, GUILayout.Width(120f));
            EditorGUILayout.LabelField("Instance ID", titleStyle, GUILayout.Width(300f));
            EditorGUILayout.LabelField("Title", titleStyle, GUILayout.Width(200f));
            EditorGUILayout.LabelField("Date", titleStyle, GUILayout.Width(150f));
            GUILayout.EndHorizontal();

            if (LogList == null)
                return;

            GUILayout.Space(10);

            var logsCount = LogList.Count;
            for (int i = 0; i < logsCount; i++)
            {
                var log = LogList[i];

                GUILayout.BeginHorizontal();
                var messageTexture = ResourcesUtils.GetMessageImage();
                GUILayout.Button(messageTexture, iconStyle, GUILayout.Width(30), GUILayout.Height(30));
                GUILayout.Space(30f);
                EditorGUILayout.LabelField(log.ID, GUILayout.Width(120f));
                EditorGUILayout.LabelField(log.InstanceID, GUILayout.Width(300f));
                EditorGUILayout.LabelField(log.Title, GUILayout.Width(200f));
                EditorGUILayout.LabelField(log.CreatedDate.ToLocalTime().ToString(), GUILayout.Width(200f));
                GUILayout.EndHorizontal();

                GUILayout.Space(5);
            }
        }

        private void DrawTitles()
        {
            if (NotificationData == null)
                return;
            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                GUILayout.BeginVertical();

                var categoriesMenu = NotificationData.GetCategories();
                int categoryHeight = 30;
                int categoriesCount = categoriesMenu.Count;

                if (categoriesCount > 0)
                {
                    CategoryIndex = GUI.SelectionGrid(new Rect(0, 100, 150, categoryHeight * categoriesCount), CategoryIndex, categoriesMenu.ToArray(), 1);
                    string selctedCategory = categoriesMenu[CategoryIndex];

                    SelectedCategory = categoriesMenu[CategoryIndex];
                }

                GUILayout.Space(30);
                GUILayout.Space(30);
                var oldColor = GUI.color;
                GUI.backgroundColor = EditorData.AddColor;
                var style = new GUIStyle(GUI.skin.button);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;
                if (GUI.Button(new Rect(0, 130 + categoryHeight * categoriesCount, 150, categoryHeight), "Add new Category", style))
                {
                    ModifyCateroriesWindow.Show(onModify =>
                    {
                        NotificationData.SetCategories(onModify);
                        SaveNotification(NotificationData);
                    }, NotificationData.Categories);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
            }
        }

        private void DrawNotifications()
        {
            //NotificationTitleTexture = EditorUtils.MakeColorTexture(EditorData.NotificationTitle);
            //NotificationExtendTexture = EditorUtils.MakeColorTexture(EditorData.NotificationExtend);

            GUIStyle titleBackgroundStyle = new GUIStyle("HelpBox");
            titleBackgroundStyle.normal.background = NotificationTitleTexture;

            GUIStyle extendBackgroundStyle = new GUIStyle("HelpBox");
            extendBackgroundStyle.normal.background = NotificationExtendTexture;

            var notificationList = NotificationData.GetNotifications();
            var categoryList = NotificationData.GetCategoriesToSelect();

            if (SelectedCategory != NotificationsData.ALL_CATEGORY)
            {
                notificationList = notificationList.Where(x => x.Category == SelectedCategory).ToList();
            }
            var notificationsCount = notificationList.Count;

            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            GUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                SaveNotification(NotificationData);
            }

            if (EditorUtils.DrawButton("Add Notification", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                AddNotificationWindow.Show(onAdd =>
                {
                    var newNotification = onAdd;
                    AddNotification(newNotification);
                }, SelectedCategory);
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // draw notifications
            for (int i = 0; i < notificationsCount; i++)
            {
                var notification = notificationList[i];
                var notificationID = notification.ID;
                var extend = ExtendStates.ContainsKey(notificationID) ? ExtendStates[notificationID] : false;

                using (var horizontalScope = new GUILayout.VerticalScope(titleBackgroundStyle))
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(notification.Title, titleStyle);
                    GUILayout.FlexibleSpace();

                    if (EditorUtils.DrawButton("Send", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(30) }))
                    {
                        SendNotification(notificationID);
                    }

                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(30) }))
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove this notification?",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveNotification(notification);
                                break;
                        }
                    }

                    if (EditorUtils.DrawButton("Extend", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(30) }))
                    {
                        ExtendStates[notificationID] = !extend;
                    }

                    GUILayout.EndHorizontal();
                }

                if (extend)
                {
                    using (var horizontalScope = new GUILayout.VerticalScope(extendBackgroundStyle))
                    {
                        // draw id
                        EditorGUILayout.LabelField("Notification ID", titleStyle);
                        EditorGUILayout.LabelField(notification.ID);
                        GUILayout.Space(5);

                        // draw title
                        EditorGUILayout.LabelField("Title", titleStyle);
                        notification.Title = EditorGUILayout.TextField(notification.Title, new GUILayoutOption[] { GUILayout.Width(400) });
                        GUILayout.Space(5);

                        // draw description
                        var descriptionTitle = new GUIStyle(GUI.skin.textField);
                        descriptionTitle.wordWrap = true;
                        EditorGUILayout.LabelField("Message", titleStyle);
                        notification.Message = EditorGUILayout.TextArea(notification.Message, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });
                        GUILayout.Space(5);

                        // draw external url
                        EditorGUILayout.LabelField("External URL", titleStyle);
                        notification.ExternalURL = EditorGUILayout.TextField(notification.ExternalURL, new GUILayoutOption[] { GUILayout.Width(400) });
                        GUILayout.Space(5);

                        // draw category
                        EditorGUILayout.LabelField("Category", titleStyle);
                        var category = notification.Category;
                        var categoryIndex = string.IsNullOrEmpty(category) ? 0 : categoryList.IndexOf(category);
                        if (categoryIndex < 0)
                            categoryIndex = 0;
                        categoryIndex = EditorGUILayout.Popup(categoryIndex, categoryList.ToArray());
                        if (categoryIndex == 0)
                        {
                            notification.Category = string.Empty;
                        }
                        else
                        {
                            var selected = categoryList[categoryIndex];
                            notification.Category = selected;
                        }
                        GUILayout.Space(5);

                        // draw custom data
                        EditorGUILayout.LabelField("Custom Data", titleStyle);
                        if (!CustomDataDrawer.ContainsKey(notificationID))
                        {
                            CustomDataDrawer[notificationID] = new ObjectCustomDataDrawer<CBSNotificationCustomData>(PlayfabUtils.ITEM_CUSTOM_DATA_SIZE, 830f);
                        }
                        CustomDataDrawer[notificationID].Draw(notification);
                        
                        // draw new players properties
                        EditorGUILayout.LabelField("Visible for new players?", titleStyle);
                        notification.VisibleForNewPlayer = EditorGUILayout.Toggle(notification.VisibleForNewPlayer);
                        EditorGUILayout.HelpBox("If true - new players will receive this notification after registration", MessageType.Info);

                        // draw rewards
                        EditorGUILayout.LabelField("Has reward?", titleStyle);
                        notification.HasReward = EditorGUILayout.Toggle(notification.HasReward);
                        if (notification.HasReward)
                        {
                            GUILayout.BeginHorizontal();
                            var reward = notification.Reward ?? new RewardObject();
                            EditorUtils.DrawReward(reward, 50, ItemDirection.HORIZONTAL);

                            GUILayout.FlexibleSpace();

                            if (EditorUtils.DrawButton("Edit reward", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(120) }))
                            {
                                ShowPrizeDialog(reward, true, result =>
                                {
                                    notification.Reward = result;
                                });
                            }

                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.Space(5);
            }
        }

        private void GetNotificationData()
        {
            ShowProgress();
            var keys = new List<string>();
            keys.Add(TitleKeys.NotificationsDataKey);

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
            bool keyExist = dictionary.ContainsKey(TitleKeys.NotificationsDataKey);
            var rawData = keyExist ? dictionary[TitleKeys.NotificationsDataKey] : JsonPlugin.EMPTY_JSON;
            NotificationData = new NotificationsData();
            try
            {
                NotificationData = JsonPlugin.FromJsonDecompress<NotificationsData>(rawData);
            }
            catch
            {
                NotificationData = JsonPlugin.FromJson<NotificationsData>(rawData);
            }
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveNotification(NotificationsData notificationData)
        {
            ShowProgress();

            string rawData = JsonPlugin.ToJsonCompress(notificationData);
            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.NotificationsDataKey,
                Value = rawData
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveNotifications, OnSaveDataFailed);
        }

        private void OnSaveNotifications(SetTitleDataResult result)
        {
            HideProgress();
            GetNotificationData();
        }

        private void OnSaveDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void RemoveNotification(CBSNotification instance)
        {
            NotificationData.RemoveNotification(instance);
            SaveNotification(NotificationData);
        }

        private void AddNotification(CBSNotification instance)
        {
            NotificationData.AddNotification(instance);
            SaveNotification(NotificationData);
        }

        private void ShowPrizeDialog(RewardObject reward, bool includeCurrencies, Action<RewardObject> modifyCallback)
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
                                reward = reward
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
                    reward = reward
                });
                GUIUtility.ExitGUI();
            }
        }

        private void SendNotification(string notificatiionID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.SendNotificationMethod,
                    FunctionParameter = new FunctionSendNotificationRequest
                    {
                        NotificationID = notificatiionID
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet =>
                {
                    var cbsError = OnGet.GetCBSError();
                    if (cbsError != null)
                    {
                        AddErrorLog(cbsError);
                        EditorUtility.DisplayDialog("Failed!", cbsError.Message, "OK");
                        HideProgress();
                    }
                    else
                    {
                        var functionSendNotificationResult = OnGet.GetResult<FunctionSendNotificationResult>();
                        var notification = functionSendNotificationResult.Notification;
                        HideProgress();
                        EditorUtility.DisplayDialog("Success!", "Message sent!", "OK");
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void UpdateLogList()
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.GetNotificationsMethod,
                    FunctionParameter = new FunctionGetNotificationsRequest
                    {
                        Request = NotificationRequest.GLOBAL,
                        MaxCount = 100
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet =>
                {
                    var cbsError = OnGet.GetCBSError();
                    if (cbsError != null)
                    {
                        AddErrorLog(cbsError);
                        HideProgress();
                    }
                    else
                    {
                        var functionGetNotificationsResult = OnGet.GetResult<FunctionGetNotificationsResult>();
                        var notifications = functionGetNotificationsResult.Notifications;
                        LogList = notifications;
                        HideProgress();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
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
    }
}
#endif


using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddTaskEventsWindow : EditorWindow
    {
        private static Action<TaskEventContainer> OnAdd { get; set; }
        private static TaskEventContainer Container { get; set; }
        private static TitleDataContainer TitleContainer { get; set; }
        private static Dictionary<string, CBSTitleData> Titles { get; set; }
        private Texture2D BackgroundTex { get; set; }
        private EditorData EditorData { get; set; }
        private TaskEventType EventToAdd { get; set; }
        private Dictionary<int, FunctionTaskRequestDrawer<ExecuteFunctionEventArgs>> DrawerPool { get; set; }
        private Dictionary<int, ObjectCustomDataDrawer<TitleCustomData>> FunctionsArgsPool { get; set; }
        private Vector2 ScrollPos { get; set; }

        public static void Show(TaskEventContainer container, TitleDataContainer titleContainer, Action<TaskEventContainer> onAdd)
        {
            OnAdd = onAdd;
            Container = container;
            TitleContainer = titleContainer;
            Titles = TitleContainer.GetAll();

            AddTaskEventsWindow window = ScriptableObject.CreateInstance<AddTaskEventsWindow>();
            window.maxSize = new Vector2(400, 700);
            window.minSize = window.maxSize;
            window.ShowUtility();
            window.Init();
        }

        private void Hide()
        {
            this.Close();
        }

        public void Init()
        {
            EditorData = CBSScriptable.Get<EditorData>();
            BackgroundTex = EditorUtils.MakeColorTexture(EditorData.ProfileEventBackground);
            DrawerPool = new Dictionary<int, FunctionTaskRequestDrawer<ExecuteFunctionEventArgs>>();
            FunctionsArgsPool = new Dictionary<int, ObjectCustomDataDrawer<TitleCustomData>>();
        }

        void OnGUI()
        {
            using (var areaScope = new GUILayout.AreaScope(new Rect(0, 0, 400, 675)))
            {
                ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

                if (Container == null)
                    return;

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 12;
                GUIStyle recipeBoxStyle = new GUIStyle("HelpBox");
                recipeBoxStyle.normal.background = BackgroundTex;

                // draw events
                if (Container.Events != null && Container.Events.Count > 0)
                {
                    GUILayout.Space(10);

                    for (int i = 0; i < Container.Events.Count; i++)
                    {
                        var eventBody = Container.Events[i];
                        var eventType = eventBody.EventType;
                        var instanceID = eventBody.GetHashCode();

                        using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Label("Event Type");
                            GUILayout.FlexibleSpace();
                            if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12))
                            {
                                Container.RemoveEvent(eventBody);
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Label(eventType.ToString(), titleStyle);

                            switch (eventType)
                            {
                                case TaskEventType.RESET_PROFILE_LEADERBOARD:
                                    var eventAdd = eventBody.GetContent<TaskResetLeaderboardEvent>();
                                    GUILayout.Label("Statistic name");
                                    eventAdd.StatisticName = GUILayout.TextField(eventAdd.StatisticName, GUILayout.Width(200));
                                    eventBody.SaveContent(eventAdd);
                                    break;
                                case TaskEventType.UPDATE_PROFILE_EXP_MULTIPLY:
                                    var eventUpdateExpMultipler = eventBody.GetContent<TaskSetProfileExpMultiplyEvent>();
                                    GUILayout.Label("Experience multiplier");
                                    eventUpdateExpMultipler.ExpMultiply = EditorGUILayout.FloatField(eventUpdateExpMultipler.ExpMultiply, GUILayout.Width(200));
                                    eventBody.SaveContent(eventUpdateExpMultipler);
                                    break;
                                case TaskEventType.ENANLE_OR_DISABLE_STORE:
                                    var eventDisableStore = eventBody.GetContent<TaskSetStoreActivityEvent>();
                                    GUILayout.Label("Store ID");
                                    eventDisableStore.StoreID = GUILayout.TextField(eventDisableStore.StoreID, GUILayout.Width(200));
                                    GUILayout.Label("Enabled ?");
                                    eventDisableStore.Enabled = EditorGUILayout.Toggle(eventDisableStore.Enabled);
                                    eventBody.SaveContent(eventDisableStore);
                                    break;
                                case TaskEventType.SET_STORE_ITEM_PRICE:
                                    var eventItemPrice = eventBody.GetContent<TaskSetStoreItemPriceEvent>();
                                    GUILayout.Label("Store ID");
                                    eventItemPrice.StoreID = GUILayout.TextField(eventItemPrice.StoreID, GUILayout.Width(200));
                                    GUILayout.Label("Item ID");
                                    eventItemPrice.ItemID = GUILayout.TextField(eventItemPrice.ItemID, GUILayout.Width(200));
                                    GUILayout.Label("Currency Code");
                                    eventItemPrice.CurrencyCode = GUILayout.TextField(eventItemPrice.CurrencyCode, GUILayout.Width(200));
                                    GUILayout.Label("Currency Value");
                                    eventItemPrice.CurrencyValue = EditorGUILayout.IntField(eventItemPrice.CurrencyValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventItemPrice);
                                    break;
                                case TaskEventType.ENABLE_OR_DISABLE_ITEM_IN_STORE:
                                    var eventItemActivity = eventBody.GetContent<TaskSetItemStoreActivityEvent>();
                                    GUILayout.Label("Store ID");
                                    eventItemActivity.StoreID = GUILayout.TextField(eventItemActivity.StoreID, GUILayout.Width(200));
                                    GUILayout.Label("Item ID");
                                    eventItemActivity.ItemID = GUILayout.TextField(eventItemActivity.ItemID, GUILayout.Width(200));
                                    GUILayout.Label("Enabled ?");
                                    eventItemActivity.Enabled = EditorGUILayout.Toggle(eventItemActivity.Enabled);
                                    eventBody.SaveContent(eventItemActivity);
                                    break;
                                case TaskEventType.START_STORE_GLOBAL_SPECIAL_OFFER:
                                    var eventStartSpecialOffer = eventBody.GetContent<TaskStartSpecialOfferEvent>();
                                    GUILayout.Label("Item ID");
                                    eventStartSpecialOffer.ItemID = GUILayout.TextField(eventStartSpecialOffer.ItemID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventStartSpecialOffer);
                                    break;
                                case TaskEventType.STOP_STORE_GLOBAL_SPECIAL_OFFER:
                                    var eventStopSpecialOffer = eventBody.GetContent<TaskStopSpecialOfferEvent>();
                                    GUILayout.Label("Item ID");
                                    eventStopSpecialOffer.ItemID = GUILayout.TextField(eventStopSpecialOffer.ItemID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventStopSpecialOffer);
                                    break;
                                case TaskEventType.SEND_MESSAGE_TO_CHAT:
                                    var eventChatMessage = eventBody.GetContent<TaskSendMessageToChatEvent>();
                                    GUILayout.Label("Chat ID");
                                    eventChatMessage.ChatID = GUILayout.TextField(eventChatMessage.ChatID, GUILayout.Width(200));
                                    GUILayout.Label("Message");
                                    eventChatMessage.ChatMessage = GUILayout.TextField(eventChatMessage.ChatMessage, GUILayout.Width(200));
                                    eventBody.SaveContent(eventChatMessage);
                                    break;
                                case TaskEventType.ENABLE_OR_DISABLE_CALENDAR:
                                    var eventCalendarActivity = eventBody.GetContent<TaskSetCalendarActivityEvent>();
                                    GUILayout.Label("Calendar ID");
                                    eventCalendarActivity.CalendarID = GUILayout.TextField(eventCalendarActivity.CalendarID, GUILayout.Width(200));
                                    GUILayout.Label("Enabled ?");
                                    eventCalendarActivity.Enabled = EditorGUILayout.Toggle(eventCalendarActivity.Enabled);
                                    eventBody.SaveContent(eventCalendarActivity);
                                    break;
                                case TaskEventType.START_BATTLE_PASS:
                                    var eventStartBattlePass = eventBody.GetContent<TaskStartBattlePassEvent>();
                                    GUILayout.Label("Battle Pass ID");
                                    eventStartBattlePass.BattlePassID = GUILayout.TextField(eventStartBattlePass.BattlePassID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventStartBattlePass);
                                    break;
                                case TaskEventType.STOP_BATTLE_PASS:
                                    var eventStopBattlePass = eventBody.GetContent<TaskStopBattlePassEvent>();
                                    GUILayout.Label("Battle Pass ID");
                                    eventStopBattlePass.BattlePassID = GUILayout.TextField(eventStopBattlePass.BattlePassID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventStopBattlePass);
                                    break;
                                case TaskEventType.SEND_NOTIFICATION:
                                    var eventSendNotification = eventBody.GetContent<TaskSendNotificationEvent>();
                                    GUILayout.Label("Notification ID");
                                    eventSendNotification.NotificationID = GUILayout.TextField(eventSendNotification.NotificationID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventSendNotification);
                                    break;
                                case TaskEventType.REWARD_MEMBERS_OF_TOP_CLAN:
                                    var eventRewardAllMembers = eventBody.GetContent<TaskRewardMembersOfTopClansEvent>();
                                    GUILayout.Label("N Top");
                                    eventRewardAllMembers.nTop = EditorGUILayout.IntField(eventRewardAllMembers.nTop, GUILayout.Width(200));
                                    GUILayout.Label("Statistic name");
                                    eventRewardAllMembers.StatisticName = GUILayout.TextField(eventRewardAllMembers.StatisticName, GUILayout.Width(200));
#if ENABLE_PLAYFABADMIN_API
                                    if (GUILayout.Button("Reward", GUILayout.Width(200)))
                                    {
                                        BaseConfigurator.Get<ClanTaskConfigurator>().ShowRewardDialog(eventRewardAllMembers.Reward ?? new RewardObject(), true, reward => 
                                        {
                                            eventRewardAllMembers.Reward = reward;
                                            eventBody.SaveContent(eventRewardAllMembers);
                                        });
                                    }
#endif
                                    eventBody.SaveContent(eventRewardAllMembers);
                                    break;
                                case TaskEventType.EXECUTE_EVENTS_FOR_MEMBERS_OF_TOP_CLAN:
                                    var eventExecutionAllMembers = eventBody.GetContent<TaskExecuteEventMembersOfTopClansEvent>();
                                    GUILayout.Label("N Top");
                                    eventExecutionAllMembers.nTop = EditorGUILayout.IntField(eventExecutionAllMembers.nTop, GUILayout.Width(200));
                                    GUILayout.Label("Statistic name");
                                    eventExecutionAllMembers.StatisticName = GUILayout.TextField(eventExecutionAllMembers.StatisticName, GUILayout.Width(200));
                                    if (GUILayout.Button("Events", GUILayout.Width(200)))
                                    {
                                        EditorUtils.ShowProfileEventWindow(eventExecutionAllMembers.Events ?? new ProfileEventContainer(), onAdd =>
                                        {
                                            eventExecutionAllMembers.Events = onAdd;
                                            eventBody.SaveContent(eventExecutionAllMembers);
                                        });
                                    }
                                    eventBody.SaveContent(eventExecutionAllMembers);
                                    break;
                                case TaskEventType.UPDATE_TITLE_DATA:
                                    var eventTitleData = eventBody.GetContent<TaskUpdateTitleDataEvent>();
                                    var lastSavedData = eventTitleData.RawDataToUpdate;
                                    var titleArray = Titles.Select(x => x.Key).ToArray();
                                    var titleIndex = titleArray.ToList().IndexOf(eventTitleData.TitleID);
                                    if (titleIndex < 0)
                                        titleIndex = 0;
                                    var cbsTitleData = !string.IsNullOrEmpty(eventTitleData.TitleID) ? Titles[eventTitleData.TitleID] : Titles.ElementAt(titleIndex).Value;
                                    if (!string.IsNullOrEmpty(lastSavedData))
                                        cbsTitleData = JsonPlugin.FromJson<CBSTitleData>(lastSavedData);
                                    GUILayout.Label("Title ID");
                                    var lastIndex = titleIndex;
                                    titleIndex = EditorGUILayout.Popup(titleIndex, titleArray);
                                    if (lastIndex != titleIndex)
                                    {
                                        cbsTitleData = Titles.ElementAt(titleIndex).Value;
                                    }
                                    var drawerID = instanceID + titleIndex;
                                    eventTitleData.TitleID = titleArray[titleIndex];
                                    if (!FunctionsArgsPool.ContainsKey(drawerID))
                                    {
                                        FunctionsArgsPool[drawerID] = new ObjectCustomDataDrawer<TitleCustomData>(PlayfabUtils.TITLE_DATA_SIZE, 400f);
                                        FunctionsArgsPool[drawerID].DrawOnlyValues = true;
                                        FunctionsArgsPool[drawerID].AutoReset = false;
                                        FunctionsArgsPool[drawerID].Reset();
                                    }
                                    var titleDrawer = FunctionsArgsPool[drawerID];
                                    titleDrawer.Draw(cbsTitleData);
                                    eventTitleData.RawDataToUpdate = JsonPlugin.ToJson(cbsTitleData);
                                    eventBody.SaveContent(eventTitleData);

                                    break;
                                case TaskEventType.EXECUTE_FUNCTION:
                                    var eventFunction = eventBody.GetContent<TaskExecuteFunctionEvent>();
                                    GUILayout.Label("Function Name");
                                    eventFunction.FunctionName = GUILayout.TextField(eventFunction.FunctionName, GUILayout.Width(200));

                                    if (!DrawerPool.ContainsKey(instanceID))
                                    {
                                        DrawerPool[instanceID] = new FunctionTaskRequestDrawer<ExecuteFunctionEventArgs>();
                                    }
                                    var requestDrawer = DrawerPool[instanceID];
                                    eventFunction.RequestRaw = requestDrawer.Draw(eventFunction.RequestRaw);

                                    eventBody.SaveContent(eventFunction);
                                    break;
                                default:
                                    break;
                            }
                            GUILayout.Space(5);
                        }

                        GUILayout.Space(10);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No events found", MessageType.Info);
                }

                // add option
                GUILayout.Space(10);
                GUILayout.Label("Select event to add", titleStyle);
                GUILayout.BeginHorizontal();
                EventToAdd = (TaskEventType)EditorGUILayout.EnumPopup(EventToAdd, GUILayout.Width(290));
                if (GUILayout.Button("Add event", GUILayout.Width(100)))
                {
                    AddEvent(EventToAdd);
                }
                GUILayout.EndHorizontal();

                EditorGUILayout.EndScrollView();
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save"))
            {
                OnAdd?.Invoke(Container);
                Hide();
            }
            if (GUILayout.Button("Close"))
            {
                Hide();
            }
            GUILayout.EndHorizontal();
        }

        private void AddEvent(TaskEventType eventType)
        {
            TaskEvent profileEvent = null;
            switch (eventType)
            {
                case TaskEventType.RESET_PROFILE_LEADERBOARD:
                    profileEvent = new TaskResetLeaderboardEvent();
                    break;
                case TaskEventType.UPDATE_PROFILE_EXP_MULTIPLY:
                    profileEvent = new TaskSetProfileExpMultiplyEvent();
                    break;
                case TaskEventType.EXECUTE_FUNCTION:
                    profileEvent = new TaskExecuteFunctionEvent();
                    break;
                case TaskEventType.ENANLE_OR_DISABLE_STORE:
                    profileEvent = new TaskSetStoreActivityEvent();
                    break;
                case TaskEventType.ENABLE_OR_DISABLE_ITEM_IN_STORE:
                    profileEvent = new TaskSetItemStoreActivityEvent();
                    break;
                case TaskEventType.SET_STORE_ITEM_PRICE:
                    profileEvent = new TaskSetStoreItemPriceEvent();
                    break;
                case TaskEventType.START_STORE_GLOBAL_SPECIAL_OFFER:
                    profileEvent = new TaskStartSpecialOfferEvent();
                    break;
                case TaskEventType.STOP_STORE_GLOBAL_SPECIAL_OFFER:
                    profileEvent = new TaskStopSpecialOfferEvent();
                    break;
                case TaskEventType.SEND_MESSAGE_TO_CHAT:
                    profileEvent = new TaskSendMessageToChatEvent();
                    break;
                case TaskEventType.ENABLE_OR_DISABLE_CALENDAR:
                    profileEvent = new TaskSetCalendarActivityEvent();
                    break;
                case TaskEventType.START_BATTLE_PASS:
                    profileEvent = new TaskStartBattlePassEvent();
                    break;
                case TaskEventType.STOP_BATTLE_PASS:
                    profileEvent = new TaskStopBattlePassEvent();
                    break;
                case TaskEventType.SEND_NOTIFICATION:
                    profileEvent = new TaskSendNotificationEvent();
                    break;
                case TaskEventType.UPDATE_TITLE_DATA:
                    profileEvent = new TaskUpdateTitleDataEvent();
                    break;
                case TaskEventType.REWARD_MEMBERS_OF_TOP_CLAN:
                    profileEvent = new TaskRewardMembersOfTopClansEvent();
                    break;
                case TaskEventType.EXECUTE_EVENTS_FOR_MEMBERS_OF_TOP_CLAN:
                    profileEvent = new TaskExecuteEventMembersOfTopClansEvent();
                    break;
                default:
                    Debug.LogError("Unrecognized Option");
                    break;
            }
            profileEvent.EventType = eventType;
            Container.AddEvent(profileEvent);
        }
    }
}

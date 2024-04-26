using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddProfileEventsWindow : EditorWindow
    {
        private static Action<ProfileEventContainer> OnAdd { get; set; }
        private static ProfileEventContainer Container { get; set; }
        private Texture2D BackgroundTex { get; set; }
        private EditorData EditorData { get; set; }
        private ProfileEventType EventToAdd { get; set; }
        private Dictionary<int, FunctionRequestDrawer<ExecuteFunctionProfileArgs>> DrawerPool { get; set; }
        private Vector2 ScrollPos { get; set; }

        public static void Show(ProfileEventContainer container, Action<ProfileEventContainer> onAdd)
        {
            OnAdd = onAdd;
            Container = container;

            AddProfileEventsWindow window = ScriptableObject.CreateInstance<AddProfileEventsWindow>();
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
            DrawerPool = new Dictionary<int, FunctionRequestDrawer<ExecuteFunctionProfileArgs>>();
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
                                case ProfileEventType.ADD_STATISTIC_VALUE:
                                    var eventAdd = eventBody.GetContent<ProfileAddStatisticEvent>();
                                    GUILayout.Label("Statistic name");
                                    eventAdd.StatisticName = GUILayout.TextField(eventAdd.StatisticName, GUILayout.Width(200));
                                    GUILayout.Label("Value to add");
                                    eventAdd.StatisticValue = EditorGUILayout.IntField(eventAdd.StatisticValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventAdd);
                                    break;
                                case ProfileEventType.ADD_ACHIEVEMENT_POINT:
                                    var eventAddAchievement = eventBody.GetContent<ProfileAddAchievementPointEvent>();
                                    GUILayout.Label("Achievement ID");
                                    eventAddAchievement.AchievementID = GUILayout.TextField(eventAddAchievement.AchievementID, GUILayout.Width(200));
                                    GUILayout.Label("Points to add");
                                    eventAddAchievement.Points = EditorGUILayout.IntField(eventAddAchievement.Points, GUILayout.Width(200));
                                    eventBody.SaveContent(eventAddAchievement);
                                    break;
                                case ProfileEventType.UPDATE_STATISTIC_VALUE:
                                    var eventUpdate = eventBody.GetContent<ProfileUpdateStatisticEvent>();
                                    GUILayout.Label("Statistic name");
                                    eventUpdate.StatisticName = GUILayout.TextField(eventUpdate.StatisticName, GUILayout.Width(200));
                                    GUILayout.Label("Value to update");
                                    eventUpdate.StatisticValue = EditorGUILayout.IntField(eventUpdate.StatisticValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventUpdate);
                                    break;
                                case ProfileEventType.CONVERT_STATISTIC_TO_EXP:
                                    var eventConvert = eventBody.GetContent<ProfileConvertStatisticToExpEvent>();
                                    GUILayout.Label("Statistic name");
                                    eventConvert.StatisticName = GUILayout.TextField(eventConvert.StatisticName, GUILayout.Width(200));
                                    eventBody.SaveContent(eventConvert);
                                    break;
                                case ProfileEventType.SET_CUSTOM_DATA:
                                    var eventSetData = eventBody.GetContent<ProfileSetCustomDataEvent>();
                                    GUILayout.Label("Data Key");
                                    eventSetData.DataKey = GUILayout.TextField(eventSetData.DataKey, GUILayout.Width(200));
                                    GUILayout.Label("Data Value");
                                    eventSetData.DataValue = EditorGUILayout.TextField(eventSetData.DataValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventSetData);
                                    break;
                                case ProfileEventType.GRANT_SPECIAL_OFFER:
                                    var eventGrantOffer = eventBody.GetContent<ProfileGrantSpefialOfferEvent>();
                                    GUILayout.Label("Special offer item ID");
                                    eventGrantOffer.SpecialOfferItemID = GUILayout.TextField(eventGrantOffer.SpecialOfferItemID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventGrantOffer);
                                    break;
                                case ProfileEventType.GRANT_CALENDAR_INSTANCE:
                                    var eventCalendar = eventBody.GetContent<ProfileGrantCalendarEvent>();
                                    GUILayout.Label("Calendar ID");
                                    eventCalendar.CalendarID = GUILayout.TextField(eventCalendar.CalendarID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventCalendar);
                                    break;
                                case ProfileEventType.GRANT_AVATAR:
                                    var eventAvatar = eventBody.GetContent<ProfileGrantAvatarEvent>();
                                    GUILayout.Label("Avatar ID");
                                    eventAvatar.AvatarID = GUILayout.TextField(eventAvatar.AvatarID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventAvatar);
                                    break;
                                case ProfileEventType.SEND_NOTIFICATION:
                                    var eventNotification = eventBody.GetContent<ProfileSendNotificationEvent>();
                                    GUILayout.Label("Notification ID");
                                    eventNotification.NotificationID = GUILayout.TextField(eventNotification.NotificationID, GUILayout.Width(200));
                                    eventBody.SaveContent(eventNotification);
                                    break;
                                case ProfileEventType.EXECUTE_FUNCTION:
                                    var eventFunction = eventBody.GetContent<ProfileExecuteFunctionEvent>();
                                    GUILayout.Label("Function Name");
                                    eventFunction.FunctionName = GUILayout.TextField(eventFunction.FunctionName, GUILayout.Width(200));

                                    if (!DrawerPool.ContainsKey(instanceID))
                                    {
                                        DrawerPool[instanceID] = new FunctionRequestDrawer<ExecuteFunctionProfileArgs>();
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
                EventToAdd = (ProfileEventType)EditorGUILayout.EnumPopup(EventToAdd, GUILayout.Width(290));
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

        private void AddEvent(ProfileEventType eventType)
        {
            ProfileEvent profileEvent = null;
            switch (eventType)
            {
                case ProfileEventType.SET_CUSTOM_DATA:
                    profileEvent = new ProfileSetCustomDataEvent();
                    break;
                case ProfileEventType.UPDATE_STATISTIC_VALUE:
                    profileEvent = new ProfileUpdateStatisticEvent();
                    break;
                case ProfileEventType.ADD_STATISTIC_VALUE:
                    profileEvent = new ProfileAddStatisticEvent();
                    break;
                case ProfileEventType.CONVERT_STATISTIC_TO_EXP:
                    profileEvent = new ProfileConvertStatisticToExpEvent();
                    break;
                case ProfileEventType.GRANT_SPECIAL_OFFER:
                    profileEvent = new ProfileGrantSpefialOfferEvent();
                    break;
                case ProfileEventType.EXECUTE_FUNCTION:
                    profileEvent = new ProfileExecuteFunctionEvent();
                    break;
                case ProfileEventType.ADD_ACHIEVEMENT_POINT:
                    profileEvent = new ProfileAddAchievementPointEvent();
                    break;
                case ProfileEventType.GRANT_CALENDAR_INSTANCE:
                    profileEvent = new ProfileGrantCalendarEvent();
                    break;
                case ProfileEventType.SEND_NOTIFICATION:
                    profileEvent = new ProfileSendNotificationEvent();
                    break;
                case ProfileEventType.GRANT_AVATAR:
                    profileEvent = new ProfileGrantAvatarEvent();
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

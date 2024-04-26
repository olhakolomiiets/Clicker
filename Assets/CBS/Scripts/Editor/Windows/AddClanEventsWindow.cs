using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddClanEventsWindow : EditorWindow
    {
        private static Action<ClanEventContainer> OnAdd { get; set; }
        private static ClanEventContainer Container { get; set; }
        private Texture2D BackgroundTex { get; set; }
        private EditorData EditorData { get; set; }
        private ClanEventType EventToAdd { get; set; }
        private Dictionary<int, FunctionRequestDrawer<ExecuteFunctionProfileArgs>> DrawerPool { get; set; }
        private Vector2 ScrollPos { get; set; }

        public static void Show(ClanEventContainer container, Action<ClanEventContainer> onAdd)
        {
            OnAdd = onAdd;
            Container = container;

            AddClanEventsWindow window = ScriptableObject.CreateInstance<AddClanEventsWindow>();
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
                                case ClanEventType.ADD_STATISTIC_VALUE:
                                    var eventAdd = eventBody.GetContent<ClanAddStatisticEvent>();
                                    GUILayout.Label("Statistic name");
                                    eventAdd.StatisticName = GUILayout.TextField(eventAdd.StatisticName, GUILayout.Width(200));
                                    GUILayout.Label("Value to add");
                                    eventAdd.StatisticValue = EditorGUILayout.IntField(eventAdd.StatisticValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventAdd);
                                    break;
                                case ClanEventType.UPDATE_STATISTIC_VALUE:
                                    var eventUpdate = eventBody.GetContent<ClanUpdateStatisticEvent>();
                                    GUILayout.Label("Statistic name");
                                    eventUpdate.StatisticName = GUILayout.TextField(eventUpdate.StatisticName, GUILayout.Width(200));
                                    GUILayout.Label("Value to update");
                                    eventUpdate.StatisticValue = EditorGUILayout.IntField(eventUpdate.StatisticValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventUpdate);
                                    break;
                                case ClanEventType.SET_CUSTOM_DATA:
                                    var eventSetData = eventBody.GetContent<ClanSetCustomDataEvent>();
                                    GUILayout.Label("Data Key");
                                    eventSetData.DataKey = GUILayout.TextField(eventSetData.DataKey, GUILayout.Width(200));
                                    GUILayout.Label("Data Value");
                                    eventSetData.DataValue = EditorGUILayout.TextField(eventSetData.DataValue, GUILayout.Width(200));
                                    eventBody.SaveContent(eventSetData);
                                    break;
                                case ClanEventType.EXECUTE_FUNCTION:
                                    var eventFunction = eventBody.GetContent<ClanExecuteFunctionEvent>();
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
                EventToAdd = (ClanEventType)EditorGUILayout.EnumPopup(EventToAdd, GUILayout.Width(290));
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

        private void AddEvent(ClanEventType eventType)
        {
            ClanEvent clanEvent = null;
            switch (eventType)
            {
                case ClanEventType.SET_CUSTOM_DATA:
                    clanEvent = new ClanSetCustomDataEvent();
                    break;
                case ClanEventType.UPDATE_STATISTIC_VALUE:
                    clanEvent = new ClanUpdateStatisticEvent();
                    break;
                case ClanEventType.ADD_STATISTIC_VALUE:
                    clanEvent = new ClanAddStatisticEvent();
                    break;
                case ClanEventType.EXECUTE_FUNCTION:
                    clanEvent = new ClanExecuteFunctionEvent();
                    break;
                default:
                    Debug.LogError("Unrecognized Option");
                    break;
            }
            clanEvent.EventType = eventType;
            Container.AddEvent(clanEvent);
        }
    }
}

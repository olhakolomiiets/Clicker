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
    public class EventsConfigurator : BaseConfigurator
    {
        protected override string Title => "Events Congiguration";
        protected string[] Titles => new string[] { "Events", "Active", "Logs" };

        protected override bool DrawScrollView => true;

        private Rect CategoriesRect = new Rect(0, 0, 150, 700);
        private Rect ItemsRect = new Rect(200, 100, 855, 700);
        private Vector2 PositionScroll { get; set; }
        private Vector2 TaskScroll { get; set; }

        private EventsData EventsData { get; set; }
        private List<ScheduledTask> Tasks { get; set; }
        private string SelectedCategory { get; set; }
        private int SelectedToolBar { get; set; }
        private int SelectedTitleBar { get; set; }
        private int CategoryIndex { get; set; }
        private ScheduledTask SelectedTask { get; set; }

        private List<CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }
        private Categories CachedLootBoxCategories { get; set; }

        private string TitleEntityToken;
        private EditorData EditorData { get; set; }
        private EventsIcons EventsIcons { get; set; }
        private ObjectCustomDataDrawer<CBSEventsCustomData> CustomDataDrawer { get; set; }

        private Dictionary<string, bool> ExtendStates;
        private List<EventExecutionLog> LogList;
        private string[] CronTitleList;
        private string[] CronValuesList;
        private EventQueueContainer QueueContainer;

        private Texture2D EventDefaultTexture;
        private Texture2D EventActiveTexture;
        private Texture2D EventWaitingTexture;
        private Texture2D InfoBackgroundTexture;
        private Texture2D ConfigBackgroundTexture;
        private Texture2D TasksBackgroundTexture;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            ExtendStates = new Dictionary<string, bool>();
            CronTitleList = CronExpression.AllCronsTitle.ToArray();
            CronValuesList = CronExpression.AllCronsValues.ToArray();
            EditorData = CBSScriptable.Get<EditorData>();
            EventsIcons = CBSScriptable.Get<EventsIcons>();
            CustomDataDrawer = new ObjectCustomDataDrawer<CBSEventsCustomData>(PlayfabUtils.DEFAULT_CUSTOM_DATA_SIZE, 830f);
            EventDefaultTexture = ResourcesUtils.GetEventNormalImage();
            EventActiveTexture = ResourcesUtils.GetEventActiveImage();
            EventWaitingTexture = ResourcesUtils.GetEventWaitingImage();
            InfoBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassInfoTitle);
            ConfigBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassConfigTitle);
            TasksBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassExtraLevelTitle);
            CustomDataDrawer.AutoReset = false;
            GetEventsList();
        }

        protected override void OnDrawInside()
        {
            DrawTitles();
            DrawToggles();
        }

        private void DrawToggles()
        {
            if (EventsData == null)
                return;

            if (SelectedTask != null)
            {
                TaskScroll = GUILayout.BeginScrollView(TaskScroll);
                DrawSelectedTask();
                GUILayout.Space(110);
                GUILayout.EndScrollView();
            }
            else
            {
                using (var areaScope = new GUILayout.AreaScope(ItemsRect))
                {
                    var lastTitleIndex = SelectedTitleBar;
                    SelectedTitleBar = GUILayout.Toolbar(SelectedTitleBar, Titles, GUILayout.MaxWidth(1200));
                    GUILayout.Space(10);
                    PositionScroll = GUILayout.BeginScrollView(PositionScroll);
                    if (SelectedTitleBar == 0)
                        DrawEvents();
                    else if (SelectedTitleBar == 1)
                        DrawActive();
                    else if (SelectedTitleBar == 2)
                        DrawLogs();

                    if (lastTitleIndex != SelectedTitleBar)
                    {
                        if (SelectedTitleBar == 2)
                        {
                            UpdateLogList();
                        }
                        if (SelectedTitleBar == 1)
                        {
                            GetEventsList();
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
        }

        private void DrawActive()
        {
            DrawEvents(true);
        }

        private void DrawLogs()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;
            GUIStyle iconStyle = new GUIStyle("Label");

            var logStyle = new GUIStyle(GUI.skin.label);
            logStyle.richText = true;
            // draw titles
            GUILayout.BeginHorizontal();
            GUILayout.Space(67);
            EditorGUILayout.LabelField("Event", titleStyle, GUILayout.Width(120f));
            EditorGUILayout.LabelField("Message", titleStyle, GUILayout.Width(500f));
            EditorGUILayout.LabelField("Date", titleStyle, GUILayout.Width(150f));
            GUILayout.EndHorizontal();

            if (LogList == null)
                return;

            GUILayout.Space(10);

            var logsCount = LogList.Count;
            for (int i = 0; i < logsCount; i++)
            {
                var log = LogList[i];
                var isSuccess = log.IsSuccess;
                var colorTag = isSuccess ? "green" : "red";

                GUILayout.BeginHorizontal();
                var messageTexture = ResourcesUtils.GetLightningImage();
                GUILayout.Button(messageTexture, iconStyle, GUILayout.Width(30), GUILayout.Height(30));
                GUILayout.Space(30f);
                EditorGUILayout.LabelField(string.Format("<color={0}>" + log.EventName + "</color>", colorTag), logStyle, GUILayout.Width(120f));
                EditorGUILayout.LabelField(string.Format("<color={0}>" + log.Message + "</color>", colorTag), logStyle, GUILayout.Width(500f));
                EditorGUILayout.LabelField(string.Format("<color={0}>" + log.LogDate.GetValueOrDefault().ToLocalTime().ToString() + "</color>", colorTag), logStyle, GUILayout.Width(200f));
                GUILayout.EndHorizontal();

                GUILayout.Space(5);
            }
        }

        private void DrawSelectedTask()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var eventStyle = new GUIStyle(GUI.skin.label);
            eventStyle.richText = true;

            GUIStyle infoStyle = new GUIStyle("HelpBox");
            infoStyle.normal.background = InfoBackgroundTexture;

            GUIStyle configStyle = new GUIStyle("HelpBox");
            configStyle.normal.background = ConfigBackgroundTexture;

            GUIStyle tasksStyle = new GUIStyle("HelpBox");
            tasksStyle.normal.background = TasksBackgroundTexture;

            var eventID = SelectedTask.TaskId;
            var metaData = EventsData.GetMetaData(eventID);
            var categoryList = EventsData.GetCategoriesToSelect();
            var displayName = SelectedTask.Name;

            GUILayout.BeginHorizontal();
            if (EditorUtils.DrawButton("Back", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
            {
                SelectedTask = null;
                return;
            }
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
            {
                SaveTask(SelectedTask);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            EditorGUILayout.LabelField("General Info", titleStyle);
            GUILayout.Space(10);

            using (var horizontalScope = new GUILayout.VerticalScope(infoStyle))
            {
                // draw id
                EditorGUILayout.LabelField("Event ID", titleStyle);
                EditorGUILayout.LabelField(eventID);
                GUILayout.Space(5);

                // draw title
                EditorGUILayout.LabelField("Display name", titleStyle);
                displayName = EditorGUILayout.TextField(displayName, new GUILayoutOption[] { GUILayout.Width(800) });
                GUILayout.Space(5);

                GUILayout.BeginHorizontal();
                // draw small icon
                var itemSprite = EventsIcons.GetSprite(eventID);
                var texture = itemSprite == null ? null : itemSprite.texture;
                GUILayout.Button(texture, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(150) });

                // draw small icon
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Small Icon", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                itemSprite = (Sprite)EditorGUILayout.ObjectField((itemSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.Width(150) });
                EventsIcons.SaveSprite(eventID, itemSprite);
                GUILayout.EndVertical();

                // draw background icon
                var backgroundSprite = EventsIcons.GetBackgroundSprite(eventID);
                var backgroundTexture = backgroundSprite == null ? null : backgroundSprite.texture;
                GUILayout.Button(backgroundTexture, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(150) });

                // draw background icon
                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Background Texture", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                backgroundSprite = (Sprite)EditorGUILayout.ObjectField((backgroundSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.Width(150) });
                EventsIcons.SaveBackgroundSprite(eventID, backgroundSprite);
                GUILayout.EndVertical();

                GUILayout.EndHorizontal();

                // draw description
                var descriptionTitle = new GUIStyle(GUI.skin.textField);
                descriptionTitle.wordWrap = true;
                EditorGUILayout.LabelField("Descriprion", titleStyle);
                SelectedTask.Description = EditorGUILayout.TextArea(SelectedTask.Description, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });
                GUILayout.Space(5);

                // draw category
                EditorGUILayout.LabelField("Category", titleStyle);
                var category = metaData.Category;
                var categoryIndex = string.IsNullOrEmpty(category) ? 0 : categoryList.IndexOf(category);
                if (categoryIndex < 0)
                    categoryIndex = 0;
                categoryIndex = EditorGUILayout.Popup(categoryIndex, categoryList.ToArray());
                if (categoryIndex == 0)
                {
                    metaData.Category = string.Empty;
                }
                else
                {
                    var selected = categoryList[categoryIndex];
                    metaData.Category = selected;
                }
                GUILayout.Space(5);

                // draw custom data
                EditorGUILayout.LabelField("Custom Data", titleStyle);
                CustomDataDrawer.Draw(metaData);
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Configuration", titleStyle);
            GUILayout.Space(10);

            EditorGUI.BeginDisabledGroup(metaData.IsRunning);
            using (var horizontalScope = new GUILayout.VerticalScope(configStyle))
            {
                // draw event type
                EditorGUILayout.LabelField("Event Type", titleStyle);
                metaData.DurationType = (EventDurationType)EditorGUILayout.EnumPopup(metaData.DurationType, new GUILayoutOption[] { GUILayout.Width(250) });
                EditorGUILayout.HelpBox(@"ONE SHOT - Simple events for a single execution. For example - send a message to the chat.
DURABLE - Events that have a start and end date.", MessageType.Info);
                GUILayout.Space(5);

                // draw duration
                if (metaData.DurationType == EventDurationType.DURABLE)
                {
                    var duration = metaData.DurationInSeconds;
                    if (duration == 0)
                    {
                        duration = EventsData.DefaultDurationInSeconds;
                    }
                    EditorGUILayout.LabelField("Event Duration in seconds", titleStyle);
                    duration = EditorGUILayout.IntField(duration, GUILayout.Width(250));
                    duration = Mathf.Clamp(duration, EventsData.MinDurationInSeconds, int.MaxValue);
                    metaData.DurationInSeconds = duration;
                }
                GUILayout.Space(5);

                // draw execute type
                EditorGUILayout.LabelField("Execute Type", titleStyle);
                metaData.ExecuteType = (EventExecuteType)EditorGUILayout.EnumPopup(metaData.ExecuteType, new GUILayoutOption[] { GUILayout.Width(250) });
                EditorGUILayout.HelpBox(@"MANUAL - events are triggered manually by users through the configurator.
BY_CRON_EXPRESSION - events are triggered based on cron expression", MessageType.Info);
                GUILayout.Space(5);

                // draw cron options
                if (metaData.ExecuteType == EventExecuteType.BY_CRON_EXPRESSION)
                {
                    EditorGUILayout.LabelField("Use custom cron expression ?", titleStyle);
                    metaData.CustomCron = EditorGUILayout.Toggle(metaData.CustomCron);
                    GUILayout.Space(5);

                    EditorGUILayout.LabelField("Cron Expression", titleStyle);
                    if (metaData.CustomCron)
                    {
                        SelectedTask.Schedule = EditorGUILayout.TextField(SelectedTask.Schedule, new GUILayoutOption[] { GUILayout.Width(250) });
                    }
                    else
                    {
                        var cronIndex = Array.IndexOf(CronValuesList, SelectedTask.Schedule);
                        if (cronIndex < 0)
                            cronIndex = 0;
                        cronIndex = EditorGUILayout.Popup(cronIndex, CronTitleList);
                        var cronValue = CronValuesList[cronIndex];
                        SelectedTask.Schedule = cronValue;
                    }
                }
            }

            GUILayout.Space(20);
            EditorGUILayout.LabelField("Tasks", titleStyle);
            GUILayout.Space(10);

            using (var horizontalScope = new GUILayout.VerticalScope(tasksStyle))
            {
                GUILayout.BeginHorizontal();
                var tasksTitle = metaData.DurationType == EventDurationType.DURABLE ? "Start Tasks List" : "Tasks List";
                EditorGUILayout.LabelField(tasksTitle, titleStyle);
                if (EditorUtils.DrawButton("Modify Tasks", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                {
                    ShowProgress();
                    var titleConfigurator = Get<TitleDataConfigurator>();
                    titleConfigurator.GetTitleData(container =>
                    {
                        HideProgress();
                        EditorUtils.ShowTasksEventWindow(metaData.StartTasks, container, onAdd =>
                        {
                            metaData.StartTasks = onAdd;
                        });
                        //GUIUtility.ExitGUI();
                    });
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();
                var startTasks = metaData.StartTasks == null ? new List<TaskEvent>() : metaData.StartTasks.Events ?? new List<TaskEvent>();
                foreach (var task in startTasks)
                {
                    EditorGUILayout.LabelField(" - <color=yellow>" + task.EventType.ToString() + "</color>", eventStyle);
                }

                GUILayout.Space(10);

                if (metaData.DurationType == EventDurationType.DURABLE)
                {
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("End Tasks", titleStyle);
                    if (EditorUtils.DrawButton("Modify Tasks", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        ShowProgress();
                        var titleConfigurator = Get<TitleDataConfigurator>();
                        titleConfigurator.GetTitleData(container =>
                        {
                            HideProgress();
                            EditorUtils.ShowTasksEventWindow(metaData.EndTasks, container, onAdd =>
                            {
                                metaData.EndTasks = onAdd;
                            });
                            //GUIUtility.ExitGUI();
                        });
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.EndHorizontal();

                    var endTasks = metaData.EndTasks == null ? new List<TaskEvent>() : metaData.EndTasks.Events ?? new List<TaskEvent>();
                    foreach (var task in endTasks)
                    {
                        EditorGUILayout.LabelField(" - <color=yellow>" + task.EventType.ToString() + "</color>", eventStyle);
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            SelectedTask.Name = displayName;
            EventsData.ApplyMetaData(metaData);
        }

        private void DrawTitles()
        {
            if (SelectedTask != null)
                return;
            if (EventsData == null)
                return;
            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                GUILayout.BeginVertical();

                var categoriesMenu = EventsData.GetCategories();
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
                        EventsData.SetCategories(onModify);
                        SaveEventsMeta(EventsData);
                    }, EventsData.Categories);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
            }
        }

        private void DrawEvents(bool onlyActive = false)
        {
            GUIStyle titleDefaultStyle = new GUIStyle("HelpBox");
            titleDefaultStyle.normal.background = EventDefaultTexture;

            GUIStyle titleActiveStyle = new GUIStyle("HelpBox");
            titleActiveStyle.normal.background = EventActiveTexture;

            GUIStyle titleWaitingStyle = new GUIStyle("HelpBox");
            titleWaitingStyle.normal.background = EventWaitingTexture;

            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;
            titleStyle.richText = true;

            var defaultStyle = new GUIStyle(GUI.skin.label);
            defaultStyle.richText = true;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (EditorUtils.DrawButton("Update", EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                GetEventsList();
            }

            if (EditorUtils.DrawButton("Add Event", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(30) }))
            {
                AddEventWindow.Show(displayName =>
                {
                    AddEvent(displayName);
                });
                GUIUtility.ExitGUI();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            if (Tasks == null)
                return;

            var eventsCount = Tasks.Count;

            // draw events
            for (int i = 0; i < eventsCount; i++)
            {
                var eventObj = Tasks[i];
                var eventID = eventObj.TaskId;
                var eventMeta = EventsData.GetMetaData(eventID);
                var isRunning = eventMeta.IsRunning;
                var isCronActive = eventObj.IsActive;
                var durationType = eventMeta.DurationType;
                var executeType = eventMeta.ExecuteType;
                var extend = ExtendStates.ContainsKey(eventID) ? ExtendStates[eventID] : false;
                var isInQueue = eventMeta.InProccess;

                if (onlyActive)
                {
                    if (!isRunning || isInQueue)
                        continue;
                }

                if (SelectedCategory != EventsData.ALL_CATEGORY)
                {
                    if (eventMeta.Category != SelectedCategory)
                        continue;
                }

                var backgroundTexture = isRunning ? titleActiveStyle : executeType == EventExecuteType.BY_CRON_EXPRESSION ? isCronActive ? titleWaitingStyle : titleDefaultStyle : titleDefaultStyle;
                if (isInQueue)
                    backgroundTexture = titleWaitingStyle;
                using (var horizontalScope = new GUILayout.VerticalScope(backgroundTexture))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(33);
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField("<color=yellow>" + eventObj.Name + "</color>", titleStyle, GUILayout.Width(100), GUILayout.Height(14));
                    var descriptionLabel = executeType == EventExecuteType.MANUAL ? "manual" : "cron expression";
                    descriptionLabel = "<color=orange>" + descriptionLabel + "</color>";
                    EditorGUILayout.LabelField(descriptionLabel, defaultStyle, GUILayout.Width(100), GUILayout.Height(13));
                    GUILayout.EndVertical();
                    // draw info
                    var infoText = string.Empty;
                    if (isInQueue)
                    {
                        infoText = "Proccessing... May take several minutes";
                    }
                    else if (durationType == EventDurationType.DURABLE || executeType == EventExecuteType.BY_CRON_EXPRESSION)
                    {
                        if (isRunning)
                        {
                            var endDate = eventMeta.EndDate;
                            if (endDate == null)
                            {
                                infoText = "Processing...";
                            }
                            else
                            {
                                var localTime = DateTime.UtcNow;
                                var endLocalTime = endDate.GetValueOrDefault();
                                var timeSpan = endLocalTime.Subtract(localTime);
                                var timeString = timeSpan.ToString(DateUtils.EventTimerFormat);
                                var totalDays = (int)timeSpan.TotalDays;
                                var dayString = totalDays > 0 ? totalDays.ToString() + " days " : string.Empty;
                                if (timeSpan.Ticks <= 0)
                                {
                                    infoText = "Processing...";
                                }
                                else
                                {
                                    infoText = string.Format("Event end in {0}{1}.<color=white> End date: {2} UTC</color>", dayString, timeString, endDate.ToString());
                                }
                            }
                        }
                        else if (executeType == EventExecuteType.BY_CRON_EXPRESSION && isCronActive)
                        {
                            var endDate = eventObj.NextRunTime;
                            if (endDate == null)
                            {
                                infoText = "Processing...";
                            }
                            else
                            {
                                var localTime = DateTime.UtcNow;
                                var endLocalTime = endDate.GetValueOrDefault();
                                var timeSpan = endLocalTime.Subtract(localTime);
                                var timeString = timeSpan.ToString(DateUtils.EventTimerFormat);
                                var totalDays = (int)timeSpan.TotalDays;
                                var dayString = totalDays > 0 ? totalDays.ToString() + " days " : string.Empty;
                                if (timeSpan.Ticks <= 0)
                                {
                                    infoText = "Processing...";
                                }
                                else
                                {
                                    infoText = string.Format("Next execution in {0}{1}.<color=white> Start date: {2} UTC</color>", dayString, timeString, endDate.ToString());
                                }
                            }
                        }
                    }
                    infoText = "<color=yellow>" + infoText + "</color>";
                    EditorGUILayout.LabelField(infoText, titleStyle, GUILayout.Width(420));
                    GUILayout.FlexibleSpace();

                    if (!isInQueue)
                    {
                        if (executeType == EventExecuteType.MANUAL)
                        {
                            if (durationType == EventDurationType.DURABLE)
                            {
                                if (isRunning)
                                {
                                    if (EditorUtils.DrawButton("Stop", EditorData.StopColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                                    {
                                        StopEventHandler(eventID);
                                    }
                                }
                                else
                                {
                                    if (EditorUtils.DrawButton("Start", EditorData.StartColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                                    {
                                        StartEventHandler(eventID);
                                    }
                                }
                            }
                            else
                            {
                                if (EditorUtils.DrawButton("Execute", EditorData.ExecuteColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                                {
                                    ExecuteEventHandler(eventID);
                                }
                            }
                        }
                        else
                        {
                            if (!isCronActive)
                            {
                                if (EditorUtils.DrawButton("Activate", EditorData.StartColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                                {
                                    StartEventHandler(eventID);
                                }
                            }
                            else
                            {
                                if (EditorUtils.DrawButton("Deactivate", EditorData.StopColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                                {
                                    StopEventHandler(eventID);
                                }
                            }
                        }
                        if (!isRunning && !isCronActive)
                        {
                            if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                            {
                                int option = EditorUtility.DisplayDialogComplex("Warning",
                                    "Are you sure you want to remove this event?",
                                    "Yes",
                                    "No",
                                    string.Empty);
                                switch (option)
                                {
                                    // ok.
                                    case 0:
                                        RemoveEvent(eventID);
                                        break;
                                }
                            }
                        }

                        if (EditorUtils.DrawButton("Configure", EditorData.ConfigureColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                        {
                            SelectedTask = eventObj;
                            CustomDataDrawer.Reset();
                            break;
                        }
                    }
                    else
                    {
                        if (EditorUtils.DrawButton("Revoke", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(30) }))
                        {
                            int option = EditorUtility.DisplayDialogComplex("Warning",
                                "Are you sure you want to revoke this event?",
                                "Yes",
                                "No",
                                string.Empty);
                            switch (option)
                            {
                                // ok.
                                case 0:
                                    RevokeEventHandler(eventID);
                                    break;
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(5);
            }
        }

        private void GetEventsList()
        {
            ShowProgress();
            var keys = new List<string>();
            keys.Add(TitleKeys.EventsDataKey);

            var request = new GetTitleDataRequest
            {
                Keys = keys
            };
            PlayFabAdminAPI.GetTitleInternalData(request, OnInternalDataGetted, OnGetDataFailed);
        }

        private void OnInternalDataGetted(GetTitleDataResult result)
        {
            var dictionary = result.Data;
            bool keyExist = dictionary.ContainsKey(TitleKeys.EventsDataKey);
            var rawData = keyExist ? dictionary[TitleKeys.EventsDataKey] : JsonPlugin.EMPTY_JSON;
            EventsData = new EventsData();
            try
            {
                EventsData = JsonPlugin.FromJsonDecompress<EventsData>(rawData);
            }
            catch
            {
                EventsData = JsonPlugin.FromJson<EventsData>(rawData);
            }
            var taskRequest = new GetTasksRequest();
            PlayFabAdminAPI.GetTasks(taskRequest, onGet =>
            {
                Tasks = onGet.Tasks;
                /*GetEventQueueContainer(container => 
                {
                    Debug.Log(JsonPlugin.ToJson(container));
                    QueueContainer = container ?? new EventQueueContainer();
                    HideProgress();
                });*/
                HideProgress();
            }, onFailed =>
            {
                AddErrorLog(onFailed);
                HideProgress();
            });
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveTask(ScheduledTask task)
        {
            ShowProgress();
            var request = new UpdateTaskRequest
            {
                Identifier = new NameIdentifier
                {
                    Id = task.TaskId
                },
                Name = task.Name,
                Description = task.Description,
                Schedule = task.Schedule,
                Type = ScheduledTaskType.CloudScriptAzureFunctions,
                Parameter = new CloudScriptTaskParameter
                {
                    FunctionName = AzureFunctions.ExecuteCBSEventHandlerMethod,
                    Argument = new FunctionEventProccesRequest
                    {
                        EventID = task.TaskId
                    }
                }
            };
            PlayFabAdminAPI.UpdateTask(request, onUpdate =>
            {
                HideProgress();
                SaveEventsMeta(EventsData);
            }, OnSaveDataFailed);
        }

        private void SaveEventsMeta(EventsData eventsData)
        {
            ShowProgress();

            string rawData = JsonPlugin.ToJsonCompress(eventsData);
            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.EventsDataKey,
                Value = rawData
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveEvents, OnSaveDataFailed);
        }

        private void OnSaveEvents(SetTitleDataResult result)
        {
            HideProgress();
            GetEventsList();
        }

        private void OnSaveDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void GetEventQueueContainer(Action<EventQueueContainer> result)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.GetEventQueueContainerMethod,
                    FunctionParameter = new FunctionBaseRequest()
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
                        var functionResult = OnGet.GetResult<FunctionGetEventQueueResult>();
                        QueueContainer = functionResult.QueueContainer ?? new EventQueueContainer();
                        HideProgress();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void RemoveEvent(string eventID)
        {
            var deleteRequest = new DeleteTaskRequest
            {
                Identifier = new NameIdentifier
                {
                    Id = eventID
                }
            };
            PlayFabAdminAPI.DeleteTask(deleteRequest, onDelete =>
            {
                EventsData.RemoveEvent(eventID);
                EventsIcons.RemoveSprite(eventID);
                EventsIcons.RemoveBackgroundSprite(eventID);
                SaveEventsMeta(EventsData);
            }, onFailed =>
            {
                AddErrorLog(onFailed);
                HideProgress();
            });

        }

        private void AddEvent(string displayName)
        {
            ShowProgress();
            AdminAPIExtension.CreateAzureCloudScriptTask(new CreateCloudScriptTaskRequest
            {
                Name = displayName,
                IsActive = false,
                Parameter = new CloudScriptTaskParameter
                {
                    FunctionName = AzureFunctions.ExecuteCBSEventHandlerMethod
                }
            }, onCreate =>
            {
                var taskID = onCreate.TaskId;
                PlayFabAdminAPI.GetTasks(new GetTasksRequest
                {
                    Identifier = new NameIdentifier
                    {
                        Id = taskID
                    }
                }, onGetTask =>
                {
                    var task = onGetTask.Tasks.FirstOrDefault();
                    task.Name = displayName;
                    var meta = EventsData.GetMetaData(taskID);
                    meta.Category = SelectedCategory;
                    EventsData.ApplyMetaData(meta);
                    SaveTask(task);
                }, onFailed =>
                {
                    HideProgress();
                    AddErrorLog(onFailed);
                });
            }, onFailed =>
            {
                HideProgress();
                AddErrorLog(onFailed);
            });
        }

        private void StartEventHandler(string eventID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.StartCBSEventHandlerMethod,
                    FunctionParameter = new FunctionEventProccesRequest
                    {
                        EventID = eventID,
                        Manual = true,
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
                        HideProgress();
                        EditorUtility.DisplayDialog("Success!", "Event will start soon. Click 'Update' to see changes!", "OK");
                        GetEventsList();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void StopEventHandler(string eventID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.StopCBSEventHandlerMethod,
                    FunctionParameter = new FunctionEventProccesRequest
                    {
                        EventID = eventID,
                        Manual = true
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

                        HideProgress();
                        EditorUtility.DisplayDialog("Success!", "Event wil stop soon. Click 'Update' to see changes!", "OK");
                        GetEventsList();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void ExecuteEventHandler(string eventID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.ExecuteCBSEventHandlerMethod,
                    FunctionParameter = new FunctionEventProccesRequest
                    {
                        EventID = eventID
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

                        HideProgress();
                        EditorUtility.DisplayDialog("Success!", "Event executed!", "OK");
                        GetEventsList();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void RevokeEventHandler(string eventID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.RevokeCBSEventHandlerMethod,
                    FunctionParameter = new FunctionEventProccesRequest
                    {
                        EventID = eventID
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, OnRevoke =>
                {
                    var cbsError = OnRevoke.GetCBSError();
                    if (cbsError != null)
                    {
                        AddErrorLog(cbsError);
                        EditorUtility.DisplayDialog("Failed!", cbsError.Message, "OK");
                        HideProgress();
                    }
                    else
                    {
                        HideProgress();
                        EditorUtility.DisplayDialog("Success!", "Event revoked!", "OK");
                        GetEventsList();
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
                    FunctionName = AzureFunctions.GetCBSEventsLogListMethod,
                    FunctionParameter = new FunctionBaseRequest()
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
                        var functionGetNotificationsResult = OnGet.GetResult<FunctionGetEventLogResult>();
                        var logs = functionGetNotificationsResult.Logs;
                        LogList = logs;
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

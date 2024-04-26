#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using PlayFab;
using PlayFab.AdminModels;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ProfileTasksConfigurator : BaseTasksConfigurator<CBSProfileTask, ProfilePeriodTasksData, AddProfileTaskWindow>
    {
        protected override string Title => "Daily Tasks";

        protected override string TASK_TITLE_ID { get; set; }

        protected override string[] Titles => new string[] { "Tasks", "Additional configs" };

        protected override string ItemKey => "task";

        private int DailyTasksCount { get; set; }

        private ProfileTasksData TasksList { get; set; } = new ProfileTasksData();

        private Rect CategoriesRect = new Rect(0, 0, 130, 700);
        private Rect ItemsRect = new Rect(150, 100, 936, 700);

        private int TaskListIndex { get; set; }
        private string SelectedTaskID { get; set; }
        private Vector2 PositionScroll { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            GetTasksPool();
        }

        protected override void DrawConfigs()
        {
            base.DrawConfigs();

            GUILayout.Space(15);
            DailyTasksCount = TasksData.DailyTasksCount;
            DailyTasksCount = EditorGUILayout.IntField("Tasks count per period", DailyTasksCount);
            EditorGUILayout.HelpBox("The number of tasks available for profile every time period. Cannot be less than 1", MessageType.Info);
            if (DailyTasksCount < 1)
                DailyTasksCount = 1;
            TasksData.DailyTasksCount = DailyTasksCount;

            GUILayout.Space(15);
            TasksData.UpdatePeriod = (DatePeriod)EditorGUILayout.EnumPopup("Update period", TasksData.UpdatePeriod, new GUILayoutOption[] { GUILayout.Width(250) });
        }

        protected override void OnDrawInside()
        {
            DrawTitles();

            if (TasksData == null || string.IsNullOrEmpty(SelectedTaskID))
                return;

            using (var areaScope = new GUILayout.AreaScope(ItemsRect))
            {
                PositionScroll = GUILayout.BeginScrollView(PositionScroll);
                base.OnDrawInside();
                GUILayout.EndScrollView();
            }
        }

        protected override void DrawInteractionButtons()
        {
            if (EditorUtils.DrawButton("Remove tasks", EditorData.RemoveColor, 12, AddButtonOptions))
            {
                string questionsText = string.Format("Are you sure you want to remove this tasks pool?");
                int option = EditorUtility.DisplayDialogComplex("Warning",
                    questionsText,
                    "Yes",
                    "No",
                    string.Empty);
                switch (option)
                {
                    // ok.
                    case 0:
                        TasksList.RemoveID(SelectedTaskID);
                        if (TasksList.IsEmpty())
                        {
                            TasksData = null;
                        }
                        SaveTasksPool(TasksList);
                        GUIUtility.ExitGUI();
                        break;
                }
            }
            base.DrawInteractionButtons();
        }

        protected override void ShowTaskWindow(CBSProfileTask task, string titleID, ItemAction action)
        {
            base.ShowTaskWindow(task, SelectedTaskID, action);
        }

        private void DrawTitles()
        {
            if (TasksList == null)
                return;

            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                GUILayout.BeginVertical();

                int categoryHeight = 30;
                int categoriesCount = TasksList.TasksList == null ? 0 : TasksList.TasksList.Count;

                if (TasksList.TasksList != null && TasksList.TasksList.Count > 0)
                {
                    var lastSelectedID = SelectedTaskID;
                    var categoriesMenu = TasksList.TasksList.ToArray();
                    TaskListIndex = GUI.SelectionGrid(new Rect(0, 100, 130, categoryHeight * categoriesCount), TaskListIndex, categoriesMenu.ToArray(), 1);
                    TaskListIndex = Mathf.Clamp(TaskListIndex, 0, categoriesMenu.Length - 1);
                    string selctedCategory = categoriesMenu[TaskListIndex];

                    SelectedTaskID = TasksList.TasksList.ElementAt(TaskListIndex);
                    if (lastSelectedID != SelectedTaskID && !string.IsNullOrEmpty(SelectedTaskID))
                    {
                        var tasksTitleID = ProfileTasksData.ProfileTasksTitlePrefix + SelectedTaskID;
                        TASK_TITLE_ID = tasksTitleID;
                        GetTaskTable(tasksTitleID);
                    }
                }

                GUILayout.Space(30);
                GUILayout.Space(30);
                var oldColor = GUI.color;
                GUI.backgroundColor = EditorData.AddColor;
                var style = new GUIStyle(GUI.skin.button);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;
                if (GUI.Button(new Rect(0, 130 + categoryHeight * categoriesCount, 130, categoryHeight), "Add new task pool", style))
                {
                    AddTaskPoolWindow.Show(onAdd =>
                    {
                        var newInstance = onAdd;
                        TasksList.AddNewID(newInstance);
                        SaveTasksPool(TasksList);
                    });
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
            }
        }

        private void GetTasksPool()
        {
            ShowProgress();
            var keys = new List<string>();
            keys.Add(TitleKeys.ProfileTasksTitleKey);

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
            bool keyExist = dictionary.ContainsKey(TitleKeys.ProfileTasksTitleKey);
            var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
            TasksList = keyExist ? jsonPlugin.DeserializeObject<ProfileTasksData>(dictionary[TitleKeys.ProfileTasksTitleKey]) : new ProfileTasksData();
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveTasksPool(ProfileTasksData tasksData)
        {
            ShowProgress();

            var jsonPlugin = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);

            string rawData = jsonPlugin.SerializeObject(tasksData);

            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.ProfileTasksTitleKey,
                Value = rawData
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveBattlePass, OnGetDataFailed);
        }

        private void OnSaveBattlePass(SetTitleDataResult result)
        {
            HideProgress();
            GetTasksPool();
        }
    }
}
#endif

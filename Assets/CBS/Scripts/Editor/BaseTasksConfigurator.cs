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
    public class BaseTasksConfigurator<TTask, TTaskData, TWindow> : BaseConfigurator where TTask : CBSTask, new() where TTaskData : CBSTasksData<TTask>, new() where TWindow : AddTaskWindow<TTask>
    {
        protected virtual string TASK_TITLE_ID { get; set; }

        protected override string Title => "Tasks";

        protected override bool DrawScrollView => true;

        protected virtual string[] Titles => new string[] { "Tasks", "Additional configs" };

        protected virtual string ItemKey => "task";

        protected EditorData EditorData { get; set; }

        private List<CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private Categories CachedPacksCategories { get; set; }
        private Categories CachedLootBoxCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }

        protected TTaskData TasksData { get; set; }

        private int SelectedToolBar { get; set; }

        protected GUILayoutOption[] AddButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(100) };
            }
        }

        private TasksIcons Icons { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            Icons = CBSScriptable.Get<TasksIcons>();
            if (!string.IsNullOrEmpty(TASK_TITLE_ID))
            {
                GetTaskTable(TASK_TITLE_ID);
            }
        }

        protected override void OnDrawInside()
        {
            if (Titles.Length == 0)
            {
                DrawTasks();
            }
            else
            {
                SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, Titles, GUILayout.MaxWidth(1200));

                switch (SelectedToolBar)
                {
                    case 0:
                        DrawTasks();
                        break;
                    case 1:
                        DrawConfigs();
                        break;
                    default:
                        break;
                }
            }
        }

        protected virtual void DrawInteractionButtons()
        {
            ShowTaskWindow(new TTask(), TASK_TITLE_ID, ItemAction.ADD);
        }

        protected virtual void DrawTasks()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(1200));

            GUILayout.Space(20);

            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;

            var tierStyle = new GUIStyle(GUI.skin.label);
            tierStyle.fontStyle = FontStyle.Bold;
            tierStyle.fontSize = 12;

            var middleStyle = new GUIStyle(GUI.skin.label);
            middleStyle.fontStyle = FontStyle.Bold;
            middleStyle.fontSize = 14;
            middleStyle.alignment = TextAnchor.MiddleCenter;

            // draw add queue 
            GUILayout.BeginHorizontal();

            // draw name
            GUILayout.Space(27);
            EditorGUILayout.LabelField("Name", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });

            // draw count
            GUILayout.Space(155);
            EditorGUILayout.LabelField("Steps", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });

            // draw mode
            GUILayout.Space(40);
            EditorGUILayout.LabelField("Level", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(100) });

            GUILayout.FlexibleSpace();
            DrawInteractionButtons();
            GUILayout.EndHorizontal();

            EditorUtils.DrawUILine(Color.black, 2, 20);

            GUILayout.Space(20);

            if (TasksData == null)
            {
                GUILayout.EndVertical();
                return;
            }           

            var taskList = TasksData.GetTasks();

            for (int i = 0; i < taskList.Count; i++)
            {
                var task = taskList[i];
                GUILayout.BeginHorizontal();
                string positionString = (i + 1).ToString();
                var positionDetail = task;

                EditorGUILayout.LabelField(positionString, titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(20), GUILayout.Height(50) });

                var actvieSprite = task.GetSprite();
                var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                GUILayout.Button(iconTexture, GUILayout.Width(50), GUILayout.Height(50));

                // draw title
                EditorGUILayout.LabelField(task.Title, titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150), GUILayout.Height(50) });

                // draw steps
                var stepsLabel = task.Type == TaskType.ONE_SHOT ? "One shot" : task.Steps.ToString();
                if (task.Type == TaskType.TIERED)
                    stepsLabel = string.Empty;
                GUILayout.Space(90);
                EditorGUILayout.LabelField(stepsLabel.ToString(), middleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(70), GUILayout.Height(50) });

                // draw level
                var levelLabel = task.IsLockedByLevel ? task.LockLevel.ToString() : "--";
                GUILayout.Space(148);
                EditorGUILayout.LabelField(levelLabel.ToString(), titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(50), GUILayout.Height(50) });

                GUILayout.FlexibleSpace();

                GUILayout.Space(50);

                if (task.Type != TaskType.TIERED)
                {
                    DrawTaskRewardsButtons(task);
                }

                // draw edit button
                ShowTaskWindow(task, TASK_TITLE_ID, ItemAction.EDIT);

                // draw remove button
                if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(60), GUILayout.Height(50) }))
                {
                    string questionsText = string.Format("Are you sure you want to remove this {0}?", ItemKey);
                    int option = EditorUtility.DisplayDialogComplex("Warning",
                        questionsText,
                        "Yes",
                        "No",
                        string.Empty);
                    switch (option)
                    {
                        // ok.
                        case 0:
                            RemoveTask(task);
                            break;
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginVertical();
                // draw tieres
                if (task.Type == TaskType.TIERED)
                {
                    var tieres = task.TierList;
                    if (tieres != null)
                    {
                        for (int j = 0; j < tieres.Count; j++)
                        {
                            GUILayout.Space(3);
                            GUILayout.BeginHorizontal();

                            var tier = tieres[j];

                            GUILayout.Space(25);
                            GUILayout.Label("Tier " + j.ToString(), tierStyle, GUILayout.Width(50));

                            GUILayout.Space(270);
                            GUILayout.Label(tier.StepsToComplete.ToString(), tierStyle, GUILayout.Width(100));

                            GUILayout.FlexibleSpace();
                            DrawTierTaskRewardsButtons(tier);
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                GUILayout.EndVertical();

                EditorUtils.DrawUILine(Color.grey, 1, 20);

                GUILayout.Space(10);
            }

            GUILayout.EndVertical();
        }

        protected virtual void ShowTaskWindow(TTask task, string titleID, ItemAction action)
        {
            var buttonTitle = action == ItemAction.ADD ? "Add new " + this.ItemKey : "Edit";
            var buttonColor = action == ItemAction.ADD ? EditorData.AddColor : EditorData.EditColor;
            var buttonOption = action == ItemAction.ADD ? AddButtonOptions : new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(50) };
            if (EditorUtils.DrawButton(buttonTitle, buttonColor, 12, buttonOption))
            {
                AddTaskWindow<TTask>.Show<TWindow>(task, titleID, action, onModify =>
                {
                    if (action == ItemAction.ADD)
                    {
                        AddNewTask(onModify);
                    }
                    else
                    {
                        SaveTasksTable(TasksData);
                    }
                });
                GUIUtility.ExitGUI();
            }
        }

        protected virtual void DrawConfigs()
        {
            GUILayout.BeginVertical(GUILayout.MaxWidth(1200));
            GUILayout.Space(5);
            EditorUtils.DrawUILine(Color.black, 2, 20);
            GUILayout.Space(20);

            GUILayout.BeginHorizontal();

            TasksData.AutomaticReward = EditorGUILayout.Toggle("Automatic reward", TasksData.AutomaticReward);

            GUILayout.FlexibleSpace();

            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(30) }))
            {
                SaveTasksTable(TasksData);
            }

            GUILayout.EndHorizontal();


            GUILayout.Space(5);
            var autoRewardHelpText = string.Format("Enable this option to automatically reward the player after completing {0}. If this option is disabled, you will need to call additional API methods to receive reward", ItemKey);
            EditorGUILayout.HelpBox(autoRewardHelpText, MessageType.Info);

            if (TasksData.AutomaticReward)
            {
                TasksData.RewardDelivery = EditorUtils.DrawRewardDelivery(TasksData.RewardDelivery);
            }

            GUILayout.EndVertical();
        }

        protected virtual void DrawTaskRewardsButtons(TTask task)
        {
            // draw events
            if (EditorUtils.DrawButton("Events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(50) }))
            {
                EditorUtils.ShowProfileEventWindow(task.Events, onAdd =>
                {
                    task.Events = onAdd;
                    SaveTasksTable(TasksData);
                });
            }

            // draw reward button
            if (EditorUtils.DrawButton("Reward", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(50) }))
            {
                var prize = task.Reward ?? new RewardObject();
                ShowRewardDialog(prize, true, result =>
                {
                    task.Reward = prize;
                    SaveTasksTable(TasksData);
                });
            }
        }

        protected virtual void DrawTierTaskRewardsButtons(TaskTier tier)
        {
            // draw events
            if (EditorUtils.DrawButton("Events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(60), GUILayout.Height(20) }))
            {
                EditorUtils.ShowProfileEventWindow(tier.Events, onAdd =>
                {
                    tier.Events = onAdd;
                    SaveTasksTable(TasksData);
                });
            }

            // draw prize button
            if (EditorUtils.DrawButton("Reward", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(20) }))
            {
                var prize = tier.Reward ?? new RewardObject();
                ShowRewardDialog(prize, true, result =>
                {
                    tier.Reward = prize;
                    SaveTasksTable(TasksData);
                });
            }
        }

        // get level table
        protected void GetTaskTable(string tasksTitleID)
        {
            var keyList = new List<string>();
            keyList.Add(tasksTitleID);
            var dataRequest = new GetTitleDataRequest
            {
                Keys = keyList
            };

            PlayFabAdminAPI.GetTitleInternalData(dataRequest, OnTasksTableGetted, OnTaskTableError);
        }

        private TTaskData FromJson(string rawData)
        {
            try
            {
                return JsonPlugin.FromJsonDecompress<TTaskData>(rawData);
            }
            catch
            {
                return JsonPlugin.FromJson<TTaskData>(rawData);
            }
        }

        private void OnTasksTableGetted(GetTitleDataResult result)
        {
            bool tableExist = result.Data.ContainsKey(TASK_TITLE_ID);
            if (tableExist)
            {
                string tableRaw = result.Data[TASK_TITLE_ID];

                var table = FromJson(tableRaw);
                if (table.GetTasks() == null)
                {
                    table.NewInstance();
                }
                HideProgress();
                TasksData = table;
            }
            else
            {
                TasksData = new TTaskData();
                TasksData.NewInstance();
            }
            HideProgress();
        }

        private void OnTaskTableError(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // add empty achievements
        private void AddNewTask(TTask task)
        {
            TasksData.Add(task);
            SaveTasksTable(TasksData);
        }

        protected void SaveTasksTable(TTaskData data)
        {
            ShowProgress();
            string listRaw = JsonPlugin.ToJsonCompress(data);

            var dataRequest = new SetTitleDataRequest
            {
                Key = TASK_TITLE_ID,
                Value = listRaw
            };

            PlayFabAdminAPI.SetTitleInternalData(dataRequest, OnTasksTableSaved, OnSaveTasksFailed);
        }

        private void RemoveTask(TTask achievement)
        {
            TasksData.Remove(achievement);
            ShowProgress();

            SaveTasksTable(TasksData);
        }

        private void OnTasksTableSaved(SetTitleDataResult result)
        {
            HideProgress();
            GetTaskTable(TASK_TITLE_ID);
        }

        private void OnSaveTasksFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        public void ShowRewardDialog(RewardObject prize, bool includeCurrencies, Action<RewardObject> modifyCallback)
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
    }
}
#endif

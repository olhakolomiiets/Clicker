#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ClanTaskConfigurator : BaseTasksConfigurator<CBSClanTask, ClanTaskData, AddClanTaskWindow>
    {
        protected override string Title => "Clan Task";

        protected override string TASK_TITLE_ID => TitleKeys.ClanTaskTitleKey;

        protected override string[] Titles => new string[] { };

        protected override string ItemKey => "task";

        private int DailyTasksCount { get; set; }

        public void ReDraw()
        {
            OnDrawInside();
        }

        protected override void DrawTasks()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            GUILayout.Space(10);
            // draw reward devilery
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(400));
            TasksData.RewardDelivery = EditorUtils.DrawRewardDelivery(TasksData.RewardDelivery);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(30) }))
            {
                SaveTasksTable(TasksData);
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            DailyTasksCount = TasksData.DailyTasksCount;
            DailyTasksCount = EditorGUILayout.IntField("Tasks count per period", DailyTasksCount);
            EditorGUILayout.HelpBox("The number of tasks available for clan every time period. Cannot be less than 1", MessageType.Info);
            if (DailyTasksCount < 1)
                DailyTasksCount = 1;
            TasksData.DailyTasksCount = DailyTasksCount;

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Update Period", titleStyle, new GUILayoutOption[] { GUILayout.Width(300) });
            TasksData.UpdatePeriod = (DatePeriod)EditorGUILayout.EnumPopup(TasksData.UpdatePeriod, new GUILayoutOption[] { GUILayout.Width(300) });

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Reward Behavior", titleStyle, new GUILayoutOption[] { GUILayout.Width(300) });
            TasksData.RewardBehavior = (TaskRewardBehavior)EditorGUILayout.EnumPopup(TasksData.RewardBehavior, new GUILayoutOption[] { GUILayout.Width(300) });

            base.DrawTasks();
        }

        protected override void DrawTaskRewardsButtons(CBSClanTask task)
        {
            GUILayout.BeginVertical();
            // draw events
            if (EditorUtils.DrawButton("Clan Events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(25) }))
            {
                EditorUtils.ShowClanEventWindow(task.ClanEvents, onAdd =>
                {
                    task.ClanEvents = onAdd;
                    SaveTasksTable(TasksData);
                });
            }

            // draw reward button
            if (EditorUtils.DrawButton("Clan Reward", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(25) }))
            {
                var prize = task.Reward ?? new RewardObject();
                ShowRewardDialog(prize, true, result =>
                {
                    task.Reward = prize;
                    SaveTasksTable(TasksData);
                });
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            // draw events
            if (EditorUtils.DrawButton("Profile Events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(25) }))
            {
                EditorUtils.ShowProfileEventWindow(task.Events, onAdd =>
                {
                    task.Events = onAdd;
                    SaveTasksTable(TasksData);
                });
            }

            // draw reward button
            if (EditorUtils.DrawButton("Profile Reward", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(100), GUILayout.Height(25) }))
            {
                var prize = task.ProfileReward ?? new RewardObject();
                ShowRewardDialog(prize, true, result =>
                {
                    task.ProfileReward = prize;
                    SaveTasksTable(TasksData);
                });
            }
            GUILayout.EndVertical();
        }

        protected override void DrawTierTaskRewardsButtons(TaskTier tier)
        {
            // draw events
            if (EditorUtils.DrawButton("Clan Events", EditorData.EventColor, 10, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(20) }))
            {
                EditorUtils.ShowClanEventWindow(tier.ClanEvents, onAdd =>
                {
                    tier.ClanEvents = onAdd;
                    SaveTasksTable(TasksData);
                });
            }

            // draw prize button
            if (EditorUtils.DrawButton("Clan Reward", EditorData.AddPrizeColor, 10, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(20) }))
            {
                var prize = tier.Reward ?? new RewardObject();
                ShowRewardDialog(prize, true, result =>
                {
                    tier.Reward = prize;
                    SaveTasksTable(TasksData);
                });
            }

            // draw events
            if (EditorUtils.DrawButton("Profile Events", EditorData.EventColor, 10, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(20) }))
            {
                EditorUtils.ShowProfileEventWindow(tier.Events, onAdd =>
                {
                    tier.Events = onAdd;
                    SaveTasksTable(TasksData);
                });
            }

            // draw prize button
            if (EditorUtils.DrawButton("Profile Reward", EditorData.AddPrizeColor, 10, new GUILayoutOption[] { GUILayout.MaxWidth(90), GUILayout.Height(20) }))
            {
                var prize = tier.AdditionalReward ?? new RewardObject();
                ShowRewardDialog(prize, true, result =>
                {
                    tier.AdditionalReward = prize;
                    SaveTasksTable(TasksData);
                });
            }
        }
    }
}
#endif

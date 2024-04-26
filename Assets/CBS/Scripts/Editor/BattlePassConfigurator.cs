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
using System.Text;
using UnityEditor;
using UnityEngine;
using Action = System.Action;

namespace CBS.Editor
{
    public class BattlePassConfigurator : BaseConfigurator
    {
        protected override string Title => "BattlePass Congiguration";
        protected string[] Titles => new string[] { "General", "Tickets" };

        protected override bool DrawScrollView => true;

        private Rect CategoriesRect = new Rect(0, 0, 150, 700);
        private Rect ItemsRect = new Rect(200, 100, 855, 700);
        private Vector2 PositionScroll { get; set; }
        private LevelTreeType CurrentRewardView;

        private BattlePassData BattlePassData { get; set; } = new BattlePassData();
        private List<CatalogItem> CatalogItems { get; set; }
        private BattlePassInstance SelectedInstance { get; set; }
        private int BattlePassIndex { get; set; }
        private int SelectedToolBar { get; set; }
        private int SelectedTitleBar { get; set; }

        private List<CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }
        private Categories CachedLootBoxCategories { get; set; }

        private EditorData EditorData { get; set; }
        private ObjectCustomDataDrawer<CBSBattlePassCustomData> CustomDataDrawer { get; set; }

        private GUILayoutOption[] AddButtonOptions
        {
            get { return new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(120) }; }
        }

        private Texture2D InfoBackgroundTexture;
        private Texture2D ConfigBackgroundTexture;
        private Texture2D LevelBackgroundTexture;
        private Texture2D ExtraLevelBackgroundTexture;
        private Texture2D TicketTitleTexture;
        private Texture2D TicketContentTexture;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            CustomDataDrawer =
                new ObjectCustomDataDrawer<CBSBattlePassCustomData>(PlayfabUtils.DEFAULT_CUSTOM_DATA_SIZE, 830f);
            InfoBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassInfoTitle);
            ConfigBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassConfigTitle);
            LevelBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassLevelTitle);
            ExtraLevelBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.BattlePassExtraLevelTitle);
            TicketTitleTexture = EditorUtils.MakeColorTexture(EditorData.TicketTitle);
            TicketContentTexture = EditorUtils.MakeColorTexture(EditorData.TicketContent);
            GetBattlePassData();
        }

        protected override void OnDrawInside()
        {
            DrawTitles();
            DrawBattlePassInstanes();
        }

        private void DrawToolbar()
        {
            SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, new string[] { "Default rewards", "Premium rewards" });
            switch (SelectedToolBar)
            {
                case 0:
                    CurrentRewardView = LevelTreeType.Default;
                    break;
                case 1:
                    CurrentRewardView = LevelTreeType.Premium;
                    break;
                default:
                    break;
            }
        }

        private void DrawTitles()
        {
            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                GUILayout.BeginVertical();

                int categoryHeight = 30;
                int categoriesCount = BattlePassData.Instances == null ? 0 : BattlePassData.Instances.Count;

                if (BattlePassData.Instances != null && BattlePassData.Instances.Count > 0)
                {
                    var categoriesMenu = BattlePassData.Instances.Select(x => x.DisplayName).ToArray();
                    BattlePassIndex = GUI.SelectionGrid(new Rect(0, 100, 150, categoryHeight * categoriesCount),
                        BattlePassIndex, categoriesMenu.ToArray(), 1);
                    string selctedCategory = categoriesMenu[BattlePassIndex];

                    SelectedInstance = BattlePassData.Instances.ElementAt(BattlePassIndex);
                }

                GUILayout.Space(30);
                GUILayout.Space(30);
                var oldColor = GUI.color;
                GUI.backgroundColor = EditorData.AddColor;
                var style = new GUIStyle(GUI.skin.button);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;
                if (GUI.Button(new Rect(0, 130 + categoryHeight * categoriesCount, 150, categoryHeight),
                        "Add new Instance", style))
                {
                    AddBattlePassInstanceWindow.Show(onAdd =>
                    {
                        var newInstance = onAdd;
                        BattlePassData.Instances.Add(newInstance);
                        SaveBattlePass(BattlePassData);
                    });
                    GUIUtility.ExitGUI();
                }

                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
            }
        }

        private void DrawBattlePassInstanes()
        {
            if (SelectedInstance == null)
                return;

            GUIStyle infoStyle = new GUIStyle("HelpBox");
            infoStyle.normal.background = InfoBackgroundTexture;

            GUIStyle configStyle = new GUIStyle("HelpBox");
            configStyle.normal.background = ConfigBackgroundTexture;

            GUIStyle levelStyle = new GUIStyle("HelpBox");
            levelStyle.normal.background = LevelBackgroundTexture;

            GUIStyle extraLevelStyle = new GUIStyle("HelpBox");
            extraLevelStyle.normal.background = ExtraLevelBackgroundTexture;

            using (var areaScope = new GUILayout.AreaScope(ItemsRect))
            {
                PositionScroll = GUILayout.BeginScrollView(PositionScroll);

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 12;

                var isRunning = SelectedInstance.IsActive;

                EditorGUILayout.LabelField("BattlePass ID", titleStyle);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(SelectedInstance.ID);

                GUILayout.FlexibleSpace();

                if (isRunning)
                {
                    var tickNegative = false;
                    var endDate = SelectedInstance.EndDate;
                    if (endDate != null)
                    {
                        var localTime = DateTime.UtcNow;
                        var endLocalTime = endDate.GetValueOrDefault();
                        var timeSpan = endLocalTime.Subtract(localTime);
                        tickNegative = timeSpan.Ticks <= 0;
                        var totalDays = (int)timeSpan.TotalDays;
                        var timeString = timeSpan.ToString(DateUtils.StoreTimerFormat);
                        var sBuilder = new StringBuilder();
                        if (tickNegative)
                        {
                            sBuilder.Append("Processing... ");
                        }
                        else
                        {
                            sBuilder.Append("Battle Pass end in ");
                            sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
                            sBuilder.Append(timeString);
                        }

                        var dateTitle = sBuilder.ToString();
                        EditorGUILayout.LabelField(dateTitle, titleStyle);
                    }

                    if (EditorUtils.DrawButton("Update", EditorData.AddPrizeColor, 12, AddButtonOptions))
                    {
                        GetBattlePassData();
                    }

                    if (EditorUtils.DrawButton("Stop", EditorData.RemoveColor, 12,
                            new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        StopBattlePassInstance(SelectedInstance.ID);
                    }
                }
                else
                {
                    if (EditorUtils.DrawButton("Start", EditorData.AddPrizeColor, 12,
                            new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        if (IsInstanceValid())
                        {
                            StartBattlePassInstance(SelectedInstance.ID);
                        }
                    }

                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12,
                            new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove this instance?",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveBattlePassInstance(SelectedInstance);
                                SelectedInstance = null;
                                SaveBattlePass(BattlePassData);
                                break;
                        }

                        if (SelectedInstance == null)
                        {
                            BattlePassIndex = 0;
                            return;
                        }
                    }

                    if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12,
                            new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        SaveBattlePass(BattlePassData);
                    }
                }

                GUILayout.EndHorizontal();

                // draw titles
                GUILayout.Space(10);
                SelectedTitleBar = GUILayout.Toolbar(SelectedTitleBar, Titles, GUILayout.MaxWidth(1200));
                GUILayout.Space(10);

                EditorGUI.BeginDisabledGroup(isRunning);

                if (SelectedTitleBar == 0)
                {
                    // draw state
                    SelectedInstance.IsActive =
                        EditorUtils.DrawEnableState(SelectedInstance.IsActive, "Running?", true);
                    if (SelectedInstance.IsActive)
                    {
                        EditorGUILayout.HelpBox("Instance is running now.", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Instance is not running. Edit mode.", MessageType.Info);
                    }

                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("General Info", titleStyle);
                    GUILayout.Space(10);

                    // info scope
                    using (var horizontalScope = new GUILayout.VerticalScope(infoStyle))
                    {
                        // draw name
                        EditorGUILayout.LabelField("Display Name", titleStyle);
                        SelectedInstance.DisplayName = EditorGUILayout.TextField(SelectedInstance.DisplayName,
                            new GUILayoutOption[] { GUILayout.Width(400) });
                        GUILayout.Space(5);

                        // draw description
                        var descriptionTitle = new GUIStyle(GUI.skin.textField);
                        descriptionTitle.wordWrap = true;
                        EditorGUILayout.LabelField("Description", titleStyle);
                        SelectedInstance.Description = EditorGUILayout.TextArea(SelectedInstance.Description,
                            descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });
                        GUILayout.Space(5);

                        // draw customs properties
                        EditorGUILayout.LabelField("Custom Data", titleStyle);
                        var rawData = CustomDataDrawer.Draw(SelectedInstance);
                    }

                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Config", titleStyle);
                    GUILayout.Space(10);

                    // config scope
                    using (var horizontalScope = new GUILayout.VerticalScope(configStyle))
                    {
                        // draw duration
                        EditorGUILayout.LabelField("Battle pass duration in hours", titleStyle);
                        GUILayout.BeginHorizontal();
                        SelectedInstance.DurationInHours =
                            EditorGUILayout.IntField(SelectedInstance.DurationInHours, GUILayout.Width(200));
                        EditorGUILayout.LabelField("hours");
                        GUILayout.EndHorizontal();
                        if (SelectedInstance.DurationInHours < 0)
                        {
                            SelectedInstance.DurationInHours = 0;
                        }
                        else if (SelectedInstance.DurationInHours == 0)
                        {
                            EditorGUILayout.HelpBox("Battle pass duration cannot be 0.", MessageType.Error);
                        }

                        GUILayout.Space(5);

                        // draw exp step
                        EditorGUILayout.LabelField("Experience step", titleStyle);
                        var expStep = EditorGUILayout.IntField(SelectedInstance.ExpStep,
                            new GUILayoutOption[] { GUILayout.Width(200) });
                        if (expStep < 1)
                            expStep = 1;
                        SelectedInstance.ExpStep = expStep;
                        EditorGUILayout.HelpBox(
                            "Describes how much experience the player needs to gain in order to reach the next level.",
                            MessageType.Info);
                        GUILayout.Space(5);

                        // draw extra levels
                        EditorGUILayout.LabelField("Enable Extra Level?", titleStyle);
                        SelectedInstance.ExtraLevelsEnabled =
                            EditorGUILayout.Toggle(SelectedInstance.ExtraLevelsEnabled);
                        EditorGUILayout.HelpBox(
                            "Extra levels can be offered to the player as additional content for an additional fee.",
                            MessageType.Info);

                        // draw tasks
                        EditorGUILayout.LabelField("Enable Tasks?", titleStyle);
                        SelectedInstance.TasksEnabled = EditorGUILayout.Toggle(SelectedInstance.TasksEnabled);
                        if (SelectedInstance.TasksEnabled)
                        {
                            SelectedInstance.TasksPoolID = EditorGUILayout.TextField("Tasks Pool ID",
                                SelectedInstance.TasksPoolID, new GUILayoutOption[] { GUILayout.Width(300) });
                            if (string.IsNullOrEmpty(SelectedInstance.TasksPoolID))
                            {
                                EditorGUILayout.HelpBox("The field cannot be empty.", MessageType.Error);
                            }
                        }

                        EditorGUILayout.HelpBox("Enable additional tasks for the player during the Battle Pass.",
                            MessageType.Info);

                        // draw bank
                        EditorGUILayout.LabelField("Enable Bank?", titleStyle);
                        SelectedInstance.BankEnabled = EditorGUILayout.Toggle(SelectedInstance.BankEnabled);
                        if (SelectedInstance.BankEnabled)
                        {
                            SelectedInstance.BankRewardDelivery =
                                EditorUtils.DrawRewardDelivery(SelectedInstance.BankRewardDelivery);
                        }

                        EditorGUILayout.HelpBox(
                            "Enable bank system. Allows you to accumulate additional rewards for the player. Rewards will be awarded at the end of the season.",
                            MessageType.Info);

                        // draw time limited
                        EditorGUILayout.LabelField("Enable Time Limit For Reward?", titleStyle);
                        SelectedInstance.TimeLimitedRewardEnabled =
                            EditorGUILayout.Toggle(SelectedInstance.TimeLimitedRewardEnabled);
                        if (SelectedInstance.TimeLimitedRewardEnabled)
                        {
                            SelectedInstance.AvailableRewardsPerDay = EditorGUILayout.IntField("Available per day",
                                SelectedInstance.AvailableRewardsPerDay,
                                new GUILayoutOption[] { GUILayout.Width(300) });
                            if (SelectedInstance.AvailableRewardsPerDay < 1)
                            {
                                SelectedInstance.AvailableRewardsPerDay = 1;
                            }
                        }

                        EditorGUILayout.HelpBox(
                            "Limits the player in receiving the number of rewards per day. Can be disabled for an additional fee.",
                            MessageType.Info);
                        
                        // draw automatic reward
                        EditorGUILayout.LabelField("Automatic Reward On End?", titleStyle);
                        SelectedInstance.AutomaticRewardOnEnd = EditorGUILayout.Toggle(SelectedInstance.AutomaticRewardOnEnd);
                        if (SelectedInstance.AutomaticRewardOnEnd)
                        {
                            SelectedInstance.AutomaticRewardDelivery =
                                EditorUtils.DrawRewardDelivery(SelectedInstance.AutomaticRewardDelivery);
                        }
                        EditorGUILayout.HelpBox(
                            "Allows you to automatically send rewards for the Battle Pass that the player did not manage to collect.",
                            MessageType.Info);
                    }

                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Level Tree", titleStyle);
                    GUILayout.Space(10);

                    // level scope
                    using (var horizontalScope = new GUILayout.VerticalScope(levelStyle))
                    {
                        // draw levels 
                        var levelTree = SelectedInstance.LevelTree ?? new List<BattlePassLevel>();
                        DrawToolbar();
                        for (int i = 0; i < levelTree.Count; i++)
                        {
                            GUILayout.Space(10);
                            GUILayout.BeginHorizontal();
                            var level = levelTree[i];
                            var rewardObject = CurrentRewardView == LevelTreeType.Default
                                ? level.DefaultReward
                                : level.PremiumReward;
                            rewardObject = rewardObject ?? new RewardObject();

                            var bankRewardAvailable = SelectedInstance.BankEnabled && level.BankReward != null &&
                                                      !level.BankReward.IsEmpty();

                            EditorUtils.DrawButton("Level " + (i + 1), EditorData.AddPrizeColor, 12,
                                new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(50) });
                            GUILayout.Space(10);

                            // draw rewards
                            EditorUtils.DrawReward(rewardObject, 50, ItemDirection.NONE);

                            GUILayout.FlexibleSpace();

                            // draw reward button
                            if (EditorUtils.DrawButton("+ Reward", EditorData.AddPrizeColor, 12,
                                    new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(90) }))
                            {
                                ShowPrizeDialog(rewardObject, true, result =>
                                {
                                    if (CurrentRewardView == LevelTreeType.Default)
                                    {
                                        level.DefaultReward = result;
                                    }
                                    else
                                    {
                                        level.PremiumReward = result;
                                    }
                                });
                            }

                            if (SelectedInstance.BankEnabled)
                            {
                                var bankButtonTitle = bankRewardAvailable ? "Edit Bank Reward" : "+ Bank Reward";
                                var bankButtonColor = bankRewardAvailable ? EditorData.EditColor : EditorData.AddColor;
                                // draw reward button
                                if (EditorUtils.DrawButton(bankButtonTitle, bankButtonColor, 12,
                                        new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(120) }))
                                {
                                    ShowPrizeDialog(level.BankReward, true, result => { level.BankReward = result; });
                                }
                            }

                            GUILayout.EndHorizontal();


                            if (bankRewardAvailable)
                            {
                                GUILayout.Space(10);
                                GUILayout.BeginHorizontal();
                                var bankReward = level.BankReward;
                                GUIStyle iconStyle = new GUIStyle("Label");
                                var bankTexture = ResourcesUtils.GetBankImage();
                                GUILayout.Button(bankTexture, iconStyle, GUILayout.Width(120), GUILayout.Height(50));
                                //EditorUtils.DrawButton("Bank " + (i + 1), EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(50) });
                                GUILayout.Space(10);
                                EditorUtils.DrawReward(bankReward, 50, ItemDirection.NONE);

                                GUILayout.FlexibleSpace();
                                if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12,
                                        new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(120) }))
                                {
                                    level.BankReward = null;
                                }

                                GUILayout.EndHorizontal();
                            }

                            SelectedInstance.LevelTree = levelTree;
                        }

                        // draw level buttons
                        GUILayout.Space(20);
                        GUILayout.BeginHorizontal();
                        if (EditorUtils.DrawButton("Add +1 level", EditorData.AddColor, 12, AddButtonOptions))
                        {
                            AddLevels(1);
                        }

                        if (EditorUtils.DrawButton("Add +10 levels", EditorData.AddColor, 12, AddButtonOptions))
                        {
                            AddLevels(10);
                        }

                        if (EditorUtils.DrawButton("Remove Last", EditorData.RemoveColor, 12, AddButtonOptions))
                        {
                            RemoveLastLevel();
                        }

                        GUILayout.EndHorizontal();
                    }

                    // extra level scope
                    if (SelectedInstance.ExtraLevelsEnabled)
                    {
                        GUILayout.Space(10);
                        EditorGUILayout.LabelField("Extra Level Tree", titleStyle);
                        GUILayout.Space(10);

                        using (var horizontalScope = new GUILayout.VerticalScope(extraLevelStyle))
                        {
                            // draw levels 
                            var levelTree = SelectedInstance.ExtraLevelTree ?? new List<BattlePassLevel>();
                            var simpleLevelsCount = SelectedInstance.LevelTree == null
                                ? 0
                                : SelectedInstance.LevelTree.Count;
                            DrawToolbar();
                            for (int i = 0; i < levelTree.Count; i++)
                            {
                                GUILayout.Space(10);
                                GUILayout.BeginHorizontal();
                                var level = levelTree[i];
                                var rewardObject = CurrentRewardView == LevelTreeType.Default
                                    ? level.DefaultReward
                                    : level.PremiumReward;
                                rewardObject = rewardObject ?? new RewardObject();
                                var bankRewardAvailable = SelectedInstance.BankEnabled && level.BankReward != null &&
                                                          !level.BankReward.IsEmpty();

                                EditorUtils.DrawButton("Level " + (i + 1 + simpleLevelsCount), EditorData.AddPrizeColor,
                                    12, new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(50) });
                                GUILayout.Space(10);

                                // draw rewards
                                EditorUtils.DrawReward(rewardObject, 50, ItemDirection.NONE);
                                GUILayout.FlexibleSpace();

                                // draw add button
                                if (EditorUtils.DrawButton("+ Reward", EditorData.AddPrizeColor, 12,
                                        new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(90) }))
                                {
                                    ShowPrizeDialog(rewardObject, true, result =>
                                    {
                                        if (CurrentRewardView == LevelTreeType.Default)
                                        {
                                            level.DefaultReward = result;
                                        }
                                        else
                                        {
                                            level.PremiumReward = result;
                                        }
                                    });
                                }

                                if (SelectedInstance.BankEnabled)
                                {
                                    var bankButtonTitle = bankRewardAvailable ? "Edit Bank Reward" : "+ Bank Reward";
                                    var bankButtonColor =
                                        bankRewardAvailable ? EditorData.EditColor : EditorData.AddColor;
                                    // draw reward button
                                    if (EditorUtils.DrawButton(bankButtonTitle, bankButtonColor, 12,
                                            new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(120) }))
                                    {
                                        ShowPrizeDialog(level.BankReward, true,
                                            result => { level.BankReward = result; });
                                    }
                                }

                                GUILayout.EndHorizontal();

                                if (bankRewardAvailable)
                                {
                                    GUILayout.Space(10);
                                    GUILayout.BeginHorizontal();
                                    var bankReward = level.BankReward;
                                    GUIStyle iconStyle = new GUIStyle("Label");
                                    var bankTexture = ResourcesUtils.GetBankImage();
                                    GUILayout.Button(bankTexture, iconStyle, GUILayout.Width(120),
                                        GUILayout.Height(50));
                                    //EditorUtils.DrawButton("Bank " + (i + 1 + simpleLevelsCount), EditorData.EditColor, 12, new GUILayoutOption[] { GUILayout.Width(120), GUILayout.Height(50) });
                                    GUILayout.Space(10);
                                    EditorUtils.DrawReward(bankReward, 50, ItemDirection.NONE);

                                    GUILayout.FlexibleSpace();
                                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12,
                                            new GUILayoutOption[] { GUILayout.Height(50), GUILayout.Width(120) }))
                                    {
                                        level.BankReward = null;
                                    }

                                    GUILayout.EndHorizontal();
                                }
                            }

                            // draw level buttons
                            GUILayout.Space(20);
                            GUILayout.BeginHorizontal();
                            if (EditorUtils.DrawButton("Add +1 level", EditorData.AddColor, 12, AddButtonOptions))
                            {
                                AddExtraLevels(1);
                            }

                            if (EditorUtils.DrawButton("Add +10 levels", EditorData.AddColor, 12, AddButtonOptions))
                            {
                                AddExtraLevels(10);
                            }

                            if (EditorUtils.DrawButton("Remove Last", EditorData.RemoveColor, 12, AddButtonOptions))
                            {
                                RemoveLastExtraLevel();
                            }

                            GUILayout.EndHorizontal();

                            SelectedInstance.ExtraLevelTree = levelTree;
                        }
                    }
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (EditorUtils.DrawButton("Add new ticket", EditorData.AddColor, 12,
                            new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        AddBattlePassTicketWindow.Show(SelectedInstance.ID, onAdd =>
                        {
                            var newTicket = onAdd;
                            AddTicket(newTicket);
                        });
                        GUIUtility.ExitGUI();
                    }

                    GUILayout.EndHorizontal();

                    GUILayout.Space(10);

                    // draw tickets
                    var freeTicket = SelectedInstance.GetFreeTicket();
                    DrawTicket(freeTicket, true);
                    GUILayout.Space(10);

                    var paidTickets = SelectedInstance.GetPaidTickets();
                    for (int i = 0; i < paidTickets.Count; i++)
                    {
                        var ticket = paidTickets[i];
                        DrawTicket(ticket, false);
                        GUILayout.Space(10);
                    }
                }

                GUILayout.Space(110);
                GUILayout.EndScrollView();
                EditorGUI.EndDisabledGroup();
            }
        }

        private void DrawTicket(BattlePassTicket ticket, bool isFree)
        {
            GUIStyle ticketTitleStyle = new GUIStyle("HelpBox");
            ticketTitleStyle.normal.background = TicketTitleTexture;
            GUIStyle ticketContentStyle = new GUIStyle("HelpBox");
            ticketContentStyle.normal.background = TicketContentTexture;
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var ticketItem = CatalogItems.FirstOrDefault(x => x.ItemId == ticket.GetCatalogID());
            if (ticketItem == null && !isFree)
            {
                CatalogItems.Add(new CatalogItem
                {
                    ItemId = ticket.GetCatalogID()
                });
            }

            using (var horizontalScope = new GUILayout.VerticalScope(ticketTitleStyle))
            {
                GUILayout.BeginHorizontal();
                var displayName = isFree ? "Free Ticket" : ticket.DisplayName;
                EditorGUILayout.LabelField(displayName, titleStyle, GUILayout.Height(30));
                GUILayout.FlexibleSpace();
                EditorGUI.BeginDisabledGroup(isFree);
                if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12,
                        new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(100) }))
                {
                    int option = EditorUtility.DisplayDialogComplex("Warning",
                        "Are you sure you want to remove this ticket?",
                        "Yes",
                        "No",
                        string.Empty);
                    switch (option)
                    {
                        // ok.
                        case 0:
                            RemoveTicket(ticket);
                            break;
                    }
                }

                EditorGUI.EndDisabledGroup();
                GUILayout.EndHorizontal();
            }

            using (var horizontalScope = new GUILayout.VerticalScope(ticketContentStyle))
            {
                if (!isFree)
                {
                    // draw id
                    EditorGUILayout.LabelField("Ticket ID", titleStyle);
                    EditorGUILayout.LabelField(ticket.ID, new GUILayoutOption[] { GUILayout.Width(400) });
                    GUILayout.Space(5);

                    // draw id
                    EditorGUILayout.LabelField("Ticket Catalog ID", titleStyle);
                    EditorGUILayout.LabelField(ticket.GetCatalogID(), new GUILayoutOption[] { GUILayout.Width(400) });
                    EditorGUILayout.HelpBox("Ticket Catalog ID is used to configure IAP in Google Play and App Store.",
                        MessageType.Info);
                    GUILayout.Space(5);

                    // draw name
                    EditorGUILayout.LabelField("Display Name", titleStyle);
                    ticket.DisplayName = EditorGUILayout.TextField(ticket.DisplayName,
                        new GUILayoutOption[] { GUILayout.Width(400) });
                    GUILayout.Space(5);

                    // draw description
                    var descriptionTitle = new GUIStyle(GUI.skin.textField);
                    descriptionTitle.wordWrap = true;
                    EditorGUILayout.LabelField("Description", titleStyle);
                    ticket.Description = EditorGUILayout.TextArea(ticket.Description, descriptionTitle,
                        new GUILayoutOption[] { GUILayout.Height(150) });
                    GUILayout.Space(5);

                    // draw purchase type
                    EditorGUILayout.LabelField("Purchase Type", titleStyle);
                    ticket.PurchaseType = (CBSPurchaseType)EditorGUILayout.EnumPopup(ticket.PurchaseType,
                        new GUILayoutOption[] { GUILayout.Width(150) });
                    EditorGUILayout.HelpBox(
                        "NOT_CONSUMABLE - Can only be purchased once while the Battle Pass instance is active. CONSUMABLE - ticket can be purchased multiple times.",
                        MessageType.Info);
                    GUILayout.Space(5);

                    // draw price
                    if (!isFree && CacheCurrencies != null && CacheCurrencies.Count != 0 && ticketItem != null)
                    {
                        var currenciesWithRM = CacheCurrencies.ToList();
                        currenciesWithRM.Add(PlayfabUtils.REAL_MONEY_CODE);
                        EditorGUILayout.LabelField("Purchase Price", titleStyle,
                            new GUILayoutOption[] { GUILayout.Width(170) });
                        var cbsPrice = ticket.Price ?? new CBSPrice();
                        var itemPrice = ticketItem.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
                        var currencyCode = cbsPrice.CurrencyID;
                        var selectedCurrencyIndex = string.IsNullOrEmpty(currencyCode)
                            ? 0
                            : currenciesWithRM.IndexOf(currencyCode);
                        selectedCurrencyIndex = EditorGUILayout.Popup("Code", selectedCurrencyIndex,
                            currenciesWithRM.ToArray(), GUILayout.Width(250));
                        currencyCode = currenciesWithRM[selectedCurrencyIndex];
                        cbsPrice.CurrencyID = currencyCode;

                        if (currencyCode == PlayfabUtils.REAL_MONEY_CODE)
                        {
                            EditorUtils.DrawRealMoneyPrice(cbsPrice);
                        }
                        else
                        {
                            cbsPrice.CurrencyValue =
                                EditorGUILayout.IntField("Value", cbsPrice.CurrencyValue, GUILayout.Width(250));
                            if (cbsPrice.CurrencyValue < 0)
                                cbsPrice.CurrencyValue = 0;
                        }

                        ticket.Price = cbsPrice;
                        itemPrice.Clear();
                        itemPrice[currencyCode] = (uint)cbsPrice.CurrencyValue;
                        ticketItem.VirtualCurrencyPrices = itemPrice;
                        GUILayout.Space(5);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Free ticket will be available to the player as soon as the battle pass starts.",
                        MessageType.Info);
                }

                // draw exp multipliyer
                EditorGUILayout.LabelField("Override Exp Multipliyer", titleStyle);
                ticket.OverrideExpMultiply = EditorGUILayout.Toggle(ticket.OverrideExpMultiply);
                if (ticket.OverrideExpMultiply)
                {
                    ticket.ExpMultiply = EditorGUILayout.FloatField(ticket.ExpMultiply, GUILayout.Width(150));
                }

                GUILayout.Space(5);

                // draw premium reward
                EditorGUILayout.LabelField("Enable Premium Rewards", titleStyle);
                ticket.PremiumAccess = EditorGUILayout.Toggle(ticket.PremiumAccess);
                GUILayout.Space(5);

                // draw extra levels
                if (SelectedInstance.ExtraLevelsEnabled)
                {
                    EditorGUILayout.LabelField("Enable Extra Levels", titleStyle);
                    ticket.ExtraLevelAccess = EditorGUILayout.Toggle(ticket.ExtraLevelAccess);
                    GUILayout.Space(5);
                }

                // draw bank
                if (SelectedInstance.BankEnabled)
                {
                    EditorGUILayout.LabelField("Enable Bank", titleStyle);
                    ticket.BankAccess = EditorGUILayout.Toggle(ticket.BankAccess);
                    GUILayout.Space(5);
                }

                // draw tasks
                if (SelectedInstance.TasksEnabled)
                {
                    EditorGUILayout.LabelField("Enable Tasks", titleStyle);
                    ticket.TasksAccess = EditorGUILayout.Toggle(ticket.TasksAccess);
                    GUILayout.Space(5);
                }

                // draw time limits
                if (SelectedInstance.TimeLimitedRewardEnabled)
                {
                    EditorGUILayout.LabelField("Disable Rewards Time Limit", titleStyle);
                    ticket.DisableTimeLimit = EditorGUILayout.Toggle(ticket.DisableTimeLimit);
                    GUILayout.Space(5);
                }

                // draw skip levels
                if (!isFree)
                {
                    EditorGUILayout.LabelField("Skip levels", titleStyle);
                    ticket.SkipLevel = EditorGUILayout.Toggle(ticket.SkipLevel);
                    if (ticket.SkipLevel)
                    {
                        ticket.SkipLevelCount = EditorGUILayout.IntField(ticket.SkipLevelCount, GUILayout.Width(150));
                    }

                    GUILayout.Space(5);
                }
            }
        }

        private void AddLevels(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var tree = SelectedInstance.LevelTree ?? new List<BattlePassLevel>();
                tree.Add(new BattlePassLevel());
                SelectedInstance.LevelTree = tree;
            }
        }

        private void RemoveLastLevel()
        {
            var tree = SelectedInstance.LevelTree ?? new List<BattlePassLevel>();
            if (tree.Count == 0)
                return;
            tree.RemoveAt(tree.Count - 1);
            tree.TrimExcess();
            SelectedInstance.LevelTree = tree;
        }

        private void RemoveLastExtraLevel()
        {
            var tree = SelectedInstance.LevelTree ?? new List<BattlePassLevel>();
            if (tree.Count == 0)
                return;
            tree.RemoveAt(tree.Count - 1);
            tree.TrimExcess();
            SelectedInstance.LevelTree = tree;
        }

        private void AddExtraLevels(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var tree = SelectedInstance.ExtraLevelTree ?? new List<BattlePassLevel>();
                tree.Add(new BattlePassLevel { IsExtraLevel = true });
                SelectedInstance.ExtraLevelTree = tree;
            }
        }

        private void GetBattlePassData()
        {
            ShowProgress();
            var catalogRequest = new GetCatalogItemsRequest
            {
                CatalogVersion = CatalogKeys.BattlePassCatalogID
            };

            PlayFabAdminAPI.GetCatalogItems(catalogRequest, onGetCatalog =>
            {
                CatalogItems = onGetCatalog.Catalog ?? new List<CatalogItem>();

                PlayFabAdminAPI.ListVirtualCurrencyTypes(new PlayFab.AdminModels.ListVirtualCurrencyTypesRequest { },
                    onGetCurrencies =>
                    {
                        CacheCurrencies = onGetCurrencies.VirtualCurrencies.Select(x => x.CurrencyCode).ToList();

                        var keys = new List<string>();
                        keys.Add(TitleKeys.BattlePassDataKey);

                        var request = new GetTitleDataRequest
                        {
                            Keys = keys
                        };
                        PlayFabAdminAPI.GetTitleInternalData(request, OnInternalDataGetted, OnGetDataFailed);
                    }, onFailed =>
                    {
                        HideProgress();
                        AddErrorLog(onFailed);
                    });
            }, onError =>
            {
                HideProgress();
                AddErrorLog(onError);
            });
        }

        private void OnInternalDataGetted(GetTitleDataResult result)
        {
            HideProgress();
            var dictionary = result.Data;
            bool keyExist = dictionary.ContainsKey(TitleKeys.BattlePassDataKey);
            var rawData = keyExist ? dictionary[TitleKeys.BattlePassDataKey] : JsonPlugin.EMPTY_JSON;
            BattlePassData = new BattlePassData();
            try
            {
                BattlePassData = JsonPlugin.FromJsonDecompress<BattlePassData>(rawData);
            }
            catch
            {
                BattlePassData = JsonPlugin.FromJson<BattlePassData>(rawData);
            }
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveBattlePass(BattlePassData battlePassData)
        {
            ShowProgress();

            var catalogRequest = new UpdateCatalogItemsRequest
            {
                Catalog = CatalogItems,
                CatalogVersion = CatalogKeys.BattlePassCatalogID,
                SetAsDefaultCatalog = false
            };
            PlayFabAdminAPI.SetCatalogItems(catalogRequest, onUpdate => { InternalSaveBattlePass(battlePassData); },
                onFailed =>
                {
                    var errorCode = onFailed.Error;
                    if (errorCode == PlayFabErrorCode.InvalidParams)
                    {
                        InternalSaveBattlePass(battlePassData);
                    }
                    else
                    {
                        HideProgress();
                        AddErrorLog(onFailed);
                    }
                });
        }

        private void InternalSaveBattlePass(BattlePassData battlePassData)
        {
            string rawData = JsonPlugin.ToJsonCompress(battlePassData);

            var request = new SetTitleDataRequest
            {
                Key = TitleKeys.BattlePassDataKey,
                Value = rawData
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSaveBattlePass, OnSaveDataFailed);
        }

        private void OnSaveBattlePass(SetTitleDataResult result)
        {
            HideProgress();
            GetBattlePassData();
        }

        private void OnSaveDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void RemoveBattlePassInstance(BattlePassInstance instance)
        {
            if (BattlePassData.Instances.Contains(instance))
            {
                BattlePassData.Instances.Remove(instance);
                var tickets = instance.GetPaidTickets();
                foreach (var ticket in tickets)
                {
                    RemoveTicket(ticket);
                }
            }
        }

        private void RemoveTicket(BattlePassTicket ticket)
        {
            var catalogID = ticket.GetCatalogID();
            var catalogItem = CatalogItems.FirstOrDefault(x => x.ItemId == catalogID);
            if (catalogItem != null)
            {
                CatalogItems.Remove(catalogItem);
            }

            CatalogItems.TrimExcess();
            SelectedInstance.RemoveTicket(ticket);
        }

        private void AddTicket(BattlePassTicket ticket)
        {
            var catalogID = ticket.GetCatalogID();
            var catalogItem = CatalogItems.FirstOrDefault(x => x.ItemId == catalogID);
            if (catalogItem != null)
            {
                CatalogItems.Add(new CatalogItem
                {
                    ItemId = catalogID,
                    DisplayName = ticket.DisplayName
                });
            }

            SelectedInstance.AddTicket(ticket);
        }

        private bool IsInstanceValid()
        {
            if (SelectedInstance == null)
                return false;
            return SelectedInstance.DurationInHours > 0;
        }

        private void ShowPrizeDialog(RewardObject reward, bool includeCurrencies, Action<RewardObject> modifyCallback)
        {
            if (CachedItemCategories == null || CachedItems == null || CacheCurrencies == null ||
                CachedLootBoxCategories == null)
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

        private void StartBattlePassInstance(string battlePassID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.StartBattlePassInstanceMethod,
                    FunctionParameter = new FunctionIDRequest
                    {
                        ID = battlePassID
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
                        var functionResult = OnGet.GetResult<FunctionStartBattlePassResult>();
                        var endDate = functionResult.EndDate;
                        var now = DateTime.UtcNow;
                        HideProgress();
                        GetBattlePassData();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void StopBattlePassInstance(string battlePassID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.StopBattlePassInstanceMethod,
                    FunctionParameter = new FunctionIDRequest
                    {
                        ID = battlePassID
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
                        var functionResult = OnGet.GetResult<FunctionStopBattlePassResult>();
                        HideProgress();
                        GetBattlePassData();
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

            PlayFabAuthenticationAPI.GetEntityToken(
                request,
                result =>
                {
                    onGet?.Invoke();
                },
                error =>
                {
                    AddErrorLog(error);
                    HideProgress();
                }
            );
        }

        public enum LevelTreeType
        {
            Default,
            Premium
        }
    }
}
#endif
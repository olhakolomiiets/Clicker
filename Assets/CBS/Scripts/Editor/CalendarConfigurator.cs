#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.ServerModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class CalendarConfigurator : BaseConfigurator
    {
        private readonly string CALENDAR_TITLE_ID = TitleKeys.CalendarTitleKey;
        private CalendarInstance CalendarInstance { get; set; }
        private CalendarContainer CalendarContainer { get; set; }
        private int CalendarIndex { get; set; }
        private int SelectedToolBar { get; set; }
        private int SelectedMonth { get; set; }

        private Rect CategoriesRect = new Rect(0, 0, 150, 700);
        private Rect ItemsRect = new Rect(200, 100, 850, 700);
        private Vector2 PositionScroll { get; set; }
        private Vector2 ContentScroll { get; set; }
        private List<PlayFab.AdminModels.CatalogItem> CalendarItems { get; set; }
        private ObjectCustomDataDrawer<CBSCalendarCustomData> CustomDataDrawer { get; set; }
        private CalendarIcons CalendarIcons { get; set; }

        private List<PlayFab.AdminModels.CatalogItem> CachedItems { get; set; }
        private Categories CachedItemCategories { get; set; }
        private Categories CachedLootBoxCategories { get; set; }
        private List<string> CacheCurrencies { get; set; }
        private List<DayOfWeek> DaysOfWeek { get; set; }
        private List<Month> Months { get; set; }

        private ItemsIcons Icons { get; set; }

        private GUILayoutOption[] AddButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(120) };
            }
        }

        private GUILayoutOption[] SaveButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Width(50) };
            }
        }

        protected override string Title => "Calendar";

        protected override bool DrawScrollView => false;

        private EditorData EditorData { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            CalendarIcons = CBSScriptable.Get<CalendarIcons>();
            Icons = CBSScriptable.Get<ItemsIcons>();
            DaysOfWeek = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(v => v).ToList();
            DaysOfWeek.RemoveAt(0);
            DaysOfWeek.TrimExcess();
            DaysOfWeek.Add(DayOfWeek.Sunday);
            Months = Enum.GetValues(typeof(Month)).Cast<Month>().Select(v => v).ToList();
            CustomDataDrawer = new ObjectCustomDataDrawer<CBSCalendarCustomData>(PlayfabUtils.DEFAULT_CUSTOM_DATA_SIZE, 830f);
            GetCalendarContainer();
        }

        private void DrawCalendar()
        {
            // draw level table
            if (CalendarInstance != null && CalendarItems != null)
            {
                using (var areaScope = new GUILayout.AreaScope(ItemsRect))
                {
                    ContentScroll = GUILayout.BeginScrollView(ContentScroll);
                    var titleStyle = new GUIStyle(GUI.skin.label);
                    titleStyle.fontStyle = FontStyle.Bold;
                    titleStyle.fontSize = 12;

                    var calendarID = CalendarInstance.ID;
                    var calendarItem = CalendarItems.FirstOrDefault(x => x.ItemId == calendarID);
                    if (calendarItem == null)
                    {
                        CalendarItems.Add(new PlayFab.AdminModels.CatalogItem
                        {
                            ItemId = calendarID
                        });
                    }

                    // draw id
                    GUILayout.BeginHorizontal();
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Calendar ID", titleStyle);
                    EditorGUILayout.LabelField(CalendarInstance.ID);
                    GUILayout.EndVertical();
                    GUILayout.FlexibleSpace();

                    // draw remove
                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
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
                                RemoveCalendarInstance(CalendarInstance);
                                CalendarInstance = null;
                                break;
                        }
                        if (CalendarInstance == null)
                        {
                            CalendarIndex = 0;
                            return;
                        }
                    }

                    // draw save
                    if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                    {
                        UpdateCalendarContainer(CalendarContainer);
                    }

                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    // draw state
                    CalendarInstance.Enabled = EditorUtils.DrawEnableState(CalendarInstance.Enabled, "Enabled?");
                    EditorGUILayout.HelpBox("If not enabled - instance will not be visible to profiles.", MessageType.Info);
                    GUILayout.Space(10);

                    // draw template
                    EditorGUILayout.LabelField("Template", titleStyle, new GUILayoutOption[] { GUILayout.Width(170) });
                    CalendarInstance.Template = (CalendarTemplate)EditorGUILayout.EnumPopup(CalendarInstance.Template, new GUILayoutOption[] { GUILayout.Width(200) });
                    GUILayout.Space(10);

                    // draw activation
                    if (CalendarInstance.Template == CalendarTemplate.CUSTOM)
                    {
                        EditorGUILayout.LabelField("Activation", titleStyle, new GUILayoutOption[] { GUILayout.Width(170) });
                        CalendarInstance.Activation = (ActivationType)EditorGUILayout.EnumPopup(CalendarInstance.Activation, new GUILayoutOption[] { GUILayout.Width(200) });
                        GUILayout.Space(10);
                    }
                    else
                    {
                        CalendarInstance.Activation = ActivationType.ALWAYS_AVAILABLE;
                    }

                    // draw price
                    if (CalendarInstance.Activation == ActivationType.BY_PURCHASE)
                    {
                        if (CacheCurrencies != null && CacheCurrencies.Count != 0)
                        {
                            var currenciesWithRM = CacheCurrencies.ToList();
                            currenciesWithRM.Add(PlayfabUtils.REAL_MONEY_CODE);
                            EditorGUILayout.LabelField("Purchase Price", titleStyle, new GUILayoutOption[] { GUILayout.Width(170) });
                            var cbsPrice = CalendarInstance.Price ?? new CBSPrice();
                            var itemPrice = calendarItem.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
                            var currencyCode = cbsPrice.CurrencyID;
                            var selectedCurrencyIndex = string.IsNullOrEmpty(currencyCode) ? 0 : currenciesWithRM.IndexOf(currencyCode);
                            selectedCurrencyIndex = EditorGUILayout.Popup("Code", selectedCurrencyIndex, currenciesWithRM.ToArray(), GUILayout.Width(250));
                            currencyCode = currenciesWithRM[selectedCurrencyIndex];
                            cbsPrice.CurrencyID = currencyCode;

                            if (currencyCode == PlayfabUtils.REAL_MONEY_CODE)
                            {
                                EditorUtils.DrawRealMoneyPrice(cbsPrice);
                            }
                            else
                            {
                                cbsPrice.CurrencyValue = EditorGUILayout.IntField("Value", cbsPrice.CurrencyValue, GUILayout.Width(250));
                                if (cbsPrice.CurrencyValue < 0)
                                    cbsPrice.CurrencyValue = 0;
                            }
                            CalendarInstance.Price = cbsPrice;
                            itemPrice.Clear();
                            itemPrice[currencyCode] = (uint)cbsPrice.CurrencyValue;
                            calendarItem.VirtualCurrencyPrices = itemPrice;
                            GUILayout.Space(10);
                        }
                    }

                    // draw loop
                    if (CalendarInstance.Template == CalendarTemplate.CUSTOM)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Looped?", titleStyle, new GUILayoutOption[] { GUILayout.Width(80) });
                        CalendarInstance.Looped = EditorGUILayout.Toggle(CalendarInstance.Looped, new GUILayoutOption[] { GUILayout.Width(200) });
                        GUILayout.EndHorizontal();
                        EditorGUILayout.HelpBox("If looped - calendar after the end will be reseted and available again for the profile.", MessageType.Info);
                        GUILayout.Space(10);
                    }
                    else
                    {
                        CalendarInstance.Looped = true;
                    }

                    // draw penalty
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("No Penalty?", titleStyle, new GUILayoutOption[] { GUILayout.Width(80) });
                    CalendarInstance.NoPenalty = EditorGUILayout.Toggle(CalendarInstance.NoPenalty, new GUILayoutOption[] { GUILayout.Width(200) });
                    GUILayout.EndHorizontal();
                    EditorGUILayout.HelpBox("If 'No Pentalty' == true - the user will be able to pick up the reward even if he missed it in a certain period.", MessageType.Info);
                    GUILayout.Space(10);
                    
                    // draw skipping
                    if (CalendarInstance.Template == CalendarTemplate.CUSTOM)
                    {
                        GUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("Don't increment index when skipping?", titleStyle, new GUILayoutOption[] { GUILayout.Width(250) });
                        CalendarInstance.DontIncrementIndexWhenSkipping = EditorGUILayout.Toggle(CalendarInstance.DontIncrementIndexWhenSkipping, new GUILayoutOption[] { GUILayout.Width(200) });
                        GUILayout.EndHorizontal();
                        EditorGUILayout.HelpBox("If true - the player will not increase the active calendar index if he missed more than a day.", MessageType.Info);
                        GUILayout.Space(10);
                    }
                    else
                    {
                        CalendarInstance.DontIncrementIndexWhenSkipping = false;
                    }

                    // draw name
                    EditorGUILayout.LabelField("Display Name", titleStyle);
                    CalendarInstance.DisplayName = EditorGUILayout.TextField(CalendarInstance.DisplayName, new GUILayoutOption[] { GUILayout.Width(400) });
                    GUILayout.Space(10);

                    // draw icon
                    EditorGUILayout.LabelField("Icon", titleStyle);
                    var itemSprite = CalendarIcons.GetSprite(calendarID);
                    var calendarTexture = itemSprite == null ? null : itemSprite.texture;
                    GUILayout.Button(calendarTexture, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(100) });
                    itemSprite = (Sprite)EditorGUILayout.ObjectField((itemSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.Width(150) });
                    CalendarIcons.SaveSprite(calendarID, itemSprite);
                    GUILayout.Space(10);

                    // draw description
                    var descriptionTitle = new GUIStyle(GUI.skin.textField);
                    descriptionTitle.wordWrap = true;
                    EditorGUILayout.LabelField("Description", titleStyle);
                    CalendarInstance.Description = EditorGUILayout.TextArea(CalendarInstance.Description, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });
                    GUILayout.Space(10);

                    // draw customs properties
                    EditorGUILayout.LabelField("Custom Data", titleStyle);
                    var rawData = CustomDataDrawer.Draw(CalendarInstance);
                    GUILayout.Space(10);

                    // draw position
                    GUILayout.Space(10);
                    EditorGUILayout.LabelField("Positions", titleStyle);
                    GUILayout.Space(10);
                    var positions = CalendarInstance.Positions ?? new List<CalendarPosition>();
                    if (CalendarInstance.Template == CalendarTemplate.WEEKLY_TEMPLATE)
                    {
                        positions = CalendarInstance.GetWeeklyPositions();
                    }
                    else if (CalendarInstance.Template == CalendarTemplate.MONTHLY_TEMPLATE)
                    {
                        SelectedMonth = GUILayout.Toolbar(SelectedMonth, Months.Select(x => x.ToString()).ToArray(), GUILayout.Width(830));
                        positions = CalendarInstance.GetMonthlyPositions(SelectedMonth);
                    }
                    var positionCount = positions.Count;
                    var expStyle = new GUIStyle("Label");


                    for (int i = 0; i < positionCount; i++)
                    {
                        var position = positions[i];
                        var reward = position.Reward;
                        GUILayout.BeginHorizontal();
                        string dayString = (i + 1).ToString() + " day";
                        if (CalendarInstance.Template == CalendarTemplate.WEEKLY_TEMPLATE)
                        {
                            dayString = DaysOfWeek[i].ToString();
                        }
                        var levelDetail = reward;

                        var texture = ResourcesUtils.GetRewardImage();
                        GUILayout.Button(texture, expStyle, new GUILayoutOption[] { GUILayout.Width(40), GUILayout.Height(40) });

                        EditorGUILayout.LabelField(dayString, titleStyle, new GUILayoutOption[] { GUILayout.Width(90), GUILayout.Height(40) });

                        EditorUtils.DrawReward(reward, 40, ItemDirection.HORIZONTAL, expStyle);

                        GUILayout.FlexibleSpace();

                        // draw events
                        if (EditorUtils.DrawButton("Events", EditorData.EventColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(40) }))
                        {
                            EditorUtils.ShowProfileEventWindow(position.Events, onAdd =>
                            {
                                position.Events = onAdd;
                            });
                        }

                        // draw price button
                        if (EditorUtils.DrawButton("Reward", EditorData.AddPrizeColor, 12, new GUILayoutOption[] { GUILayout.MaxWidth(80), GUILayout.Height(40) }))
                        {
                            ShowRewardDialog(reward, true, result =>
                            {
                                position.Reward = result;
                            });
                        }

                        GUILayout.EndHorizontal();

                        EditorUtils.DrawUILine(Color.grey, 1, 20);

                        GUILayout.Space(10);
                    }

                    GUILayout.Space(20);

                    GUILayout.BeginHorizontal();
                    if (CalendarInstance.Template == CalendarTemplate.CUSTOM)
                    {
                        // add new level
                        if (EditorUtils.DrawButton("Add new day", EditorData.AddColor, 12, AddButtonOptions))
                        {
                            AddNewDay();
                        }

                        if (positionCount != 0)
                        {
                            // remove last level
                            if (EditorUtils.DrawButton("Remove last day", EditorData.RemoveColor, 12, AddButtonOptions))
                            {
                                RemoveLastPosition();
                            }

                        }
                    }
                    GUILayout.EndHorizontal();

                    GUILayout.EndScrollView();
                    GUILayout.Space(150);
                }
            }
        }

        private void DrawTitles()
        {
            if (CalendarContainer == null)
                return;
            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                GUILayout.BeginVertical();

                int categoryHeight = 30;
                int categoriesCount = CalendarContainer.Instances == null ? 0 : CalendarContainer.Instances.Count;

                if (CalendarContainer != null)
                {
                    if (CalendarContainer.Instances != null && CalendarContainer.Instances.Count > 0)
                    {
                        var categoriesMenu = CalendarContainer.Instances.Select(x => x.DisplayName).ToArray();
                        CalendarIndex = GUI.SelectionGrid(new Rect(0, 100, 150, categoryHeight * categoriesCount), CalendarIndex, categoriesMenu.ToArray(), 1);
                        string selctedCategory = categoriesMenu[CalendarIndex];

                        CalendarInstance = CalendarContainer.Instances.ElementAt(CalendarIndex);
                    }
                }

                GUILayout.Space(30);
                GUILayout.Space(30);
                var oldColor = GUI.color;
                GUI.backgroundColor = EditorData.AddColor;
                var style = new GUIStyle(GUI.skin.button);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;
                if (GUI.Button(new Rect(0, 130 + categoryHeight * categoriesCount, 150, categoryHeight), "Add new Calendar", style))
                {
                    AddCalendarInstanceWindow.Show(onAdd =>
                    {
                        var newInstance = onAdd;
                        AddNewCalendarInstance(newInstance);
                    });
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
            }
        }

        protected override void OnDrawInside()
        {
            DrawTitles();
            DrawCalendar();
        }

        // get level table
        private void GetCalendarContainer()
        {
            ShowProgress();

            var catalogRequest = new PlayFab.AdminModels.GetCatalogItemsRequest
            {
                CatalogVersion = CatalogKeys.CalendarCatalogID
            };
            PlayFabAdminAPI.GetCatalogItems(catalogRequest, onGetCatalog =>
            {
                CalendarItems = onGetCatalog.Catalog ?? new List<PlayFab.AdminModels.CatalogItem>();

                PlayFabAdminAPI.ListVirtualCurrencyTypes(new PlayFab.AdminModels.ListVirtualCurrencyTypesRequest { }, onGetCurrencies =>
                {
                    CacheCurrencies = onGetCurrencies.VirtualCurrencies.Select(x => x.CurrencyCode).ToList();

                    var keyList = new List<string>();
                    keyList.Add(CALENDAR_TITLE_ID);
                    var dataRequest = new PlayFab.AdminModels.GetTitleDataRequest
                    {
                        Keys = keyList
                    };
                    PlayFabAdminAPI.GetTitleInternalData(dataRequest, OnGetCalendarContainer, OnGetContainerFailed);
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

        private void OnGetCalendarContainer(PlayFab.AdminModels.GetTitleDataResult result)
        {
            bool tableExist = result.Data.ContainsKey(CALENDAR_TITLE_ID);
            if (tableExist)
            {
                string tableRaw = result.Data[CALENDAR_TITLE_ID];
                var table = GetFromRawData(tableRaw);
                HideProgress();
                CalendarContainer = table;
            }
            else
            {
                CalendarContainer = new CalendarContainer();
            }
            HideProgress();
        }

        private CalendarContainer GetFromRawData(string rawData)
        {
            try
            {
                return JsonPlugin.FromJsonDecompress<CalendarContainer>(rawData);
            }
            catch
            {
                return JsonPlugin.FromJson<CalendarContainer>(rawData);
            }
        }

        private void OnGetContainerFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // add empty level
        private void AddNewDay()
        {
            var position = new CalendarPosition();
            CalendarInstance.AddPostion(position);
        }

        private void RemoveCalendarInstance(CalendarInstance instance)
        {
            ShowProgress();
            var calendrarID = instance.ID;
            var itemExist = CalendarItems.Any(x => x.ItemId == calendrarID);
            if (itemExist)
            {
                var calendarItem = CalendarItems.FirstOrDefault(x => x.ItemId == calendrarID);
                CalendarItems.Remove(calendarItem);
            }
            CalendarContainer.Remove(instance);
            UpdateCalendarContainer(CalendarContainer);
        }

        private void AddNewCalendarInstance(CalendarInstance instance)
        {
            ShowProgress();
            var calendrarID = instance.ID;
            var itemExist = CalendarItems.Any(x => x.ItemId == calendrarID);
            if (!itemExist)
            {
                CalendarItems.Add(new PlayFab.AdminModels.CatalogItem
                {
                    ItemId = calendrarID
                });
            }
            CalendarContainer.Add(instance);
            UpdateCalendarContainer(CalendarContainer);
        }

        private void UpdateCalendarContainer(CalendarContainer container)
        {
            ShowProgress();

            var catalogRequest = new PlayFab.AdminModels.UpdateCatalogItemsRequest
            {
                Catalog = CalendarItems,
                CatalogVersion = CatalogKeys.CalendarCatalogID,
                SetAsDefaultCatalog = false
            };
            PlayFabAdminAPI.SetCatalogItems(catalogRequest, onUpdate =>
            {
                InternalSaveContainer(container);
            }, onFailed =>
            {
                var errorCode = onFailed.Error;
                if (errorCode == PlayFabErrorCode.InvalidParams)
                {
                    InternalSaveContainer(container);
                }
                else
                {
                    HideProgress();
                    AddErrorLog(onFailed);
                }
            });
        }

        private void InternalSaveContainer(CalendarContainer container)
        {
            container.CleanUp();
            string listRaw = JsonPlugin.ToJsonCompress(container);

            var dataRequest = new SetTitleDataRequest
            {
                Key = CALENDAR_TITLE_ID,
                Value = listRaw
            };

            PlayFabServerAPI.SetTitleInternalData(dataRequest, OnContainerUpdated, OnUpdateContainerFailed);
        }

        private void RemoveLastPosition()
        {
            CalendarInstance.RemoveLastPosition();
        }

        private void OnContainerUpdated(SetTitleDataResult result)
        {
            HideProgress();
            GetCalendarContainer();
        }

        private void OnUpdateContainerFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        private void ShowRewardDialog(RewardObject reward, bool includeCurrencies, Action<RewardObject> modifyCallback)
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
    }
}
#endif

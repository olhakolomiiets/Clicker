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
using CBS.Editor.Utils;
using CBS.SharedData.Lootbox;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ItemsConfigurator : BaseConfigurator
    {
        protected override string Title => "Items Configurator";

        private int SelectedToolBar { get; set; }

        public List<CatalogItem> Items { get; private set; }

        public List<CatalogItem> Packs { get; private set; }

        public List<CatalogItem> LootBoxes { get; private set; }

        public List<RandomResultTable> RandomItems { get; private set; }

        public Categories ItemsCategories { get; private set; }
        public CBSRecipeContainer Recipes { get; private set; }
        public CBSItemUpgradesContainer Upgrades { get; private set; }
        public CBSLootboxGeneralData CbsLootboxGeneralData { get; private set; }
        public CBSLootboxTable CbsLootboxTable { get; private set; }
        private Categories PacksCategories { get; set; }
        private Categories LootBoxesCategories { get; set; }
        private List<VirtualCurrencyData> VirtualCurrencies { get; set; }
        private Dictionary<string, bool> ExtendTable { get; set; }
        private Dictionary<string, int> SelectedCategoryTable { get; set; }
        private Dictionary<string, int> SelectedItemsIDsTable { get; set; }
        private Dictionary<string, int> SelectedCurrencyCodeTable { get; set; }
        private Dictionary<string, int> CurrencyToAddTable { get; set; }

        private GUILayoutOption[] AddButtonOptions { get; set; } = { GUILayout.Height(30), GUILayout.Width(140) };

        private bool ItemsInited { get; set; }

        private ItemsIcons Icons { get; set; }
        private CurrencyIcons CurrenciesIcons { get; set; }
        private LinkedPrefabData PrefabData { get; set; }
        private LinkedScriptableData ScriptableData { get; set; }

        private int[] CategoryIndex { get; set; } = new int[] { 0, 0, 0 };

        private string SelectedCategory { get; set; }

        protected override bool DrawScrollView => false;

        private Rect CategoriesRect = new Rect(0, 55, 150, 620);
        private Rect ItemsRect = new Rect(150, 115, 935, 600);
        private Rect LootboxRect = new Rect(150, 175, 935, 550);
        private Rect RendomItemRect = new Rect(0, 115, 1085, 600);
        private Vector2 TitleScroll { get; set; }

        private Vector2 ItemsScroll { get; set; }
        private Vector2 PacksScroll { get; set; }
        private Vector2 LootBoxScroll { get; set; }
        private Vector2 LootBoxCustomScroll { get; set; }
        private Vector2 RandomItemScroll { get; set; }

        private EditorData EditorData { get; set; }

        private Texture2D CurrencySlotBackgroundTexture;
        private Texture2D ItemSlotBackgroundTexture;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            Icons = CBSScriptable.Get<ItemsIcons>();
            CurrenciesIcons = CBSScriptable.Get<CurrencyIcons>();
            PrefabData = CBSScriptable.Get<LinkedPrefabData>();
            ScriptableData = CBSScriptable.Get<LinkedScriptableData>();
            EditorData = CBSScriptable.Get<EditorData>();
            ExtendTable = new Dictionary<string, bool>();
            SelectedCategoryTable = new Dictionary<string, int>();
            SelectedItemsIDsTable = new Dictionary<string, int>();
            SelectedCurrencyCodeTable = new Dictionary<string, int>();
            CurrencyToAddTable = new Dictionary<string, int>();
            AllConfigurator.Add(this);
            
            CurrencySlotBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.LootboxCurrencySlot);
            ItemSlotBackgroundTexture = EditorUtils.MakeColorTexture(EditorData.LootboxItemSlot);
        }

        protected override void OnDrawInside()
        {
            // draw sub titles
            SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, new string[] { "Items", "Packs", "Loot Boxes", "Randomize Items" });

            if (!ItemsInited)
            {
                InitConfigurator();
            }

            // check ready
            if (Items == null || ItemsCategories == null || VirtualCurrencies == null || RandomItems == null)
                return;

            switch (SelectedToolBar)
            {
                case 0:
                    DrawCategories(ItemsCategories, 0);
                    DrawItems();
                    break;
                case 1:
                    DrawCategories(PacksCategories, 1);
                    DrawPacks();
                    break;
                case 2:
                    DrawCategories(LootBoxesCategories, 2);
                    DrawLootboxes();
                    break;
                case 3:
                    DrawRandomizeItems();
                    break;
                default:
                    break;
            }
        }

        private void DrawCategories(Categories categories, int index)
        {
            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                var levelTitleStyle = new GUIStyle(GUI.skin.label);
                levelTitleStyle.fontStyle = FontStyle.Bold;
                levelTitleStyle.fontSize = 14;

                int catIndex = CategoryIndex[index];

                GUILayout.BeginVertical();

                GUILayout.Space(112);

                EditorGUILayout.LabelField("Categories", levelTitleStyle);

                int categoryHeight = 30;
                
                var categoriesMenu = categories.List.ToList();
                categoriesMenu.Remove(CBSConstants.UndefinedCategory);
                categoriesMenu.Insert(0, "All");
                var gridRect = new Rect(0, 142, 150, categoryHeight * categoriesMenu.Count);
                var scrollRect = gridRect;
                scrollRect.height += categoryHeight*4;
                scrollRect.width = 0;
                TitleScroll = GUI.BeginScrollView(CategoriesRect, TitleScroll, scrollRect);
                
                catIndex = GUI.SelectionGrid(gridRect, catIndex, categoriesMenu.ToArray(), 1);
                string selctedCategory = categoriesMenu[catIndex];

                SelectedCategory = selctedCategory == "All" ? string.Empty : selctedCategory;

                GUILayout.Space(30);
                var oldColor = GUI.color;
                GUI.backgroundColor = EditorData.AddColor;
                var style = new GUIStyle(GUI.skin.button);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;
                if (GUI.Button(new Rect(0, 170 + categoryHeight * categoriesMenu.Count, 150, categoryHeight), "Add category", style))
                {
                    ModifyCateroriesWindow.Show(onModify =>
                    {
                        SaveCategories(onModify);
                    }, categories);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
                
                GUI.EndScrollView();

                CategoryIndex[index] = catIndex;
            }
        }

        private void DrawItems()
        {
            // draw titles
            using (var areaScope = new GUILayout.AreaScope(ItemsRect))
            {
                GUILayout.BeginHorizontal();
                // title style
                GUILayout.Space(20);
                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleLeft;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 14;
                // draw titles
                GUILayout.Label("ID", titleStyle, GUILayout.Width(100));
                GUILayout.Label("Sprite", titleStyle, GUILayout.Width(118));
                GUILayout.Label("Name", titleStyle, GUILayout.Width(150));
                GUILayout.Label("Count", titleStyle, GUILayout.Width(100));
                GUILayout.Label("Category", titleStyle, GUILayout.Width(100));
                GUILayout.Label("Price", titleStyle, GUILayout.Width(200));

                // add new item

                if (EditorUtils.DrawButton("Add new item", EditorData.AddColor, 12, AddButtonOptions))
                {
                    var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                    AddItemWindow.Show<AddItemWindow>(new CatalogItem(), newItem =>
                    {
                        AddNewItem(newItem);
                    }, ItemAction.ADD, ItemsCategories.List, currenciesList, ItemType.ITEMS, CategoryIndex[0]);
                    GUIUtility.ExitGUI();
                }

                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.grey, 2, 20);

                ItemsScroll = GUILayout.BeginScrollView(ItemsScroll);

                float cellHeight = 100;

                for (int i = 0; i < Items.Count; i++)
                {
                    var item = Items[i];

                    bool isConsumable = item.Consumable.UsageCount != null;

                    bool tagExist = item.Tags != null && item.Tags.Count != 0;
                    var category = tagExist ? item.Tags[0] : CBSConstants.UndefinedCategory;

                    if (!string.IsNullOrEmpty(SelectedCategory))
                    {
                        if (category != SelectedCategory)
                        {
                            continue;
                        }
                    }

                    GUILayout.Space(10);
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(20);
                    // title style
                    var levelTitleStyle = new GUIStyle(GUI.skin.label);
                    levelTitleStyle.fontStyle = FontStyle.Bold;
                    // draw id
                    EditorGUILayout.LabelField(item.ItemId, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(cellHeight) });
                    // draw icon
                    var actvieSprite = Icons.GetSprite(item.ItemId);
                    var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                    GUILayout.Button(iconTexture, GUILayout.Width(cellHeight), GUILayout.Height(cellHeight));
                    // draw display name
                    GUILayout.Space(20);
                    EditorGUILayout.LabelField(item.DisplayName, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(cellHeight) });
                    // draw consumable option
                    string consumeOptions = isConsumable ? item.Consumable.UsageCount.ToString() : "Static";
                    EditorGUILayout.LabelField(consumeOptions, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(cellHeight) });
                    // draw caterory
                    EditorGUILayout.LabelField(category, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(cellHeight) });
                    // draw currencies
                    bool currenciesExist = item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0;
                    float currenciesHeight = currenciesExist ? cellHeight / item.VirtualCurrencyPrices.Count : cellHeight;
                    GUILayout.BeginVertical(GUILayout.Height(cellHeight));
                    if (currenciesExist)
                    {
                        GUILayout.FlexibleSpace();
                        var curList = item.VirtualCurrencyPrices;
                        foreach (var currency in curList)
                        {
                            GUILayout.BeginHorizontal(GUILayout.Width(200));
                            var currencyKey = currency.Key;
                            if (currencyKey == PlayfabUtils.REAL_MONEY_CODE)
                            {
                                var curTexture = ResourcesUtils.GetRealMoneyImage();
                                GUILayout.Button(curTexture, GUILayout.Width(25), GUILayout.Height(25));
                                EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                EditorGUILayout.LabelField(item.GetRMPriceString(), GUILayout.Width(100));
                            }
                            else
                            {
                                var curSprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(currency.Key);
                                var curTexture = curSprite == null ? null : curSprite.texture;
                                GUILayout.Button(curTexture, GUILayout.Width(25), GUILayout.Height(25));
                                EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                EditorGUILayout.LabelField(currency.Value.ToString(), GUILayout.Width(100));
                            }

                            GUILayout.EndHorizontal();
                        }
                        GUILayout.FlexibleSpace();
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No prices", GUILayout.Width(200), GUILayout.Height(cellHeight));
                    }

                    GUILayout.EndVertical();
                    GUILayout.BeginVertical(GUILayout.Height(cellHeight));
                    GUILayout.FlexibleSpace();
                    // draw edit button
                    if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, GUILayout.Width(75)))
                    {
                        var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                        AddItemWindow.Show<AddItemWindow>(item, newItem =>
                        {
                            AddNewItem(newItem);
                        }, ItemAction.EDIT, ItemsCategories.List, currenciesList, ItemType.ITEMS, CategoryIndex[0]);
                        GUIUtility.ExitGUI();
                    }
                    // draw remove button
                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, GUILayout.Width(75)))
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove this item?",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveItem(item);
                                break;
                        }
                    }
                    // draw duplicate
                    if (EditorUtils.DrawButton("Duplicate", EditorData.DuplicateColor, 12, GUILayout.Width(75)))
                    {
                        EditorInputDialog.Show("Duplicate item ?", "Please enter new id", item.GetNextID(), OnDuplicate =>
                        {
                            var newId = OnDuplicate;
                            var newItem = item.Duplicate(newId);
                            Recipes.Duplicate(item.ItemId, newItem.ItemId);
                            Upgrades.Duplicate(item.ItemId, newItem.ItemId);
                            AddNewItem(newItem);
                        });
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();

                    EditorUtils.DrawUILine(Color.grey, 1, 20);
                }

                GUILayout.Space(110);

                GUILayout.EndScrollView();
            }
        }

        private void DrawPacks()
        {
            GUILayout.Space(5);
            // tile style
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;


            using (var areaScope = new GUILayout.AreaScope(ItemsRect))
            {
                GUILayout.BeginHorizontal();

                // draw titles
                GUILayout.Label("Pack", titleStyle, GUILayout.Width(230));
                GUILayout.Label("Items", titleStyle, GUILayout.Width(60));
                GUILayout.Label("Currencies", titleStyle, GUILayout.Width(500));

                GUILayout.FlexibleSpace();
                // add new item
                if (EditorUtils.DrawButton("Add new pack", EditorData.AddColor, 12, AddButtonOptions))
                {
                    var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                    AddPackWindow.Show<AddPackWindow>(new CatalogItem(), newItem =>
                    {
                        AddNewItem(newItem);
                    }, ItemAction.ADD, PacksCategories.List, currenciesList, ItemType.PACKS, CategoryIndex[1]);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.grey, 2, 20);

                GUILayout.Space(15);

                PacksScroll = GUILayout.BeginScrollView(PacksScroll);

                for (int i = 0; i < Packs.Count; i++)
                {
                    var pack = Packs[i];

                    bool tagExist = pack.Tags != null && pack.Tags.Count != 0;
                    var category = tagExist ? pack.Tags[0] : CBSConstants.UndefinedCategory;

                    if (!string.IsNullOrEmpty(SelectedCategory))
                    {
                        if (category != SelectedCategory)
                        {
                            continue;
                        }
                    }

                    GUILayout.BeginHorizontal();

                    GUILayout.Space(20);

                    GUILayout.BeginVertical();
                    // draw display name
                    EditorGUILayout.LabelField(pack.DisplayName, titleStyle, new GUILayoutOption[] { GUILayout.Width(200) });
                    // draw icon
                    var actvieSprite = Icons.GetSprite(pack.ItemId);
                    var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                    GUILayout.Button(iconTexture, GUILayout.Width(200), GUILayout.Height(200));
                    GUILayout.EndVertical();

                    // draw items
                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    foreach (var item in pack.Bundle.BundledItems)
                    {
                        GUILayout.BeginHorizontal(GUILayout.Width(200));
                        GUILayout.Space(20);
                        var actvieItemSprite = Icons.GetSprite(item);
                        var itemTexture = actvieItemSprite == null ? null : actvieItemSprite.texture;
                        GUILayout.Button(itemTexture, GUILayout.Width(50), GUILayout.Height(50));
                        EditorGUILayout.LabelField(item);
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                    // draw currencies
                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    if (pack.Bundle?.BundledVirtualCurrencies != null)
                    {
                        foreach (var currency in pack.Bundle.BundledVirtualCurrencies)
                        {
                            GUILayout.BeginHorizontal(GUILayout.Width(200));
                            GUILayout.Space(20);
                            var actvieCurrencySprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(currency.Key);
                            var currencyTexture = actvieCurrencySprite == null ? null : actvieCurrencySprite.texture;
                            GUILayout.Button(currencyTexture, GUILayout.Width(50), GUILayout.Height(50));
                            EditorGUILayout.LabelField(currency.Key + " - " + currency.Value.ToString());
                            GUILayout.EndHorizontal();
                        }
                    }

                    GUILayout.EndVertical();


                    GUILayout.FlexibleSpace();

                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    // draw edit button
                    if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, GUILayout.Width(75)))
                    {
                        var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                        AddPackWindow.Show<AddPackWindow>(pack, newItem =>
                        {
                            AddNewItem(newItem);
                        }, ItemAction.EDIT, PacksCategories.List, currenciesList, ItemType.PACKS, CategoryIndex[1]);
                        GUIUtility.ExitGUI();
                    }
                    // draw remove button
                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, GUILayout.Width(75)))
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to this pack?",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveItem(pack);
                                break;
                        }
                    }
                    // draw duplicate
                    if (EditorUtils.DrawButton("Duplicate", EditorData.DuplicateColor, 12, GUILayout.Width(75)))
                    {
                        EditorInputDialog.Show("Duplicate item ?", "Please enter new id", pack.GetNextID(), OnDuplicate =>
                        {
                            var newId = OnDuplicate;
                            var newItem = pack.Duplicate(newId);
                            Recipes.Duplicate(pack.ItemId, newItem.ItemId);
                            Upgrades.Duplicate(pack.ItemId, newItem.ItemId);
                            AddNewItem(newItem);
                        });
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();

                    EditorUtils.DrawUILine(Color.grey, 1, 20);

                    GUILayout.Space(20);
                }

                GUILayout.Space(110);

                GUILayout.EndScrollView();
            }
        }

        private void DrawLootboxes()
        {
            GUILayout.Space(5);
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;
            GUILayout.BeginHorizontal();
            GUILayout.Space(160);
            GUILayout.BeginVertical();
            GUILayout.Label("Solution", titleStyle, GUILayout.Width(230));
            CbsLootboxGeneralData.Solution = (LootboxSolution)EditorGUILayout.EnumPopup(CbsLootboxGeneralData.Solution, GUILayout.Width(150));
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();

            if (CbsLootboxGeneralData.Solution == LootboxSolution.CBS_CUSTOM)
            {
                if (EditorUtils.DrawButton("Add new Lootbox", EditorData.AddColor, 12, AddButtonOptions))
                {
                    var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                    AddLootBoxWindow.Show<AddLootBoxWindow>(new CatalogItem(), newItem =>
                    {
                        AddNewItem(newItem);
                    }, ItemAction.ADD, LootBoxesCategories.List, currenciesList, ItemType.LOOT_BOXES, CategoryIndex[2]);
                    GUIUtility.ExitGUI();
                }
            }
            GUILayout.Space(2);
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
            {
                SaveLootboxData();
            }

            GUILayout.EndHorizontal();
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(150);
            EditorUtils.DrawUILine(Color.grey, 4, 20);
            GUILayout.EndHorizontal();
            
            if (CbsLootboxGeneralData.Solution == LootboxSolution.PLAYFAB)
            {
                DrawLootboxPlayfabSolution();
            }
            else
            {
                DrawLootboxCustomSolution();
            }
        }

        private void DrawLootboxCustomSolution()
        {
            using (var areaScope = new GUILayout.AreaScope(LootboxRect))
            {
                LootBoxCustomScroll = GUILayout.BeginScrollView(LootBoxCustomScroll);
                var lootEntityBoxStyle = new GUIStyle("HelpBox");
                //lootEntityBoxStyle.normal.background = LootBoxBackgroundTex;
                
                var contentTitle = new GUIStyle(GUI.skin.label);
                contentTitle.richText = true;

                for (int i = 0; i < LootBoxes.Count; i++)
                {
                    var item = LootBoxes[i];
                    bool tagExist = item.Tags != null && item.Tags.Count != 0;
                    var category = tagExist ? item.Tags[0] : CBSConstants.UndefinedCategory;

                    if (!string.IsNullOrEmpty(SelectedCategory))
                    {
                        if (category != SelectedCategory)
                        {
                            continue;
                        }
                    }
                    var lootEntity = CbsLootboxTable.GetDropEntityForItem(item.ItemId);
                    
                    GUILayout.Space(10);
                    using (var horizontalScope = new GUILayout.VerticalScope(lootEntityBoxStyle, GUILayout.Width(920)))
                    {
                        GUILayout.BeginHorizontal();
                        
                        var itemSprite = Icons.GetSprite(item.ItemId);
                        var itemIcon = itemSprite == null ? null : itemSprite.texture;
                        GUILayout.Button(itemIcon, GUILayout.Width(100), GUILayout.Height(100));
                        
                        GUILayout.BeginVertical();
                        EditorGUILayout.LabelField($"<color=orange><b>{item.ItemId}</b></color>", contentTitle);
                        EditorGUILayout.LabelField($"<color=teal>{item.DisplayName}</color>", contentTitle);
                        GUILayout.EndVertical();
                        
                        GUILayout.Space(20);
                        
                        GUILayout.BeginVertical();
                        EditorGUILayout.LabelField($"<color=lime>Locked by timer?</color>", contentTitle);
                        lootEntity.TimerLocked = EditorGUILayout.Toggle(lootEntity.TimerLocked);
                        if (lootEntity.TimerLocked)
                        {
                            EditorGUILayout.LabelField("Timer in seconds", contentTitle);
                            lootEntity.LockSeconds = EditorGUILayout.IntField(lootEntity.LockSeconds);
                        }
                        GUILayout.EndVertical();
                        
                        GUILayout.FlexibleSpace();
                        
                        GUILayout.BeginVertical(GUILayout.Height(120));
                        //GUILayout.FlexibleSpace();

                        if (EditorUtils.DrawButton("Edit info", EditorData.EditColor, 12, GUILayout.Width(120), GUILayout.Height(30)))
                        {
                            var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                            AddLootBoxWindow.Show<AddLootBoxWindow>(item, newItem =>
                            {
                                AddNewItem(newItem);
                            }, ItemAction.EDIT, LootBoxesCategories.List, currenciesList, ItemType.LOOT_BOXES, CategoryIndex[2]);
                            GUIUtility.ExitGUI();
                        }
                        if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, GUILayout.Width(120), GUILayout.Height(30)))
                        {
                            int option = EditorUtility.DisplayDialogComplex("Warning",
                                "Are you sure you want to remove this pack?",
                                "Yes",
                                "No",
                                string.Empty);
                            switch (option)
                            {
                                // ok.
                                case 0:
                                    RemoveItem(item);
                                    break;
                            }
                        }
                        var extended = ExtendTable.TryGetValue(item.ItemId, out var value) && value;
                        var extendButtonTitle = extended ? "Hide" : "Edit drop table";
                        if (EditorUtils.DrawButton(extendButtonTitle, EditorData.AddColor, 12, GUILayout.Width(120), GUILayout.Height(30)))
                        {
                            ExtendTable[item.ItemId] = !extended;
                            extended = ExtendTable[item.ItemId];
                        }
                        GUILayout.EndVertical();
                        
                        GUILayout.EndHorizontal();

                        if (extended)
                        {
                            GUILayout.Space(10);
                            
                            GUILayout.BeginHorizontal();
                            
                            if (GUILayout.Button("Add item slot", GUILayout.Width(150)))
                            {
                                var itemSlot = new DropSlot
                                {
                                    SlotDropType = SlotDropType.ITEM
                                };
                                lootEntity.AddSlot(itemSlot);
                            }
                            
                            if (GUILayout.Button("Add currency slot", GUILayout.Width(150)))
                            {
                                var currencySlot = new DropSlot
                                {
                                    SlotDropType = SlotDropType.CURRENCY
                                };
                                lootEntity.AddSlot(currencySlot);
                            }
                            
                            GUILayout.EndHorizontal();

                            DrawDropSlots(lootEntity);
                        }
                    }
                }
                GUILayout.Space(150);
                GUILayout.EndScrollView();
            }
        }
        
        private void DrawDropSlots(LootBoxEntity entity)
        {
            var itemID = entity.ItemID;
            var slots = entity.Slots;
            if (slots == null)
            {
                slots = new List<DropSlot>();
                entity.Slots = slots;
            }
            var dropSlotBoxStyle = new GUIStyle("HelpBox");

            var contentTitle = new GUIStyle(GUI.skin.label);
            contentTitle.richText = true;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var slotType = slot.SlotDropType;

                var slotTexture = slotType == SlotDropType.ITEM
                    ? ItemSlotBackgroundTexture
                    : CurrencySlotBackgroundTexture;
                dropSlotBoxStyle.normal.background = slotTexture;
                
                if (slotType == SlotDropType.ITEM)
                {
                    GUILayout.Space(10);
                    using (var horizontalScope = new GUILayout.VerticalScope(dropSlotBoxStyle, GUILayout.Width(900)))
                    {
                        GUILayout.BeginHorizontal(GUILayout.Width(900));
                        
                        GUILayout.BeginVertical(GUILayout.Width(150));
                        EditorGUILayout.LabelField($"<color=lime>Drop Behavior</color>", contentTitle);
                        slot.DropBehavior = (DropBehavior)EditorGUILayout.EnumPopup(slot.DropBehavior, GUILayout.Width(150));

                        if (slot.DropBehavior == DropBehavior.BY_CHANGE)
                        {
                            GUILayout.Space(5);
                            EditorGUILayout.LabelField($"<color=lime>Drop Chanсe</color>", contentTitle, GUILayout.Width(150));
                            GUILayout.BeginHorizontal();
                            slot.DropChance = EditorGUILayout.Slider(slot.DropChance, 0, 100, GUILayout.Width(150));
                            EditorGUILayout.LabelField($"%", contentTitle, GUILayout.Width(20));
                            GUILayout.EndHorizontal();
                        }
                        
                        GUILayout.Space(5);
                        if (GUILayout.Button("Remove slot", GUILayout.Width(150)))
                        {
                            entity.RemoveSlot(slot);
                        }
                        
                        GUILayout.EndVertical();

                        GUILayout.FlexibleSpace();

                        GUILayout.BeginVertical();
                        EditorGUILayout.LabelField($"<color=lime>Category</color>", contentTitle);
                        var slotID = itemID + i;
                        var lastCachedCategoryIndex =
                            SelectedCategoryTable.TryGetValue(slotID, out var categoryIndex) ? categoryIndex : -1;
                        var categoryList = ItemsCategories.List ?? new List<string>();
                        int selectedCategory = EditorGUILayout.Popup(lastCachedCategoryIndex == -1 ? 0 : lastCachedCategoryIndex, categoryList.ToArray());
                        if (lastCachedCategoryIndex != selectedCategory)
                        {
                            SelectedItemsIDsTable[slotID] = 0;
                        }
                        GUILayout.EndVertical();
                        
                        GUILayout.BeginVertical();
                        EditorGUILayout.LabelField($"<color=lime>Item</color>", contentTitle);
                        var itemsForCategory = Items
                            .Where(x => x.Tags.Any() && x.Tags[0] == categoryList[selectedCategory]);
                        var itemsIDs = itemsForCategory.Select(x => x.ItemId).ToArray();
                        SelectedCategoryTable[slotID] = selectedCategory;
                        var selectedItemIndex = SelectedItemsIDsTable.TryGetValue(slotID, out var itemIndex) ? itemIndex : -1;
                        selectedItemIndex = EditorGUILayout.Popup(selectedItemIndex, itemsIDs);
                        SelectedItemsIDsTable[slotID] = selectedItemIndex;
                        var selectedItemID = itemsIDs.Any() && selectedItemIndex >= 0 && selectedItemIndex < itemsIDs.Length ? itemsIDs[selectedItemIndex] : String.Empty;

                        if (!string.IsNullOrEmpty(selectedItemID))
                        {
                            GUILayout.BeginHorizontal();
                            var itemSprite = Icons.GetSprite(selectedItemID);
                            var itemIcon = itemSprite == null ? null : itemSprite.texture;
                            GUILayout.Button(itemIcon, GUILayout.Width(50), GUILayout.Height(50));

                            if (GUILayout.Button("Add item", GUILayout.Width(150), GUILayout.Height(50)))
                            {
                                slot.AddItem(new DropItem
                                {
                                    ItemID = selectedItemID,
                                    Weight = 1
                                });
                            }
                            
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.EndVertical();

                        GUILayout.EndHorizontal();

                        if (slot.ItemsToDrop == null)
                            slot.ItemsToDrop = new List<DropItem>();
                        var itemsToDrop = slot.ItemsToDrop;

                        var allWeight = itemsToDrop.Sum(x => x.Weight);

                        var rowCount = 3;
                        var splittedArray = CollectionUtils.SplitArray(itemsToDrop.ToArray(), rowCount);
                        
                        GUILayout.Space(30);
                        GUILayout.BeginVertical();
                        
                        for (int j = 0; j < splittedArray.Count(); j++)
                        {
                            var rowPart = splittedArray.ElementAt(j);
                            GUILayout.BeginHorizontal();
                            for (int k = 0; k < rowPart.Count(); k++)
                            {
                                var dropItem = rowPart.ElementAt(k);
                                var dropChance = (float)dropItem.Weight / (float)allWeight * 100f;
                                var itemSprite = Icons.GetSprite(dropItem.ItemID);
                                var itemIcon = itemSprite == null ? null : itemSprite.texture;
                                GUILayout.BeginHorizontal(GUILayout.Width(150));
                                GUILayout.Button(itemIcon, GUILayout.Width(100), GUILayout.Height(100));
                                GUILayout.BeginVertical();
                                EditorGUILayout.LabelField($"<color=orange><b>{dropItem.ItemID}</b></color>", contentTitle, GUILayout.Width(150));
                                EditorGUILayout.LabelField($"<color=lime>Weight</color>", contentTitle, GUILayout.Width(120));
                                dropItem.Weight = EditorGUILayout.IntSlider(dropItem.Weight, 1, 1000, GUILayout.Width(150));
                                EditorGUILayout.LabelField($"<color=lime>Drop Chance: </color><color=yellow><b>{dropChance.ToString("F2")} %</b></color>", contentTitle, GUILayout.Width(150));
                                if (GUILayout.Button("Remove",GUILayout.Width(150)))
                                {
                                    slot.RemoveItem(dropItem);
                                }
                                GUILayout.EndVertical();
                                GUILayout.EndHorizontal();
                                GUILayout.Space(50);
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(10);
                        }
                        
                        GUILayout.EndVertical();
                    }
                }
                else
                {
                    GUILayout.Space(10);
                    using (var horizontalScope = new GUILayout.VerticalScope(dropSlotBoxStyle, GUILayout.Width(910)))
                    {
                        GUILayout.BeginHorizontal(GUILayout.Width(910));

                        GUILayout.BeginVertical(GUILayout.Width(150));
                        EditorGUILayout.LabelField($"<color=lime>Drop Behavior</color>", contentTitle);
                        slot.DropBehavior =
                            (DropBehavior)EditorGUILayout.EnumPopup(slot.DropBehavior, GUILayout.Width(150));

                        if (slot.DropBehavior == DropBehavior.BY_CHANGE)
                        {
                            GUILayout.Space(5);
                            EditorGUILayout.LabelField($"<color=lime>Drop Chanсe</color>", contentTitle, GUILayout.Width(150));
                            GUILayout.BeginHorizontal();
                            slot.DropChance = EditorGUILayout.Slider(slot.DropChance, 0, 100, GUILayout.Width(150));
                            EditorGUILayout.LabelField($"%", contentTitle, GUILayout.Width(20));
                            GUILayout.EndHorizontal();
                        }
                        
                        GUILayout.Space(5);
                        if (GUILayout.Button("Remove slot", GUILayout.Width(150)))
                        {
                            entity.RemoveSlot(slot);
                        }
                        GUILayout.EndVertical();

                        GUILayout.FlexibleSpace();

                        GUILayout.BeginVertical();
                        var slotID = itemID + i;
                        EditorGUILayout.LabelField($"<color=lime>Currency</color>", contentTitle);
                        var selectedItemIndex = SelectedCurrencyCodeTable.TryGetValue(slotID, out var itemIndex)
                            ? itemIndex
                            : -1;
                        var currencies = VirtualCurrencies.Select(x => x.CurrencyCode).ToArray();
                        selectedItemIndex = EditorGUILayout.Popup(selectedItemIndex, currencies.ToArray());
                        SelectedCurrencyCodeTable[slotID] = selectedItemIndex;
                        var selectedCurrencyCode =
                            currencies.Any() && selectedItemIndex >= 0 && selectedItemIndex < currencies.Length
                                ? currencies[selectedItemIndex]
                                : String.Empty;

                        if (!string.IsNullOrEmpty(selectedCurrencyCode))
                        {
                            GUILayout.BeginHorizontal();
                            var currencySprite = CurrenciesIcons.GetSprite(selectedCurrencyCode);
                            var currencyIcon = currencySprite == null ? null : currencySprite.texture;
                            GUILayout.Button(currencyIcon, GUILayout.Width(50), GUILayout.Height(50));

                            if (GUILayout.Button("Add Currency", GUILayout.Width(150), GUILayout.Height(50)))
                            {
                                slot.AddCurrency(new DropCurrency()
                                {
                                    CurrencyCode = selectedCurrencyCode
                                });
                            }

                            GUILayout.EndHorizontal();
                        }

                        GUILayout.EndVertical();

                        GUILayout.EndHorizontal();
                        
                        if (slot.CurrenciesToDrop == null)
                            slot.CurrenciesToDrop = new List<DropCurrency>();
                        var currenciesToDrop = slot.CurrenciesToDrop;
                        
                        GUILayout.Space(20);

                        for (int c = 0; c < currenciesToDrop.Count; c++)
                        {
                            GUILayout.Space(10);
                            var dropCurrency = currenciesToDrop[c];
                            var currencyCode = dropCurrency.CurrencyCode;
                            
                            GUILayout.BeginHorizontal();
                            var currencySprite = CurrenciesIcons.GetSprite(currencyCode);
                            var currencyIcon = currencySprite == null ? null : currencySprite.texture;
                            GUILayout.Button(currencyIcon, GUILayout.Width(100), GUILayout.Height(100));
                            GUILayout.BeginVertical();
                            GUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField($"<color=orange><b>{currencyCode}</b></color>", contentTitle, GUILayout.Width(50));
                            if (GUILayout.Button("Remove", GUILayout.Width(150)))
                            {
                                slot.RemoveCurrency(dropCurrency);
                            }
                            GUILayout.EndHorizontal();
                            EditorGUILayout.LabelField($"<color=lime>Drop Behavior</color>", contentTitle);
                            dropCurrency.DropType = (CurrencyDropType)EditorGUILayout.EnumPopup(dropCurrency.DropType, GUILayout.Width(200));
                            if (dropCurrency.DropType == CurrencyDropType.MIN_MAX_VALUE)
                            {
                                EditorGUILayout.LabelField($"<color=lime>Value</color>", contentTitle);
                                GUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Min", GUILayout.Width(30));
                                dropCurrency.Min =
                                    EditorGUILayout.IntField(dropCurrency.Min, GUILayout.Width(200));
                                EditorGUILayout.LabelField("Max", GUILayout.Width(30));
                                dropCurrency.Max =
                                    EditorGUILayout.IntField( dropCurrency.Max, GUILayout.Width(200));
                                GUILayout.EndHorizontal();
                            }
                            else
                            {
                                var table = dropCurrency.DropTable;
                                EditorGUILayout.LabelField($"<color=lime>Value</color>", contentTitle);
                                var currencySlotID = slotID + c.ToString();
                                var currencyInput = CurrencyToAddTable.TryGetValue(currencySlotID, out var currency)
                                    ? currency
                                    : 1;
                                GUILayout.BeginHorizontal();
                                CurrencyToAddTable[currencySlotID] = EditorGUILayout.IntField(currencyInput, GUILayout.Width(175));
                                if (GUILayout.Button("+", GUILayout.Width(25)))
                                {
                                    var valueToAdd = CurrencyToAddTable[currencySlotID];
                                    dropCurrency.AddToTable(valueToAdd);
                                    CurrencyToAddTable[currencySlotID] = 1;
                                }
                                GUILayout.EndHorizontal();

                                GUILayout.Space(5);
                                
                                if (table != null)
                                {
                                    var allWeight = table.Sum(x => x.Weight);
                                    for (int j = 0; j < table.Count; j++)
                                    {
                                        GUILayout.Space(5);
                                        var tablePair = table[j];
                                        var dropChance = (float)tablePair.Weight / (float)allWeight * 100f;
                                        GUILayout.BeginHorizontal();
                                        EditorGUILayout.LabelField($"<color=orange><b>{tablePair.Amount}</b></color>", contentTitle, GUILayout.Width(175));
                                        if (GUILayout.Button("-", GUILayout.Width(25)))
                                        {
                                            dropCurrency.RemoveFromTable(tablePair.Amount);
                                            GUILayout.EndHorizontal();
                                            continue;
                                        }
                                        GUILayout.EndHorizontal();
                                        EditorGUILayout.LabelField($"<color=lime>Weight</color>", contentTitle);
                                        tablePair.Weight = EditorGUILayout.IntSlider(tablePair.Weight, 1, 1000, GUILayout.Width(250));
                                        EditorGUILayout.LabelField($"<color=lime>Drop Chance: </color><color=yellow><b>{dropChance.ToString("F2")} %</b></color>", contentTitle);
                                        GUILayout.Space(5);
                                    }
                                }
                            }
                            GUILayout.EndVertical();
                            GUILayout.EndHorizontal();
                        }
                    }
                }
                
            }
        }

        private void DrawLootboxPlayfabSolution()
        {
            GUILayout.Space(5);
            // tile style
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;


            using (var areaScope = new GUILayout.AreaScope(LootboxRect))
            {
                GUILayout.BeginHorizontal();

                // draw titles
                GUILayout.Label("Lootbox", titleStyle, GUILayout.Width(230));
                GUILayout.Label("Items", titleStyle, GUILayout.Width(60));
                GUILayout.Label("Currencies", titleStyle, GUILayout.Width(500));

                GUILayout.FlexibleSpace();
                // add new item
                if (EditorUtils.DrawButton("Add new Lootbox", EditorData.AddColor, 12, AddButtonOptions))
                {
                    var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                    AddLootBoxWindow.Show<AddLootBoxWindow>(new CatalogItem(), newItem =>
                    {
                        AddNewItem(newItem);
                    }, ItemAction.ADD, LootBoxesCategories.List, currenciesList, ItemType.LOOT_BOXES, CategoryIndex[2]);
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.grey, 2, 20);

                GUILayout.Space(15);

                LootBoxScroll = GUILayout.BeginScrollView(LootBoxScroll);

                for (int i = 0; i < LootBoxes.Count; i++)
                {
                    var box = LootBoxes[i];

                    bool tagExist = box.Tags != null && box.Tags.Count != 0;
                    var category = tagExist ? box.Tags[0] : CBSConstants.UndefinedCategory;

                    if (!string.IsNullOrEmpty(SelectedCategory))
                    {
                        if (category != SelectedCategory)
                        {
                            continue;
                        }
                    }

                    GUILayout.BeginHorizontal();

                    GUILayout.Space(20);

                    GUILayout.BeginVertical();
                    // draw display name
                    EditorGUILayout.LabelField(box.DisplayName, titleStyle, new GUILayoutOption[] { GUILayout.Width(200) });
                    // draw icon
                    var actvieSprite = Icons.GetSprite(box.ItemId);
                    var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                    GUILayout.Button(iconTexture, GUILayout.Width(200), GUILayout.Height(200));
                    GUILayout.EndVertical();

                    // draw items
                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    foreach (var item in box.Container.ResultTableContents)
                    {
                        GUILayout.BeginHorizontal(GUILayout.Width(200));
                        GUILayout.Space(20);
                        var actvieItemSprite = Icons.GetSprite(item);
                        var itemTexture = actvieItemSprite == null ? null : actvieItemSprite.texture;
                        GUILayout.Button(itemTexture, GUILayout.Width(50), GUILayout.Height(50));
                        EditorGUILayout.LabelField(item);
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                    // draw currencies
                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    if (box.Container?.VirtualCurrencyContents != null)
                    {
                        foreach (var currency in box.Container.VirtualCurrencyContents)
                        {
                            GUILayout.BeginHorizontal(GUILayout.Width(200));
                            GUILayout.Space(20);
                            var actvieCurrencySprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(currency.Key);
                            var currencyTexture = actvieCurrencySprite == null ? null : actvieCurrencySprite.texture;
                            GUILayout.Button(currencyTexture, GUILayout.Width(50), GUILayout.Height(50));
                            EditorGUILayout.LabelField(currency.Key + " - " + currency.Value.ToString());
                            GUILayout.EndHorizontal();
                        }
                    }

                    GUILayout.EndVertical();

                    GUILayout.FlexibleSpace();

                    GUILayout.BeginVertical();
                    GUILayout.Space(20);
                    // draw edit button
                    if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, GUILayout.Width(75)))
                    {
                        var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                        AddLootBoxWindow.Show<AddLootBoxWindow>(box, newItem =>
                        {
                            AddNewItem(newItem);
                        }, ItemAction.EDIT, LootBoxesCategories.List, currenciesList, ItemType.LOOT_BOXES, CategoryIndex[2]);
                        GUIUtility.ExitGUI();
                    }
                    // draw remove button
                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, GUILayout.Width(75)))
                    {
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove this pack?",
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveItem(box);
                                break;
                        }
                    }
                    // draw dublicate
                    if (EditorUtils.DrawButton("Duplicate", EditorData.DuplicateColor, 12, GUILayout.Width(75)))
                    {
                        EditorInputDialog.Show("Duplicate item ?", "Please enter new id", box.GetNextID(), OnDuplicate =>
                        {
                            var newId = OnDuplicate;
                            var newItem = box.Duplicate(newId);
                            Recipes.Duplicate(box.ItemId, newItem.ItemId);
                            Upgrades.Duplicate(box.ItemId, newItem.ItemId);
                            AddNewItem(newItem);
                        });
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();

                    EditorUtils.DrawUILine(Color.grey, 1, 20);

                    GUILayout.Space(20);
                }

                GUILayout.Space(150);

                GUILayout.EndScrollView();
            }
        }

        private void DrawRandomizeItems()
        {
            GUILayout.Space(5);
            // tile style
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;


            using (var areaScope = new GUILayout.AreaScope(RendomItemRect))
            {
                GUILayout.BeginHorizontal();

                // draw titles
                GUILayout.Label("Random Item ID", titleStyle, GUILayout.Width(500));
                GUILayout.Label("Content", titleStyle, GUILayout.Width(60));

                GUILayout.FlexibleSpace();
                // add new item
                if (EditorUtils.DrawButton("Add new random item", EditorData.AddColor, 12, GUILayout.Width(150), GUILayout.Height(30)))
                {
                    var currenciesList = VirtualCurrencies.Select(x => x.CurrencyCode).ToList();
                    AddRandomItemWindow.Show(newItem =>
                    {
                        if (RandomItems != null)
                        {
                            RandomItems.Add(newItem);
                            SaveRandomItems(RandomItems);
                        }
                    }, new RandomResultTable());
                    GUIUtility.ExitGUI();
                }
                GUILayout.EndHorizontal();

                EditorUtils.DrawUILine(Color.grey, 2, 20);

                GUILayout.Space(15);

                RandomItemScroll = GUILayout.BeginScrollView(RandomItemScroll);

                float rowWith = 900;
                int rowCount = 9;

                for (int i = 0; i < RandomItems.Count; i++)
                {
                    GUILayout.BeginHorizontal(GUILayout.Width(rowWith + 150));

                    var randomItem = RandomItems[i];
                    string itemID = randomItem.TableId;

                    GUILayout.BeginVertical();
                    // draw display name
                    EditorGUILayout.LabelField(itemID, new GUILayoutOption[] { GUILayout.Width(100) });
                    // draw icon
                    var actvieSprite = Icons.GetSprite(itemID);
                    var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                    GUILayout.Button(iconTexture, GUILayout.Width(100), GUILayout.Height(100));
                    GUILayout.EndVertical();

                    GUILayout.Space(10);

                    int allWeight = randomItem.Nodes.Select(x => x.Weight).Sum();

                    // draw nodes
                    for (int j = 0; j < randomItem.Nodes.Count; j++)
                    {
                        if (j % rowCount == 0 && j != 0)
                        {
                            if (j <= rowCount)
                            {
                                DrawRandomEditRemove(randomItem);
                            }

                            GUILayout.EndHorizontal();

                            GUILayout.BeginHorizontal(GUILayout.Width(rowWith));

                            if (i != rowCount)
                            {
                                GUILayout.Space(115);
                            }
                        }

                        GUILayout.Space(10);

                        GUILayout.BeginVertical();

                        GUILayout.Space(22);

                        var node = randomItem.Nodes[j];
                        string nodeId = node.ResultItem;
                        int weight = node.Weight;

                        float persent = (float)weight / (float)allWeight;

                        var nodeSprite = Icons.GetSprite(nodeId);
                        var nodeTexture = nodeSprite == null ? null : nodeSprite.texture;
                        GUILayout.Button(nodeTexture, GUILayout.Width(75), GUILayout.Height(75));

                        float lastY = GUILayoutUtility.GetLastRect().y;
                        float lastX = GUILayoutUtility.GetLastRect().x;

                        string progressTitle = (persent * 100).ToString("0.00") + "%";

                        EditorGUI.ProgressBar(new Rect(lastX, lastY + 80, 75, 20), persent, progressTitle);

                        GUILayout.Space(25);

                        GUILayout.EndVertical();
                    }

                    GUILayout.FlexibleSpace();

                    if (randomItem.Nodes.Count <= rowCount)
                    {
                        //GUILayout.Space(100);
                        DrawRandomEditRemove(randomItem);
                    }

                    GUILayout.EndHorizontal();

                    EditorUtils.DrawUILine(Color.grey, 1, 20);

                    GUILayout.Space(30);
                }


                GUILayout.Space(110);

                GUILayout.EndScrollView();
            }
        }

        private void DrawRandomEditRemove(RandomResultTable randomItem)
        {
            GUILayout.FlexibleSpace();

            GUILayout.BeginVertical();
            GUILayout.Space(20);
            // draw edit button
            if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, GUILayout.Width(75)))
            {
                AddRandomItemWindow.Show(newItem =>
                {
                    if (RandomItems != null)
                    {
                        SaveRandomItems(RandomItems);
                    }
                }, randomItem);
                GUIUtility.ExitGUI();
            }
            // draw remove button
            if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, GUILayout.Width(75)))
            {
                int option = EditorUtility.DisplayDialogComplex("Warning",
                    "Are you sure you want to remove this item?",
                    "Yes",
                    "No",
                    string.Empty);
                switch (option)
                {
                    // ok.
                    case 0:
                        if (RandomItems.Contains(randomItem))
                        {
                            RandomItems.Remove(randomItem);
                            RandomItems.TrimExcess();
                        }
                        SaveRandomItems(RandomItems);
                        break;
                }
            }
            GUILayout.EndVertical();
        }

        private void InitConfigurator()
        {
            // get categories
            GetTitleData(categotyResult =>
            {
                OnGetTitleData(categotyResult);
                // get items
                GetItemsCatalog(itemsResult =>
                {
                    OnItemsCatalogGetted(itemsResult);
                    // currencies
                    GetAllCurrencies(currenciesResult =>
                    {
                        OnCurrenciesListGetted(currenciesResult);
                        // random items
                        GetRandomItems(randomItemsResult =>
                        {
                            OnGetRandomTables(randomItemsResult);
                        });
                    });
                });
            });

            ItemsInited = true;
        }

        // fab methods

        // get random items
        private void GetRandomItems(Action<GetRandomResultTablesResult> finish = null)
        {
            ShowProgress();

            var dataRequest = new GetRandomResultTablesRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID
            };

            var succesCallback = finish == null ? OnGetRandomTables : finish;

            PlayFabAdminAPI.GetRandomResultTables(dataRequest, succesCallback, OnGetRandomTablesFailed);
        }

        private void OnGetRandomTables(GetRandomResultTablesResult result)
        {
            RandomItems = new List<RandomResultTable>();

            foreach (var table in result.Tables)
            {
                RandomItems.Add(new RandomResultTable
                {
                    TableId = table.Value.TableId,
                    Nodes = table.Value.Nodes
                });
            }
            HideProgress();
        }

        private void OnGetRandomTablesFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // save random items
        private void SaveRandomItems(List<RandomResultTable> newList)
        {
            ShowProgress();

            var dataRequest = new UpdateRandomResultTablesRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                Tables = newList
            };

            PlayFabAdminAPI.UpdateRandomResultTables(dataRequest, OnRandomTablesUpdated, OnUpdateRandomTablesFailed);
        }

        private void OnRandomTablesUpdated(UpdateRandomResultTablesResult result)
        {
            HideProgress();
            GetRandomItems();
        }

        private void OnUpdateRandomTablesFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // get categories
        public void GetTitleData(Action<GetTitleDataResult> finish = null)
        {
            ShowProgress();

            var dataRequest = new GetTitleDataRequest
            {
                Keys = new List<string> {
                    TitleKeys.ItemsCategoriesKey,
                    TitleKeys.PackCategoriesKey,
                    TitleKeys.LootboxesCategoriesKey,
                    TitleKeys.ItemsRecipeKey,
                    TitleKeys.ItemsUpgradeKey,
                    TitleKeys.LootboxGeneralDataKey,
                    TitleKeys.LootboxTableKey
                }
            };

            var succesCallback = finish == null ? OnGetTitleData : finish;

            PlayFabAdminAPI.GetTitleInternalData(dataRequest, succesCallback, OnGetTitleDataFailed);
        }

        private void OnGetTitleData(GetTitleDataResult result)
        {
            // items categories
            if (result.Data.ContainsKey(TitleKeys.ItemsCategoriesKey))
            {
                var rawData = result.Data[TitleKeys.ItemsCategoriesKey];
                ItemsCategories = JsonUtility.FromJson<Categories>(rawData);
                if (!ItemsCategories.List.Contains(CBSConstants.UndefinedCategory))
                {
                    ItemsCategories.List.Insert(0, CBSConstants.UndefinedCategory);
                }
                ItemsCategories.TitleKey = TitleKeys.ItemsCategoriesKey;
            }
            else
            {
                ItemsCategories = new Categories();
                ItemsCategories.List = new List<string>();
                ItemsCategories.List.Add(CBSConstants.UndefinedCategory);
                ItemsCategories.TitleKey = TitleKeys.ItemsCategoriesKey;
            }
            // packs categories
            if (result.Data.ContainsKey(TitleKeys.PackCategoriesKey))
            {
                var rawData = result.Data[TitleKeys.PackCategoriesKey];
                PacksCategories = JsonUtility.FromJson<Categories>(rawData);
                if (!PacksCategories.List.Contains(CBSConstants.UndefinedCategory))
                {
                    PacksCategories.List.Insert(0, CBSConstants.UndefinedCategory);
                }
                PacksCategories.TitleKey = TitleKeys.PackCategoriesKey;
            }
            else
            {
                PacksCategories = new Categories();
                PacksCategories.List = new List<string>();
                PacksCategories.List.Add(CBSConstants.UndefinedCategory);
                PacksCategories.TitleKey = TitleKeys.PackCategoriesKey;
            }
            // loot boxes categories
            if (result.Data.ContainsKey(TitleKeys.LootboxesCategoriesKey))
            {
                var rawData = result.Data[TitleKeys.LootboxesCategoriesKey];
                LootBoxesCategories = JsonUtility.FromJson<Categories>(rawData);
                if (!LootBoxesCategories.List.Contains(CBSConstants.UndefinedCategory))
                {
                    LootBoxesCategories.List.Insert(0, CBSConstants.UndefinedCategory);
                }
                LootBoxesCategories.TitleKey = TitleKeys.LootboxesCategoriesKey;
            }
            else
            {
                LootBoxesCategories = new Categories();
                LootBoxesCategories.List = new List<string>();
                LootBoxesCategories.List.Add(CBSConstants.UndefinedCategory);
                LootBoxesCategories.TitleKey = TitleKeys.LootboxesCategoriesKey;
            }
            // get upgrade/recipe data
            if (result.Data.ContainsKey(TitleKeys.ItemsRecipeKey))
            {
                var rawData = result.Data[TitleKeys.ItemsRecipeKey];
                try
                {
                    Recipes = JsonPlugin.FromJsonDecompress<CBSRecipeContainer>(rawData);
                }
                catch
                {
                    Recipes = JsonPlugin.FromJson<CBSRecipeContainer>(rawData);
                }
            }
            else
            {
                Recipes = new CBSRecipeContainer();
            }
            // get items upgrades data
            if (result.Data.ContainsKey(TitleKeys.ItemsUpgradeKey))
            {
                var rawData = result.Data[TitleKeys.ItemsUpgradeKey];
                try
                {
                    Upgrades = JsonPlugin.FromJsonDecompress<CBSItemUpgradesContainer>(rawData);
                }
                catch
                {
                    Upgrades = JsonPlugin.FromJson<CBSItemUpgradesContainer>(rawData);
                }
            }
            else
            {
                Upgrades = new CBSItemUpgradesContainer();
            }
            // get loot box general data
            if (result.Data.ContainsKey(TitleKeys.LootboxGeneralDataKey))
            {
                var rawData = result.Data[TitleKeys.LootboxGeneralDataKey];
                try
                {
                    CbsLootboxGeneralData = JsonPlugin.FromJsonDecompress<CBSLootboxGeneralData>(rawData);
                }
                catch
                {
                    CbsLootboxGeneralData = JsonPlugin.FromJson<CBSLootboxGeneralData>(rawData);
                }
            }
            else
            {
                CbsLootboxGeneralData = new CBSLootboxGeneralData();
            }
            // get loot box table data
            if (result.Data.ContainsKey(TitleKeys.LootboxTableKey))
            {
                var rawData = result.Data[TitleKeys.LootboxTableKey];
                try
                {
                    CbsLootboxTable = JsonPlugin.FromJsonDecompress<CBSLootboxTable>(rawData);
                }
                catch
                {
                    CbsLootboxTable = JsonPlugin.FromJson<CBSLootboxTable>(rawData);
                }
            }
            else
            {
                CbsLootboxTable = new CBSLootboxTable();
            }
            HideProgress();
        }

        private void OnGetTitleDataFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // save lootbox
        private void SaveLootboxData()
        {
            ShowProgress();

            string rawData = JsonPlugin.ToJson(CbsLootboxGeneralData);

            var dataRequest = new SetTitleDataRequest
            {
                Key = TitleKeys.LootboxGeneralDataKey,
                Value = rawData
            };

            PlayFabAdminAPI.SetTitleInternalData(dataRequest, SaveLootboxTable, OnSaveLootoboxDataFailed);
        }
        
        private void SaveLootboxTable(SetTitleDataResult result)
        {
            ShowProgress();

            string rawData = JsonPlugin.ToJson(CbsLootboxTable);

            var dataRequest = new SetTitleDataRequest
            {
                Key = TitleKeys.LootboxTableKey,
                Value = rawData
            };

            PlayFabAdminAPI.SetTitleInternalData(dataRequest, OnLootboxDataSaved, OnSaveLootoboxDataFailed);
        }
        
        private void OnLootboxDataSaved(SetTitleDataResult result)
        {
            HideProgress();
            GetTitleData();
        }

        private void OnSaveLootoboxDataFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // save category
        private void SaveCategories(Categories categories)
        {
            ShowProgress();

            var list = categories.List.ToList();
            if (list.Contains(CBSConstants.UndefinedCategory))
            {
                list.Remove(CBSConstants.UndefinedCategory);
            }

            categories.List = list;

            string rawData = JsonPlugin.ToJson(categories);

            var dataRequest = new SetTitleDataRequest
            {
                Key = categories.TitleKey,
                Value = rawData
            };

            PlayFabAdminAPI.SetTitleInternalData(dataRequest, OnCategoriesSaved, OnSeTitleDataFailed);
        }

        private void OnCategoriesSaved(SetTitleDataResult result)
        {
            HideProgress();
            GetTitleData();
        }

        private void OnSeTitleDataFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // add new item
        private void AddNewItem(CatalogItem item)
        {
            Items.Add(item);
            SaveCatalog();
        }

        // remove item
        private void RemoveItem(CatalogItem item)
        {
            var itemID = item.ItemId;
            if (Items.Contains(item))
            {
                Items.Remove(item);
                Items.TrimExcess();
            }
            if (Packs.Contains(item))
            {
                Packs.Remove(item);
                Packs.TrimExcess();
            }
            if (LootBoxes.Contains(item))
            {
                LootBoxes.Remove(item);
                LootBoxes.TrimExcess();
                if (CbsLootboxGeneralData.Solution == LootboxSolution.CBS_CUSTOM)
                {
                    CbsLootboxTable.RemoveEntity(item.ItemId);
                    SaveLootboxTable(null);
                }
            }
            Icons.RemoveSprite(itemID);
            Recipes.RemoveRecipe(itemID);
            Upgrades.RemoveUpgrade(itemID);
            PrefabData.RemoveAsset(itemID);
            ScriptableData.RemoveAsset(itemID);
            OverrideCatalog();
        }

        // override catalog
        private void OverrideCatalog()
        {
            ShowProgress();

            var allItems = Items.Concat(Packs).ToList();
            allItems = allItems.Concat(LootBoxes).ToList();

            var dataRequest = new UpdateCatalogItemsRequest
            {
                Catalog = allItems,
                CatalogVersion = CatalogKeys.ItemsCatalogID
            };

            PlayFabAdminAPI.SetCatalogItems(dataRequest, OnCatalogUpdated, OnCatalogUpdatedFailed);
        }

        // save catalog
        private void SaveCatalog()
        {
            ShowProgress();

            var dataRequest = new UpdateCatalogItemsRequest
            {
                Catalog = Items,
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                SetAsDefaultCatalog = true
            };

            PlayFabAdminAPI.UpdateCatalogItems(dataRequest, OnCatalogUpdated, OnCatalogUpdatedFailed);
        }

        private void OnCatalogUpdated(UpdateCatalogItemsResult result)
        {
            HideProgress();

            SaveItemsMetaData();
        }

        private void OnCatalogUpdatedFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        private void SaveItemsMetaData()
        {
            ShowProgress();

            string recipesRawData = JsonPlugin.ToJsonCompress(Recipes);

            var recipeRequest = new SetTitleDataRequest
            {
                Key = TitleKeys.ItemsRecipeKey,
                Value = recipesRawData
            };

            PlayFabAdminAPI.SetTitleInternalData(recipeRequest, OnRecipeDataSaved, OnSeTitleDataFailed);
        }

        private void OnRecipeDataSaved(SetTitleDataResult result)
        {
            string upgradesRawData = JsonPlugin.ToJsonCompress(Upgrades);

            var upgradesRequest = new SetTitleDataRequest
            {
                Key = TitleKeys.ItemsUpgradeKey,
                Value = upgradesRawData
            };

            PlayFabAdminAPI.SetTitleInternalData(upgradesRequest, OnUpgradesDataSaved, OnSeTitleDataFailed);
        }

        private void OnUpgradesDataSaved(SetTitleDataResult result)
        {
            HideProgress();

            GetItemsCatalog();
        }

        // get items
        public void GetItemsCatalog(Action<GetCatalogItemsResult> finish = null)
        {
            ShowProgress();
            var dataRequest = new GetCatalogItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID
            };

            var succesCallback = finish == null ? OnItemsCatalogGetted : finish;

            PlayFabAdminAPI.GetCatalogItems(dataRequest, succesCallback, OnGetCatalogFailed);
        }

        private void OnItemsCatalogGetted(GetCatalogItemsResult result)
        {
            HideProgress();
            Items = result.Catalog.Where(x => x.Bundle == null && x.Container == null).ToList();
            Packs = result.Catalog.Where(x => x.Bundle != null).ToList();
            LootBoxes = result.Catalog.Where(x => x.Container != null).ToList();
        }

        private void OnGetCatalogFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }

        // get currency
        public void GetAllCurrencies(Action<ListVirtualCurrencyTypesResult> finish = null)
        {
            ShowProgress();

            var dataRequest = new ListVirtualCurrencyTypesRequest();

            var succesCallback = finish == null ? OnCurrenciesListGetted : finish;

            PlayFabAdminAPI.ListVirtualCurrencyTypes(dataRequest, succesCallback, OnGetCurrenciesListFailed);
        }

        private void OnCurrenciesListGetted(ListVirtualCurrencyTypesResult result)
        {
            HideProgress();
            VirtualCurrencies = result.VirtualCurrencies;
        }

        private void OnGetCurrenciesListFailed(PlayFabError error)
        {
            HideProgress();
            AddErrorLog(error);
        }
    }

    public enum ItemAction
    {
        ADD,
        EDIT
    }
}
#endif

#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using PlayFab.AdminModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddStoreItemWindow : EditorWindow
    {
        private static Dictionary<string, CatalogItem> AllCatalogItems = new Dictionary<string, CatalogItem>();
        private static List<CatalogItem> AllItems = new List<CatalogItem>();
        private static List<CatalogItem> AllLootboxes = new List<CatalogItem>();
        private static List<CatalogItem> AllPacks = new List<CatalogItem>();
        private static Categories ItemCategories = new Categories();
        private static Categories LootboxCategories = new Categories();
        private static Categories PackCategories = new Categories();
        private static Action<StoreItem> ModifyCallback { get; set; }

        private static StoreItem StoreItem { get; set; }

        private string SelectedItemID { get; set; }
        private int SelectedItemIndex { get; set; }
        private ItemType SelectedItemType { get; set; }

        private int SelectedItemCatagoryID { get; set; } = -1;

        private string[] ItemsIDsToSelect { get; set; }

        private ItemsIcons Icons { get; set; }

        private Vector2 ScrollPos { get; set; }

        public static void Show(StoreItemWindowRequest request)
        {
            AllCatalogItems = request.items.ToDictionary(x => x.ItemId, x => x);
            AllItems = request.items.Where(x => x.Bundle == null && x.Container == null).ToList();
            AllLootboxes = request.items.Where(x => x.Container != null).ToList();
            AllPacks = request.items.Where(x => x.Bundle != null).ToList();
            ItemCategories = request.itemCategories;
            LootboxCategories = request.lootboxCategories;
            PackCategories = request.packCategories;
            StoreItem = new StoreItem();
            ModifyCallback = request.modifyCallback;

            AddStoreItemWindow window = ScriptableObject.CreateInstance<AddStoreItemWindow>();
            window.maxSize = new Vector2(400, 700);
            window.minSize = window.maxSize;
            window.Init();
            window.ShowUtility();

        }

        private void Hide()
        {
            this.Close();
        }

        public void Init()
        {
            Icons = CBSScriptable.Get<ItemsIcons>();
        }

        void OnGUI()
        {
            using (var areaScope = new GUILayout.AreaScope(new Rect(0, 0, 400, 700)))
            {
                ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);
                GUILayout.Space(10);
                if (StoreItem == null)
                    return;

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 12;

                if (AllCatalogItems == null || AllCatalogItems.Count == 0)
                    return;

                // draw selected item
                if (!string.IsNullOrEmpty(SelectedItemID))
                {
                    var curSprite = Icons.GetSprite(SelectedItemID);
                    var curTexture = curSprite == null ? null : curSprite.texture;
                    var catalogItem = AllCatalogItems[SelectedItemID];
                    GUILayout.BeginHorizontal();
                    GUILayout.Button(curTexture, GUILayout.Width(100), GUILayout.Height(100));
                    EditorGUILayout.LabelField(catalogItem.DisplayName);
                    GUILayout.EndHorizontal();
                }

                // draw items

                EditorGUILayout.LabelField("Select item to add", titleStyle);

                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();

                GUILayout.BeginVertical();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Type");
                GUILayout.Label("Category");
                GUILayout.Label("Item ID");
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                var lastSavedType = SelectedItemType;
                SelectedItemType = (ItemType)EditorGUILayout.EnumPopup(SelectedItemType);
                if (SelectedItemType != lastSavedType)
                {
                    SelectedItemCatagoryID = -1;
                    SelectedItemIndex = 0;
                }

                var categoryList = ItemCategories.List ?? new List<string>();
                if (SelectedItemType == ItemType.ITEMS)
                    categoryList = ItemCategories.List ?? new List<string>();
                if (SelectedItemType == ItemType.LOOT_BOXES)
                    categoryList = LootboxCategories.List ?? new List<string>();
                if (SelectedItemType == ItemType.PACKS)
                    categoryList = PackCategories.List ?? new List<string>();
                if (!categoryList.Contains(CBSConstants.UndefinedCategory))
                    categoryList.Insert(0, CBSConstants.UndefinedCategory);

                int selectedCategory = EditorGUILayout.Popup(SelectedItemCatagoryID == -1 ? 0 : SelectedItemCatagoryID, categoryList.ToArray());
                if (selectedCategory != SelectedItemCatagoryID)
                {
                    var itemListToParse = AllItems;
                    if (SelectedItemType == ItemType.ITEMS)
                        itemListToParse = AllItems;
                    if (SelectedItemType == ItemType.LOOT_BOXES)
                        itemListToParse = AllLootboxes;
                    if (SelectedItemType == ItemType.PACKS)
                        itemListToParse = AllPacks;
                    if (categoryList.Count > 0)
                    {
                        string category = categoryList[selectedCategory];
                        if (category == CBSConstants.UndefinedCategory)
                            ItemsIDsToSelect = itemListToParse.Select(x => x.ItemId).ToArray();
                        else
                            ItemsIDsToSelect = itemListToParse.Where(i => i.Tags[0] == category).Select(x => x.ItemId).ToArray();
                    }
                    else
                    {
                        ItemsIDsToSelect = itemListToParse.Select(x => x.ItemId).ToArray();
                    }
                }
                SelectedItemCatagoryID = selectedCategory;
                SelectedItemIndex = EditorGUILayout.Popup(SelectedItemIndex, ItemsIDsToSelect);
                GUILayout.EndHorizontal();

                GUILayout.EndVertical();

                GUILayout.EndVertical();
                if (ItemsIDsToSelect.Length > 0)
                    SelectedItemID = ItemsIDsToSelect[SelectedItemIndex];
                else
                    SelectedItemID = string.Empty;

                GUILayout.EndVertical();

                EditorGUILayout.EndScrollView();

                GUILayout.FlexibleSpace();

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Add"))
                {
                    if (string.IsNullOrEmpty(SelectedItemID))
                        return;
                    var catalogItem = AllCatalogItems[SelectedItemID];
                    StoreItem = new StoreItem
                    {
                        ItemId = SelectedItemID,
                        VirtualCurrencyPrices = catalogItem.VirtualCurrencyPrices
                    };
                    ModifyCallback?.Invoke(StoreItem);
                    Hide();
                }
                if (GUILayout.Button("Close"))
                {
                    Hide();
                }

                GUILayout.EndHorizontal();
            }
        }
    }

    public struct StoreItemWindowRequest
    {
        public Action<StoreItem> modifyCallback;
        public List<CatalogItem> items;
        public Categories itemCategories;
        public Categories lootboxCategories;
        public Categories packCategories;
    }
}
#endif
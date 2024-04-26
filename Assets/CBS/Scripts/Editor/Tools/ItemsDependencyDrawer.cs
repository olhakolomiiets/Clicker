#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using PlayFab.AdminModels;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ItemsDependencyDrawer
    {
        private string SelectedItemID { get; set; }
        private int SelectedItemIndex { get; set; }
        private int SelectedItemCatagoryID { get; set; } = -1;
        private int SelectedCurrencyIndex { get; set; }
        private int CurrentRecipeItemsCount { get; set; }

        public CBSItemDependency Dependency { get; private set; }
        private EditorWindow EditorContext { get; set; }
        public List<string> Categories { get; set; }
        public List<CatalogItem> Items { get; set; }
        private List<string> Currencies { get; set; }
        public string[] AllItemsIDs { get; set; }

        private Dictionary<string, uint> CurrencyDependecies { get; set; }
        private Dictionary<string, uint> ItemsDependencies { get; set; }

        private ItemsIcons Icons { get; set; }

        public ItemsDependencyDrawer(CBSItemDependency dependency, List<string> categories, List<CatalogItem> items, List<string> currencies, EditorWindow editorContenxt)
        {
            Dependency = dependency;
            Categories = categories;
            Items = items;
            Currencies = currencies;
            Icons = CBSScriptable.Get<ItemsIcons>();
            EditorContext = editorContenxt;

            ItemsDependencies = dependency.ItemsDependencies ?? new Dictionary<string, uint>();
            CurrencyDependecies = dependency.CurrencyDependecies ?? new Dictionary<string, uint>();
        }

        public void DrawRecipe()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            // draw items

            EditorGUILayout.LabelField("Recipe", titleStyle);

            GUILayout.BeginVertical();

            GUILayout.Space(5);

            for (int i = 0; i < ItemsDependencies.Count; i++)
            {
                var keyPair = ItemsDependencies.ElementAt(i);
                string key = keyPair.Key;
                int value = (int)keyPair.Value;

                if (key == CBSConstants.UndefinedCategory)
                    continue;

                GUILayout.BeginHorizontal();

                GUILayout.Space(10);

                GUILayout.BeginVertical();
                var actvieSprite = Icons.GetSprite(key);
                var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                EditorGUILayout.LabelField(key, GUILayout.Width(100));
                GUILayout.Button(iconTexture, GUILayout.Width(50), GUILayout.Height(50));
                GUILayout.EndVertical();

                GUILayout.BeginVertical();
                EditorGUILayout.LabelField("Count", GUILayout.Width(50));
                value = EditorGUILayout.IntField(value, GUILayout.Width(100));
                if (value < 1)
                    value = 1;
                ItemsDependencies[key] = (uint)value;
                GUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    ItemsDependencies.Remove(key);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Category");
            GUILayout.Label("Item ID");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            int selectedCategory = EditorGUILayout.Popup(SelectedItemCatagoryID == -1 ? 0 : SelectedItemCatagoryID, Categories.ToArray());
            if (selectedCategory != SelectedItemCatagoryID)
            {
                string category = Categories[selectedCategory];
                AllItemsIDs = Items.Where(i => i.Tags[0] == category).Select(x => x.ItemId).ToArray();
            }
            SelectedItemCatagoryID = selectedCategory;
            SelectedItemIndex = EditorGUILayout.Popup(SelectedItemIndex, AllItemsIDs);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();

            GUILayout.EndVertical();

            if (GUILayout.Button("Add"))
            {
                if (AllItemsIDs.Length > 0)
                {
                    SelectedItemID = AllItemsIDs[SelectedItemIndex];
                    if (!string.IsNullOrEmpty(SelectedItemID) && !ItemsDependencies.ContainsKey(SelectedItemID))
                    {
                        ItemsDependencies.Add(SelectedItemID, 1);
                    }
                }
            }

            Dependency.ItemsDependencies = ItemsDependencies;

            GUILayout.EndVertical();
        }

        public void DrawPrices()
        {
            if (Currencies != null && Currencies.Count != 0)
            {
                // draw currencies list
                GUILayout.Space(10);
                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 12;

                // draw items

                EditorGUILayout.LabelField("Price", titleStyle);
                GUILayout.Space(5);

                if (CurrencyDependecies != null && CurrencyDependecies.Count != 0)
                {
                    for (int i = 0; i < CurrencyDependecies.Count; i++)
                    {
                        string key = CurrencyDependecies.Keys.ElementAt(i);
                        int val = (int)CurrencyDependecies.Values.ElementAt(i);
                        GUILayout.BeginHorizontal();
                        CurrencyDependecies[key] = (uint)EditorGUILayout.IntField(key, val);

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            CurrencyDependecies.Remove(key);
                        }

                        GUILayout.EndHorizontal();
                    }
                }
                // add currency button
                GUILayout.Space(15);
                GUILayout.BeginHorizontal();
                if (Currencies != null && Currencies.Count != 0)
                {
                    SelectedCurrencyIndex = EditorGUILayout.Popup(SelectedCurrencyIndex, Currencies.ToArray());
                    string defaultKey = Currencies[SelectedCurrencyIndex];
                    if (GUILayout.Button("Add currency"))
                    {
                        if (!CurrencyDependecies.ContainsKey(defaultKey))
                            CurrencyDependecies[defaultKey] = 0;
                    }
                }
                GUILayout.EndHorizontal();
            }

            Dependency.CurrencyDependecies = CurrencyDependecies;
        }

        public void DrawRecipeLimit()
        {
            CurrentRecipeItemsCount = CalculateItemsLimit();
            var limitLabel = string.Format("Items Limit {0}/{1}", CurrentRecipeItemsCount, CBSConstants.MaxItemsCountForRecipe);
            EditorGUILayout.IntSlider(limitLabel, Mathf.RoundToInt(CurrentRecipeItemsCount), 0, CBSConstants.MaxItemsCountForRecipe);
            if (!IsValid())
            {
                EditorGUILayout.HelpBox("Items limit reached. Max is 100", MessageType.Error);
            }
        }

        public bool IsValid()
        {
            return CalculateItemsLimit() <= CBSConstants.MaxItemsCountForRecipe;
        }

        private int CalculateItemsLimit()
        {
            if (ItemsDependencies == null)
                return 0;
            var limitCounter = 0;
            for (int i = 0; i < ItemsDependencies.Count; i++)
            {
                var itemPair = ItemsDependencies.ElementAt(i);
                var itemID = itemPair.Key;
                var itemCount = itemPair.Value;
                var fabItem = Items.FirstOrDefault(x => x.ItemId == itemID);
                if (fabItem != null && fabItem.Consumable != null && fabItem.Consumable.UsageCount > 0)
                {
                    limitCounter++;
                }
                else
                {
                    limitCounter += (int)itemCount;
                }
            }
            return limitCounter;
        }
    }
}
#endif

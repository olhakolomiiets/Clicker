#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ItemRecipeDrawer
    {
        private string SelectedItemID { get; set; }
        private int SelectedItemIndex { get; set; }
        private int SelectedItemCatagoryID { get; set; } = -1;
        private string[] AllItemsIDs { get; set; }

        private ItemsDependencyDrawer DependencyDrawer { get; set; }
        private ObjectCustomDataDrawer<CBSRecipeCustomData> CustomDataDrawer { get; set; }
        private CBSItemRecipe Recipe { get; set; }
        private ItemsIcons Icons { get; set; }
        private EditorData EditorData { get; set; }

        private readonly Texture2D TargetTex;
        private readonly Texture2D ContentTex;
        private readonly Texture2D PriceTex;
        private readonly Texture2D CustomDataTex;

        public ItemRecipeDrawer(CBSItemRecipe recipe, ItemsDependencyDrawer dependencyDrawer)
        {
            Recipe = recipe;
            DependencyDrawer = dependencyDrawer;
            Icons = CBSScriptable.Get<ItemsIcons>();
            EditorData = CBSScriptable.Get<EditorData>();
            CustomDataDrawer = new ObjectCustomDataDrawer<CBSRecipeCustomData>(PlayfabUtils.ITEM_CUSTOM_DATA_SIZE, 380f);

            TargetTex = EditorUtils.MakeColorTexture(EditorData.RecipeTarget);
            ContentTex = EditorUtils.MakeColorTexture(EditorData.RecipeContent);
            PriceTex = EditorUtils.MakeColorTexture(EditorData.RecipePrice);
            CustomDataTex = EditorUtils.MakeColorTexture(EditorData.RecipeCustomData);
        }

        public bool IsValid()
        {
            return DependencyDrawer.IsValid() && CustomDataDrawer.IsInputValid();
        }

        public void Draw()
        {
            GUIStyle recipeBoxStyle = new GUIStyle("HelpBox");

            recipeBoxStyle.normal.background = TargetTex;
            using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
            {
                DrawTarget();
            }

            GUILayout.Space(5);

            recipeBoxStyle.normal.background = ContentTex;
            using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
            {
                DependencyDrawer.DrawRecipe();
                DependencyDrawer.DrawRecipeLimit();
                EditorGUILayout.HelpBox("Recipe for item creation. The recipe has an item limit of 100. Consumable items count as one, but you cannot add more than 10 different instances of Consumable items in recipe", MessageType.Info);
            }

            GUILayout.Space(5);

            recipeBoxStyle.normal.background = PriceTex;
            using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
            {
                DependencyDrawer.DrawPrices();
                EditorGUILayout.HelpBox("Recipe price to create item", MessageType.Info);
            }

            GUILayout.Space(5);

            recipeBoxStyle.normal.background = CustomDataTex;
            using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
            {
                DrawCustomData();
                EditorGUILayout.HelpBox("Custom data for recipe", MessageType.Info);
            }
        }

        private void DrawTarget()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var targetStyle = new GUIStyle(GUI.skin.label);
            targetStyle.alignment = TextAnchor.MiddleCenter;

            var targetItemID = Recipe.ItemIdToGraft;
            if (!string.IsNullOrEmpty(targetItemID) && string.IsNullOrEmpty(SelectedItemID))
            {
                var fabItem = DependencyDrawer.Items.FirstOrDefault(x => x.ItemId == targetItemID);
                if (fabItem != null)
                {
                    SelectedItemID = targetItemID;
                    var category = fabItem.Tags[0];
                    SelectedItemCatagoryID = DependencyDrawer.Categories.IndexOf(category);
                    AllItemsIDs = DependencyDrawer.Items.Where(i => i.Tags[0] == category).Select(x => x.ItemId).ToArray();
                    SelectedItemIndex = AllItemsIDs.ToList().IndexOf(targetItemID);
                }
            }

            // draw items

            GUILayout.Space(10);
            EditorGUILayout.LabelField("Item to craft", titleStyle);

            if (!string.IsNullOrEmpty(SelectedItemID))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(145);
                GUILayout.BeginVertical();
                var actvieSprite = Icons.GetSprite(SelectedItemID);
                var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                EditorGUILayout.LabelField(SelectedItemID, targetStyle, GUILayout.Width(100));
                GUILayout.Button(iconTexture, GUILayout.Width(100), GUILayout.Height(100));
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginVertical();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Category");
            GUILayout.Label("Item ID");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            int selectedCategory = EditorGUILayout.Popup(SelectedItemCatagoryID == -1 ? 0 : SelectedItemCatagoryID, DependencyDrawer.Categories.ToArray());
            if (selectedCategory != SelectedItemCatagoryID)
            {
                string category = DependencyDrawer.Categories[selectedCategory];
                AllItemsIDs = DependencyDrawer.Items.Where(i => i.Tags[0] == category).Select(x => x.ItemId).ToArray();
            }
            SelectedItemCatagoryID = selectedCategory;
            SelectedItemIndex = EditorGUILayout.Popup(SelectedItemIndex, AllItemsIDs);
            SelectedItemID = AllItemsIDs[SelectedItemIndex];
            Recipe.ItemIdToGraft = SelectedItemID;

            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUILayout.EndVertical();
            GUILayout.EndVertical();

            // draw limit

            EditorGUILayout.HelpBox("Target item to create from recipe", MessageType.Info);

            GUILayout.Space(10);
        }

        private void DrawCustomData()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            EditorGUILayout.LabelField("Custom Data", titleStyle);
            CustomDataDrawer.Draw<CBSRecipeCustomData>(Recipe);
        }
    }
}
#endif


#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class ItemUpgradeDrawer
    {
        private int UpgradeIndex { get; set; }

        private ItemsDependencyDrawer DependencyDrawer { get; set; }
        private ObjectCustomDataDrawer<CBSUpgradeItemCustomData> CustomDataDrawer { get; set; }
        private CBSItemUpgradeState Upgrade { get; set; }
        private ItemsIcons Icons { get; set; }
        private EditorData EditorData { get; set; }

        private bool Extend { get; set; }

        private readonly Texture2D TargetTex;
        private readonly Texture2D ContentTex;
        private readonly Texture2D PriceTex;
        private readonly Texture2D CustomDataTex;

        public ItemUpgradeDrawer(int upgradeIndex, CBSItemUpgradeState upgrade, ItemsDependencyDrawer dependencyDrawer)
        {
            UpgradeIndex = upgradeIndex;
            Upgrade = upgrade;
            DependencyDrawer = dependencyDrawer;
            Icons = CBSScriptable.Get<ItemsIcons>();
            EditorData = CBSScriptable.Get<EditorData>();
            CustomDataDrawer = new ObjectCustomDataDrawer<CBSUpgradeItemCustomData>(PlayfabUtils.ITEM_CUSTOM_DATA_SIZE, 380f);

            TargetTex = EditorUtils.MakeColorTexture(EditorData.UpgradeTitle);
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
                DrawLabel();
            }

            if (Extend)
            {
                GUILayout.Space(5);

                recipeBoxStyle.normal.background = CustomDataTex;
                using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
                {
                    DrawCustomData();
                    EditorGUILayout.HelpBox("Custom data for upgrade state", MessageType.Info);
                }

                GUILayout.Space(5);

                recipeBoxStyle.normal.background = ContentTex;
                using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
                {
                    DependencyDrawer.DrawRecipe();
                    DependencyDrawer.DrawRecipeLimit();
                    EditorGUILayout.HelpBox("Ingredients to upgrade item. The ingredients has an limit of 100. Consumable items count as one, but you cannot add more than 10 different instances of Consumable items in recipe", MessageType.Info);
                }

                GUILayout.Space(5);

                recipeBoxStyle.normal.background = PriceTex;
                using (var horizontalScope = new GUILayout.VerticalScope(recipeBoxStyle))
                {
                    DependencyDrawer.DrawPrices();
                    EditorGUILayout.HelpBox("Price to upgrade item", MessageType.Info);
                }
            }
        }

        private void DrawLabel()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var targetStyle = new GUIStyle(GUI.skin.label);
            targetStyle.alignment = TextAnchor.MiddleCenter;

            // draw label

            GUILayout.Space(10);
            var upgradeLabel = string.Format("Upgrade level - {0}", UpgradeIndex);
            //EditorGUILayout.LabelField(upgradeLabel, titleStyle);
            if (GUILayout.Button(upgradeLabel, titleStyle))
            {
                Extend = !Extend;
            }
            // draw limit

            GUILayout.Space(10);
        }

        private void DrawCustomData()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            EditorGUILayout.LabelField("Custom Data", titleStyle);
            CustomDataDrawer.Draw<CBSUpgradeItemCustomData>(Upgrade);
        }
    }
}
#endif
using CBS.Models;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class ModifyCateroriesWindow : EditorWindow
    {
        private static Action<Categories> ModifyCallback { get; set; }
        private static Categories Categories { get; set; }

        private string EnteredName { get; set; }
        
        private Vector2 PositionScroll { get; set; }

        public static void Show(Action<Categories> modifyCallback, Categories categories)
        {
            ModifyCallback = modifyCallback;
            Categories = categories ?? new Categories();

            ModifyCateroriesWindow window = ScriptableObject.CreateInstance<ModifyCateroriesWindow>();
            window.maxSize = new Vector2(400, 700);
            window.minSize = window.maxSize;
            window.ShowUtility();
        }

        private void Hide()
        {
            this.Close();
        }

        void OnGUI()
        {
            using (var areaScope = new GUILayout.AreaScope(new Rect(0, 0, 400, 700)))
            {
                if (Categories == null)
                    return;
                
                PositionScroll = GUILayout.BeginScrollView(PositionScroll);

                GUILayout.Space(10);

                GUILayout.BeginVertical();

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 18;
                EditorGUILayout.LabelField("Categories", titleStyle);

                GUILayout.Space(30);

                var categoryList = Categories.List ?? new List<string>();

                for (int i = 0; i < categoryList.Count; i++)
                {
                    string key = categoryList[i];

                    if (key == CBSConstants.UndefinedCategory)
                        continue;

                    GUILayout.BeginHorizontal();

                    GUILayout.Space(30);

                    EditorGUILayout.LabelField(key);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        categoryList.Remove(key);
                        categoryList.TrimExcess();
                    }

                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(30);

                GUILayout.BeginHorizontal();

                GUILayout.Label("Category ID", GUILayout.Width(70));
                EnteredName = GUILayout.TextField(EnteredName);

                if (GUILayout.Button("Add"))
                {
                    if (!string.IsNullOrEmpty(EnteredName) && !categoryList.Contains(EnteredName))
                    {
                        categoryList.Add(EnteredName);
                    }
                    EnteredName = string.Empty;
                }

                GUILayout.EndVertical();

                GUILayout.Space(30);

                GUILayout.BeginHorizontal();
                Categories.List = categoryList;

                if (GUILayout.Button("Save"))
                {
                    ModifyCallback?.Invoke(Categories);
                    Hide();
                }
                if (GUILayout.Button("Close"))
                {
                    Hide();
                }

                GUILayout.EndHorizontal();

                GUILayout.EndVertical();
                
                GUILayout.EndScrollView();
            }
        }
    }
}

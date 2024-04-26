using CBS.Models;
using CBS.Utils;
using System;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddTitleDataWindow : EditorWindow
    {
        private static Action<CBSTitleData> AddCallback { get; set; }

        private string EntereID { get; set; }

        public static void Show(Action<CBSTitleData> modifyCallback)
        {
            AddCallback = modifyCallback;

            AddTitleDataWindow window = ScriptableObject.CreateInstance<AddTitleDataWindow>();
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
                GUILayout.Space(10);

                GUILayout.BeginVertical();

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 18;
                EditorGUILayout.LabelField("Add new Title Data", titleStyle);

                GUILayout.Space(30);

                GUILayout.Label("Data Key", GUILayout.Width(120));
                EntereID = GUILayout.TextField(EntereID);

                GUILayout.Space(5);

                EditorGUILayout.HelpBox("id cannot contain special characters (/*-+_@&$#%)", MessageType.Info);

                GUILayout.EndVertical();

                GUILayout.Space(30);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    if (string.IsNullOrEmpty(EntereID))
                        return;
                    if (TextUtils.ContainSpecialSymbols(EntereID))
                        return;
                    var newInstance = new CBSTitleData
                    {
                        DataKey = EntereID
                    };
                    AddCallback?.Invoke(newInstance);
                    Hide();
                }
                if (GUILayout.Button("Close"))
                {
                    Hide();
                }

                GUILayout.EndVertical();
            }
        }
    }
}

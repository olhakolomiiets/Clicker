using System;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddEventWindow : EditorWindow
    {
        private static Action<string> AddCallback { get; set; }

        private string EnteredName { get; set; }

        public static void Show(Action<string> modifyCallback)
        {
            AddCallback = modifyCallback;

            AddEventWindow window = ScriptableObject.CreateInstance<AddEventWindow>();
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
                EditorGUILayout.LabelField("Add new Event", titleStyle);

                GUILayout.Space(15);

                GUILayout.Label("Display Name", GUILayout.Width(120));
                EnteredName = GUILayout.TextField(EnteredName);

                GUILayout.EndVertical();

                GUILayout.Space(30);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    if (string.IsNullOrEmpty(EnteredName))
                        return;
                    AddCallback?.Invoke(EnteredName);
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

using CBS.Scriptable;
using CBS.Utils;
using System;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddStoreWindow : EditorWindow
    {
        private static Action<string, string, string> AddCallback { get; set; }
        private static string[] ItemIDs;

        private string EntereID { get; set; }
        private string EnteredName { get; set; }
        private string SelectedItemID { get; set; }

        private ItemsIcons Icons { get; set; }

        private int SelectedItemIndex { get; set; }

        public static void Show(string[] itemsIDs, Action<string, string, string> modifyCallback)
        {
            AddCallback = modifyCallback;
            ItemIDs = itemsIDs;

            AddStoreWindow window = ScriptableObject.CreateInstance<AddStoreWindow>();
            window.maxSize = new Vector2(400, 700);
            window.minSize = window.maxSize;
            window.ShowUtility();
            window.Init();
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
                GUILayout.Space(10);

                GUILayout.BeginVertical();

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 18;
                EditorGUILayout.LabelField("Add new Store", titleStyle);

                GUILayout.Space(30);

                GUILayout.Label("Store ID", GUILayout.Width(120));
                EntereID = GUILayout.TextField(EntereID);

                GUILayout.Space(5);

                EditorGUILayout.HelpBox("id cannot contain special characters (/*-+_@&$#%)", MessageType.Info);

                GUILayout.Space(5);

                GUILayout.Label("Store Name", GUILayout.Width(120));
                EnteredName = GUILayout.TextField(EnteredName);

                GUILayout.Space(5);
                GUILayout.Label("Default item", GUILayout.Width(120));
                if (ItemIDs == null || ItemIDs.Length == 0)
                {
                    EditorGUILayout.HelpBox("No item found, store creation requires at least one", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Default item. PlayFab requires at least one item to create a store. It can be changed later", MessageType.Info);
                    GUILayout.BeginHorizontal();
                    var actvieSprite = Icons.GetSprite(SelectedItemID);
                    var iconTexture = actvieSprite == null ? null : actvieSprite.texture;
                    GUILayout.Button(iconTexture, GUILayout.Width(50), GUILayout.Height(50));
                    SelectedItemIndex = EditorGUILayout.Popup(SelectedItemIndex, ItemIDs);
                    SelectedItemID = ItemIDs[SelectedItemIndex];
                    GUILayout.EndHorizontal();
                }

                GUILayout.EndVertical();

                GUILayout.Space(30);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    if (string.IsNullOrEmpty(EntereID) || string.IsNullOrEmpty(EnteredName) || string.IsNullOrEmpty(SelectedItemID))
                        return;
                    if (TextUtils.ContainSpecialSymbols(EntereID))
                        return;

                    AddCallback?.Invoke(EntereID, EnteredName, SelectedItemID);
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

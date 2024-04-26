#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using System;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddStickerWindow : EditorWindow
    {
        private static ItemAction Action { get; set; }
        private static Action<ChatSticker> ModifyCallback { get; set; }
        private static ChatSticker CurrentSticker { get; set; }

        private string ID { get; set; }

        private StickersIcons Icons { get; set; }
        private Sprite IconSprite { get; set; }
        private Vector2 ScrollPos { get; set; }

        public static void Show(ItemAction action, ChatSticker sticker, Action<ChatSticker> onModify)
        {
            Action = action;
            ModifyCallback = onModify;
            CurrentSticker = sticker;
            AddStickerWindow window = ScriptableObject.CreateInstance<AddStickerWindow>();
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
            Icons = CBSScriptable.Get<StickersIcons>();
            ID = CurrentSticker.ID;
            IconSprite = Icons.GetSprite(ID);
        }

        void OnGUI()
        {
            using (var areaScope = new GUILayout.AreaScope(new Rect(0, 0, 400, 700)))
            {
                ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);
                GUILayout.Space(15);

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.alignment = TextAnchor.MiddleCenter;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 14;

                // draw items

                if (Action == ItemAction.ADD)
                {
                    ID = EditorGUILayout.TextField("Sticker ID", ID);
                }
                if (Action == ItemAction.EDIT)
                {
                    EditorGUILayout.LabelField("Sticker ID", ID);
                }
                EditorGUILayout.HelpBox("Unique id for sticker.", MessageType.Info);

                GUILayout.Space(15);
                // draw icon
                EditorGUILayout.LabelField("Sprite", new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                IconSprite = (Sprite)EditorGUILayout.ObjectField((IconSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                EditorGUILayout.HelpBox("Sprite for sticker. ATTENTION! The sprite is not saved on the server, it will be included in the build", MessageType.Info);



                GUILayout.Space(30);

                EditorGUILayout.EndScrollView();

                GUILayout.FlexibleSpace();

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    ApplyChanges(CurrentSticker);
                    if (IsValidInput())
                    {
                        ModifyCallback?.Invoke(CurrentSticker);
                        Hide();
                    }
                }
                if (GUILayout.Button("Close"))
                {
                    Hide();
                }

                GUILayout.EndHorizontal();
            }
        }

        private void ApplyChanges(ChatSticker sticker)
        {
            sticker.ID = ID;

            if (IconSprite == null)
            {
                Icons.RemoveSprite(ID);
            }
            else
            {
                Icons.SaveSprite(ID, IconSprite);
            }
        }

        private bool IsValidInput()
        {
            if (string.IsNullOrEmpty(ID))
                return false;
            if (CurrentSticker == null)
                return false;
            return true;
        }
    }
}
#endif
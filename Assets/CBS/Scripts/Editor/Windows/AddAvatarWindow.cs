#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddAvatarWindow : EditorWindow
    {
        private static ItemAction Action { get; set; }
        private static Action<CBSSpriteAvatar> ModifyCallback { get; set; }
        private static CBSSpriteAvatar CurrentAvatar { get; set; }

        private string ID { get; set; }
        private bool HasPrice { get; set; }
        private bool HasLevel { get; set; }
        private CBSPrice Price { get; set; }
        private int LevelLimit { get; set; }
        private int SelectedCurrencyIndex { get; set; }

        private AvatarIcons Icons { get; set; }
        private Sprite IconSprite { get; set; }
        private static List<string> Currencies { get; set; }
        private Vector2 ScrollPos { get; set; }

        public static void Show(ItemAction action, CBSSpriteAvatar avatar, List<string> virtualCurrencies, Action<CBSSpriteAvatar> onModify)
        {
            Action = action;
            ModifyCallback = onModify;
            CurrentAvatar = avatar;
            Currencies = virtualCurrencies;
            AddAvatarWindow window = ScriptableObject.CreateInstance<AddAvatarWindow>();
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
            Icons = CBSScriptable.Get<AvatarIcons>();

            ID = CurrentAvatar.ID;
            HasLevel = CurrentAvatar.HasLevelLimit;
            HasPrice = CurrentAvatar.Purchasable;
            Price = CurrentAvatar.Price ?? new CBSPrice();
            LevelLimit = CurrentAvatar.LevelLimit;
            IconSprite = Icons.GetSprite(ID);

            if (!string.IsNullOrEmpty(Price.CurrencyID))
            {
                SelectedCurrencyIndex = Currencies.IndexOf(Price.CurrencyID);
            }
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
                    ID = EditorGUILayout.TextField("Avatar ID", ID);
                }
                if (Action == ItemAction.EDIT)
                {
                    EditorGUILayout.LabelField("Avatar ID", ID);
                }
                EditorGUILayout.HelpBox("Unique id for avatar.", MessageType.Info);

                GUILayout.Space(15);
                // draw icon
                EditorGUILayout.LabelField("Sprite", new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                IconSprite = (Sprite)EditorGUILayout.ObjectField((IconSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                EditorGUILayout.HelpBox("Sprite for avatar. ATTENTION! The sprite is not saved on the server, it will be included in the build", MessageType.Info);

                GUILayout.Space(15);
                HasLevel = EditorGUILayout.Toggle("Has level limit", HasLevel);
                EditorGUILayout.HelpBox("Make an avatar available after reaching a certain level", MessageType.Info);

                if (HasLevel)
                {
                    GUILayout.Space(15);
                    LevelLimit = EditorGUILayout.IntField("Level limit", LevelLimit);
                    EditorGUILayout.HelpBox("The avatar will be available to the player upon reaching the set level.", MessageType.Info);
                }

                GUILayout.Space(15);
                HasPrice = EditorGUILayout.Toggle("Has price", HasPrice);
                EditorGUILayout.HelpBox("Make avatar available for purchase", MessageType.Info);
                // draw price
                if (HasPrice)
                {
                    if (Currencies != null && Currencies.Count != 0)
                    {
                        // add currency button
                        GUILayout.Space(15);
                        if (Currencies != null && Currencies.Count != 0)
                        {
                            SelectedCurrencyIndex = EditorGUILayout.Popup("Code", SelectedCurrencyIndex, Currencies.ToArray());
                            string currencyKey = Currencies[SelectedCurrencyIndex];
                            Price.CurrencyID = currencyKey;

                            Price.CurrencyValue = EditorGUILayout.IntField("Value", Price.CurrencyValue);
                            if (Price.CurrencyValue < 0)
                                Price.CurrencyValue = 0;
                        }
                    }
                }

                GUILayout.Space(30);

                EditorGUILayout.EndScrollView();

                GUILayout.FlexibleSpace();

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Save"))
                {
                    ApplyChanges(CurrentAvatar);
                    if (IsValidInput())
                    {
                        ModifyCallback?.Invoke(CurrentAvatar);
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

        private void ApplyChanges(CBSSpriteAvatar avatar)
        {
            avatar.ID = ID;
            avatar.LevelLimit = LevelLimit;
            avatar.HasLevelLimit = HasLevel;
            avatar.Purchasable = HasPrice;
            avatar.Price = Price;

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
            if (CurrentAvatar == null)
                return false;
            return true;
        }
    }
}
#endif
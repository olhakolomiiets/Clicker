#if ENABLE_PLAYFABADMIN_API
using CBS.Scriptable;
using CBS.Utils;
using PlayFab.AdminModels;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddCurrencyPackWindow : EditorWindow
    {
        private static Action<CatalogItem> AddCallback { get; set; }
        private static CatalogItem CurrentData { get; set; }
        private static CurrencyAction Action { get; set; }
        private static List<string> CurrenciesKeys;

        private string ID { get; set; }
        private string DisplayName { get; set; }
        private string Description { get; set; }
        private string ExternalUrl { get; set; }
        private string ItemTag { get; set; }
        private string PriceTitle { get; set; }
        private Dictionary<string, uint> Currencies { get; set; }
        private Sprite IconSprite { get; set; }

        private Vector2 ScrollPos { get; set; }

        private bool IsInited { get; set; } = false;

        private int SelectedCurrencyIndex { get; set; }

        private CurrencyIcons Icons { get; set; }

        private ObjectCustomDataDrawer<CBSCurrencyPackCustomData> CustomDataDrawer { get; set; }
        private ItemCustomDataAdapter<CBSCurrencyPackCustomData> DataAdapter { get; set; }

        public static void Show(CatalogItem current, Action<CatalogItem> addCallback, CurrencyAction action, List<string> codes)
        {
            AddCallback = addCallback;
            CurrentData = current;
            Action = action;
            CurrenciesKeys = codes;

            AddCurrencyPackWindow window = ScriptableObject.CreateInstance<AddCurrencyPackWindow>();
            window.maxSize = new Vector2(400, 700);
            window.minSize = window.maxSize;
            window.ShowUtility();
        }

        private void Hide()
        {
            this.Close();
        }

        private void Init()
        {
            DataAdapter = new ItemCustomDataAdapter<CBSCurrencyPackCustomData>(CurrentData);
            CustomDataDrawer = new ObjectCustomDataDrawer<CBSCurrencyPackCustomData>(PlayfabUtils.ITEM_CUSTOM_DATA_SIZE, 400f);
            Icons = CBSScriptable.Get<CurrencyIcons>();
            ID = CurrentData.ItemId;
            DisplayName = CurrentData.DisplayName;
            Description = CurrentData.Description;
            ExternalUrl = CurrentData.ItemImageUrl;
            PriceTitle = CurrentData.CustomData;
            if (CurrentData.Bundle == null)
                CurrentData.Bundle = new CatalogItemBundleInfo();
            Currencies = CurrentData.Bundle.BundledVirtualCurrencies ?? new Dictionary<string, uint>();

            bool tagExist = CurrentData.Tags != null && CurrentData.Tags.Count != 0;
            ItemTag = tagExist ? CurrentData.Tags[0] : string.Empty;

            IconSprite = Icons.GetSprite(ID);

            IsInited = true;
        }

        private void ApplyInputs()
        {
            CurrentData.ItemId = ID;
            CurrentData.DisplayName = DisplayName;
            CurrentData.Description = Description;
            CurrentData.ItemImageUrl = ExternalUrl;

            if (!string.IsNullOrEmpty(ItemTag))
            {
                if (CurrentData.Tags == null)
                {
                    CurrentData.Tags = new List<string>();
                }
                if (CurrentData.Tags.Count == 0)
                {
                    CurrentData.Tags.Add(ItemTag);
                }
                else
                {
                    CurrentData.Tags[0] = ItemTag;
                }
            }

            CurrentData.Bundle = new CatalogItemBundleInfo();
            CurrentData.Bundle.BundledVirtualCurrencies = Currencies;
        }

        public bool IsValidInputs()
        {
            return CustomDataDrawer.IsInputValid();
        }

        void OnGUI()
        {
            using (var areaScope = new GUILayout.AreaScope(new Rect(0, 0, 400, 700)))
            {
                ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);
                // init start values
                if (!IsInited)
                {
                    Init();
                }
                GUILayout.Space(15);
                if (Action == CurrencyAction.ADD)
                {
                    ID = EditorGUILayout.TextField("Pack ID", ID);
                }
                if (Action == CurrencyAction.EDIT)
                {
                    EditorGUILayout.LabelField("Item ID", ID);
                }
                EditorGUILayout.HelpBox("Unique id for currency pack.", MessageType.Info);

                GUILayout.Space(15);
                DisplayName = EditorGUILayout.TextField("Title", DisplayName);
                EditorGUILayout.HelpBox("Full name of the pack", MessageType.Info);

                // draw icon
                EditorGUILayout.LabelField("Sprite", new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                IconSprite = (Sprite)EditorGUILayout.ObjectField((IconSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                EditorGUILayout.HelpBox("Sprite for game currency. ATTENTION! The sprite is not saved on the server, it will be included in the build", MessageType.Info);

                // draw currencies list
                GUILayout.Space(30);

                EditorGUILayout.LabelField("Pack content");

                if (Currencies != null && CurrenciesKeys.Count != 0)
                {
                    for (int i = 0; i < Currencies.Count; i++)
                    {
                        string key = Currencies.Keys.ElementAt(i);
                        int val = (int)Currencies.Values.ElementAt(i);
                        GUILayout.BeginHorizontal();
                        Currencies[key] = (uint)EditorGUILayout.IntField(key, val);

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            Currencies.Remove(key);
                        }

                        GUILayout.EndHorizontal();
                    }
                }
                // add currency button
                GUILayout.Space(15);
                GUILayout.BeginHorizontal();
                if (CurrenciesKeys != null && CurrenciesKeys.Count != 0)
                {
                    SelectedCurrencyIndex = EditorGUILayout.Popup(SelectedCurrencyIndex, CurrenciesKeys.ToArray());
                    string defaultKey = CurrenciesKeys[SelectedCurrencyIndex];
                    if (GUILayout.Button("Add currency"))
                    {
                        if (Currencies == null)
                            Currencies = new Dictionary<string, uint>();
                        if (!Currencies.ContainsKey(defaultKey))
                            Currencies[defaultKey] = 0;
                    }
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("List of virtual currencies", MessageType.Info);

                // description
                GUILayout.Space(15);
                var descriptionTitle = new GUIStyle(GUI.skin.textField);
                descriptionTitle.wordWrap = true;
                EditorGUILayout.LabelField("Description");
                Description = EditorGUILayout.TextArea(Description, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });

                // draw customs properties
                GUILayout.Space(15);
                EditorGUILayout.LabelField("Custom Data");
                var rawData = CustomDataDrawer.Draw<CBSCurrencyPackCustomData>(DataAdapter);
                EditorGUILayout.HelpBox("You can create your own Custom Data. Check tutorial", MessageType.Info);

                // external url
                GUILayout.Space(15);
                ExternalUrl = EditorGUILayout.TextField("External URL", ExternalUrl);
                EditorGUILayout.HelpBox("You can use it for example for remote texture url", MessageType.Info);

                //item tag
                GUILayout.Space(15);
                ItemTag = EditorGUILayout.TextField("Tag", ItemTag);
                EditorGUILayout.HelpBox("Use this tag for sorting pack", MessageType.Info);

                // real money price
                GUILayout.Space(15);
                EditorGUILayout.LabelField("Real Money price");
                EditorUtils.DrawRealMoneyPrice(CurrentData);
                EditorGUILayout.HelpBox("Real money price in USD", MessageType.Info);

                // apply button
                GUILayout.Space(30);
                string buttonTitle = Action == CurrencyAction.ADD ? "Add" : "Save";
                if (GUILayout.Button(buttonTitle))
                {
                    if (!IsValidInputs())
                        return;
                    if (IconSprite == null)
                    {
                        Icons.RemoveSprite(ID);
                    }
                    else
                    {
                        Icons.SaveSprite(ID, IconSprite);
                    }
                    ApplyInputs();
                    AddCallback?.Invoke(CurrentData);
                    Hide();
                }

                EditorGUILayout.EndScrollView();
            }
        }
    }
}
#endif

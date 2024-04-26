#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using PlayFab.AdminModels;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddItemWindow : EditorWindow
    {
        private static Action<CatalogItem> AddCallback { get; set; }
        protected static CatalogItem CurrentData { get; set; }
        private static ItemAction Action { get; set; }

        private static ItemType ItemType { get; set; }

        private static List<string> Categories;
        protected static List<string> Currencies { get; set; }

        protected string[] Titles = new string[] { "Info", "Configs", "Linked Data", "Prices", "Upgrades" };
        protected string AddTitle = "Add Item";
        protected string SaveTitle = "Save Item";

        private static int CategoryAtStart { get; set; }

        private string ID { get; set; }
        private string DisplayName { get; set; }
        private string Description { get; set; }
        private string ExternalUrl { get; set; }
        private string ItemCategory { get; set; }
        private string RawCustomData { get; set; }
        private string ItemClass { get; set; }
        private Sprite IconSprite { get; set; }
        private GameObject LinkedPrefab { get; set; }
        private ScriptableObject LinkedScriptable { get; set; }
        private Dictionary<string, uint> Prices { get; set; }
        private bool IsStackable { get; set; }
        private bool IsTradable { get; set; }
        private bool IsConsumable { get; set; }
        private bool IsEquippable { get; set; }
        private bool HasLifeTime { get; set; }
        private bool IsRecipe { get; set; }

        private bool IsUpgradable
        {
            get
            {
                return ItemUpgrades != null && ItemUpgrades.Count > 0;
            }
        }

        private uint UsageCount { get; set; }
        private uint LifeTime { get; set; }

        private Vector2 ScrollPos { get; set; }

        private bool IsInited { get; set; } = false;

        private int SelectedCurrencyIndex { get; set; }

        private int SelectedCategoryIndex { get; set; }

        private int SelectedTypeIndex { get; set; }

        private int SelectedToolBar { get; set; }

        private ItemsIcons Icons { get; set; }
        private LinkedPrefabData PrefabData { get; set; }
        private LinkedScriptableData ScriptableData { get; set; }

        private List<Type> AllDataTypes = new List<Type>();

        private int MaxRawBytes = 1000;

        public List<string> TempStringList;
        public List<int> TempIntList;
        public List<float> TempFloatList;

        private CBSRecipeContainer Recipes { get; set; }
        private CBSItemUpgradesContainer Upgrades { get; set; }

        private ItemsDependencyDrawer ItemsDependencyDrawer { get; set; }
        private ItemRecipeDrawer ItemRecipeDrawer { get; set; }

        private List<CBSItemUpgradeState> ItemUpgrades { get; set; }
        private Dictionary<int, ItemUpgradeDrawer> ItemUpgradeDrawers { get; set; }

        public static void Show<T>(CatalogItem current, Action<CatalogItem> addCallback, ItemAction action, List<string> category, List<string> currencies, ItemType type, int categoryIndex) where T : EditorWindow
        {
            AddCallback = addCallback;
            CurrentData = current;
            Action = action;
            Categories = category;
            Currencies = currencies;
            ItemType = type;
            CategoryAtStart = categoryIndex;

            var window = ScriptableObject.CreateInstance<T>();
            window.maxSize = new Vector2(400, 700);
            window.minSize = window.maxSize;
            window.ShowUtility();
        }

        private void Hide()
        {
            this.Close();
        }

        protected virtual void Init()
        {
            AllDataTypes = AppDomain.CurrentDomain.GetAssemblies()
                       .SelectMany(assembly => assembly.GetTypes())
                       .Where(type => type.IsSubclassOf(typeof(CBSItemCustomData))).ToList();

            Icons = CBSScriptable.Get<ItemsIcons>();
            PrefabData = CBSScriptable.Get<LinkedPrefabData>();
            ScriptableData = CBSScriptable.Get<LinkedScriptableData>();
            Recipes = BaseConfigurator.Get<ItemsConfigurator>().Recipes;
            Upgrades = BaseConfigurator.Get<ItemsConfigurator>().Upgrades;

            ID = CurrentData.ItemId;
            DisplayName = CurrentData.DisplayName;
            Description = CurrentData.Description;
            ExternalUrl = CurrentData.ItemImageUrl;
            ItemClass = string.IsNullOrEmpty(CurrentData.ItemClass) ? "CBSDefaultData" : CurrentData.ItemClass;
            RawCustomData = CurrentData.CustomData;
            try
            {
                RawCustomData = Compressor.Decompress(RawCustomData);
            }
            catch { }
            RawCustomData = JsonPlugin.IsValidJson(RawCustomData) ? RawCustomData : JsonPlugin.EMPTY_JSON;
            var dataObject = JsonUtility.FromJson<CBSItemCustomData>(RawCustomData);
            IsRecipe = dataObject == null ? false : dataObject.IsRecipe;
            IsEquippable = dataObject == null ? false : dataObject.IsEquippable;
            Prices = CurrentData.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
            IsStackable = CurrentData.IsStackable;
            IsTradable = CurrentData.IsTradable;

            CurrentData.Consumable = CurrentData.Consumable ?? new CatalogItemConsumableInfo();

            IsConsumable = CurrentData.Consumable.UsageCount != null;
            UsageCount = IsConsumable ? (uint)CurrentData.Consumable.UsageCount : 1;
            HasLifeTime = CurrentData.Consumable.UsagePeriod != null;
            LifeTime = HasLifeTime ? (uint)CurrentData.Consumable.UsagePeriod : 5;

            bool tagExist = CurrentData.Tags != null && CurrentData.Tags.Count != 0;
            ItemCategory = tagExist ? CurrentData.Tags[0] : CBSConstants.UndefinedCategory;
            if (Action == ItemAction.ADD)
            {
                ItemCategory = Categories == null || ItemCategory.Length == 0 ? CBSConstants.UndefinedCategory : Categories[CategoryAtStart];
            }
            SelectedCategoryIndex = Categories.Contains(ItemCategory) ? Categories.IndexOf(ItemCategory) : 0;
            var itemClassType = AllDataTypes.Where(x => x.Name == ItemClass).FirstOrDefault();
            SelectedTypeIndex = AllDataTypes.IndexOf(itemClassType);

            IconSprite = Icons.GetSprite(ID);
            LinkedPrefab = PrefabData.GetLinkedData(ID);
            LinkedScriptable = ScriptableData.GetLinkedData(ID);
            ItemUpgrades = Upgrades.GetUpdgrades(ID);

            IsInited = true;
        }

        protected virtual void CheckInputs()
        {
            DrawInfo();

            CurrentData.ItemId = ID;
            CurrentData.DisplayName = DisplayName;
            CurrentData.Description = Description;
            CurrentData.ItemImageUrl = ExternalUrl;
            CurrentData.CustomData = RawCustomData;
            CurrentData.VirtualCurrencyPrices = Prices;
            CurrentData.IsStackable = IsStackable;
            CurrentData.IsTradable = IsTradable;
            if (IsConsumable)
            {
                CurrentData.Consumable.UsageCount = UsageCount;
            }
            else
            {
                CurrentData.Consumable.UsageCount = null;
            }

            if (HasLifeTime)
            {
                CurrentData.Consumable.UsagePeriod = LifeTime;
            }
            else
            {
                CurrentData.Consumable.UsagePeriod = null;
            }

            if (!string.IsNullOrEmpty(ItemCategory))
            {
                if (CurrentData.Tags == null)
                {
                    CurrentData.Tags = new List<string>();
                }
                ItemCategory = Categories[SelectedCategoryIndex];
                if (CurrentData.Tags.Count == 0)
                {
                    CurrentData.Tags.Add(ItemCategory);
                }
                else
                {
                    CurrentData.Tags[0] = ItemCategory;
                }
            }

            CurrentData.ItemClass = AllDataTypes[SelectedTypeIndex].Name;

            if (IsUpgradable)
            {
                Upgrades.AddOrUpdateUpgradeInfo(ID, ItemUpgrades);
            }
            else
            {
                Upgrades.RemoveUpgrade(ID);
            }
        }

        void OnGUI()
        {
            var titleStyle = new GUIStyle(GUI.skin.button);
            using (var areaScope = new GUILayout.AreaScope(new Rect(0, 0, 400, 700)))
            {
                ScrollPos = EditorGUILayout.BeginScrollView(ScrollPos);

                SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, Titles, titleStyle, GUI.ToolbarButtonSize.Fixed, GUILayout.Width(380));

                // init start values
                if (!IsInited)
                {
                    Init();
                }

                switch (SelectedToolBar)
                {
                    case 0:
                        DrawInfo();
                        break;
                    case 1:
                        DrawConfigs();
                        break;
                    case 2:
                        DrawLinkedData();
                        break;
                    case 3:
                        DrawPrices();
                        break;
                    case 4:
                        DrawUpgrades();
                        break;
                    default:
                        break;
                }

                // apply button
                GUILayout.FlexibleSpace();
                GUILayout.Space(30);
                string buttonTitle = Action == ItemAction.ADD ? AddTitle : SaveTitle;
                if (GUILayout.Button(buttonTitle))
                {
                    if (IsInputValid())
                    {
                        if (IconSprite == null)
                            Icons.RemoveSprite(ID);
                        else
                            Icons.SaveSprite(ID, IconSprite);

                        if (LinkedPrefab == null)
                            PrefabData.RemoveAsset(ID);
                        else
                            PrefabData.SaveAssetData(ID, LinkedPrefab);

                        if (LinkedScriptable == null)
                            ScriptableData.RemoveAsset(ID);
                        else
                            ScriptableData.SaveAssetData(ID, LinkedScriptable);
                        CheckInputs();
                        AddCallback?.Invoke(CurrentData);
                        Hide();
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawLinkedData()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            GUILayout.Space(10);
            // draw icon
            EditorGUILayout.LabelField("Sprite", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            IconSprite = (Sprite)EditorGUILayout.ObjectField((IconSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            EditorGUILayout.HelpBox("Sprite for game item. ATTENTION! The sprite is not saved on the server, it will be included in the build", MessageType.Info);

            // draw preview
            var iconTexture = IconSprite == null ? null : IconSprite.texture;
            GUILayout.Button(iconTexture, GUILayout.Width(100), GUILayout.Height(100));

            // draw prefab
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Prefab", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            LinkedPrefab = (GameObject)EditorGUILayout.ObjectField((LinkedPrefab as UnityEngine.Object), typeof(GameObject), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            EditorGUILayout.HelpBox("Prefab for game item. ATTENTION! The prefab is not saved on the server, it will be included in the build", MessageType.Info);

            // draw scriptable
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Scriptable", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            LinkedScriptable = (ScriptableObject)EditorGUILayout.ObjectField((LinkedScriptable as UnityEngine.Object), typeof(ScriptableObject), false, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            EditorGUILayout.HelpBox("Scriptable data for game item. ATTENTION! The data is not saved on the server, it will be included in the build", MessageType.Info);

            // external url
            GUILayout.Space(10);
            EditorGUILayout.LabelField("External Icon URL", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
            ExternalUrl = EditorGUILayout.TextField(ExternalUrl);
            EditorGUILayout.HelpBox("You can use it for example for remote texture url", MessageType.Info);
        }

        private void DrawInfo()
        {
            GUILayout.Space(15);
            if (Action == ItemAction.ADD)
            {
                ID = EditorGUILayout.TextField("Item ID", ID);
            }
            if (Action == ItemAction.EDIT)
            {
                EditorGUILayout.LabelField("Item ID", ID);
            }
            EditorGUILayout.HelpBox("Unique id for item.", MessageType.Info);

            GUILayout.Space(15);
            DisplayName = EditorGUILayout.TextField("Title", DisplayName);
            EditorGUILayout.HelpBox("Full name of the item", MessageType.Info);

            // description
            GUILayout.Space(15);
            var descriptionTitle = new GUIStyle(GUI.skin.textField);
            descriptionTitle.wordWrap = true;
            EditorGUILayout.LabelField("Description");
            Description = EditorGUILayout.TextArea(Description, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });

            //item tag
            GUILayout.Space(15);
            EditorGUILayout.LabelField("Category");
            SelectedCategoryIndex = EditorGUILayout.Popup(SelectedCategoryIndex, Categories.ToArray());
            EditorGUILayout.HelpBox("Item category", MessageType.Info);

            // draw custom data
            GUILayout.Space(15);
            EditorGUILayout.LabelField("Custom Data");
            // draw raw data progress bar
            try
            {
                RawCustomData = Compressor.Decompress(RawCustomData);
            }
            catch { }
            RawCustomData = JsonPlugin.IsValidJson(RawCustomData) ? RawCustomData : JsonPlugin.EMPTY_JSON;
            int byteCount = System.Text.Encoding.UTF8.GetByteCount(RawCustomData);
            float difValue = (float)byteCount / (float)MaxRawBytes;
            string progressTitle = byteCount.ToString() + "/" + MaxRawBytes.ToString() + " bytes";
            float lastY = GUILayoutUtility.GetLastRect().y;
            var lastColor = GUI.color;
            if (byteCount > MaxRawBytes)
            {
                GUI.color = Color.red;
            }
            EditorGUI.ProgressBar(new Rect(3, lastY + 25, position.width - 6, 20), difValue, progressTitle);
            GUI.color = lastColor;
            // draw data
            GUILayout.Space(35);
            SelectedTypeIndex = EditorGUILayout.Popup(SelectedTypeIndex, AllDataTypes.Select(x => x.Name).ToArray());
            var selectedType = AllDataTypes[SelectedTypeIndex];
            var dataObject = JsonUtility.FromJson(RawCustomData, selectedType);
            if (dataObject == null)
            {
                dataObject = Activator.CreateInstance(selectedType);
            }
            foreach (var f in selectedType.GetFields().Where(f => f.IsPublic))
            {
                // set item type
                if (f.Name == "ItemType")
                {
                    f.SetValue(dataObject, ItemType);
                }
                // set consumable
                else if (f.Name == "IsConsumable")
                {
                    f.SetValue(dataObject, IsConsumable);
                }
                // set stackable
                else if (f.Name == "IsStackable")
                {
                    f.SetValue(dataObject, IsStackable);
                }
                // set consumable
                else if (f.Name == "IsTradable")
                {
                    f.SetValue(dataObject, IsTradable);
                }
                // set equipable
                else if (f.Name == "IsEquippable")
                {
                    f.SetValue(dataObject, IsEquippable);
                }
                // set recipe
                else if (f.Name == "IsRecipe")
                {
                    f.SetValue(dataObject, IsRecipe);
                }
                // draw string
                else if (f.FieldType == typeof(string))
                {
                    string stringTitle = f.Name;
                    string stringValue = f.GetValue(dataObject) == null ? string.Empty : f.GetValue(dataObject).ToString();
                    var text = EditorGUILayout.TextField(stringTitle, stringValue);
                    f.SetValue(dataObject, text);
                }
                // draw int
                else if (f.FieldType == typeof(int))
                {
                    string stringTitle = f.Name;
                    int intValue = (int)f.GetValue(dataObject);
                    var i = EditorGUILayout.IntField(stringTitle, intValue);
                    f.SetValue(dataObject, i);
                }
                // draw float
                else if (f.FieldType == typeof(float))
                {
                    string stringTitle = f.Name;
                    float floatValue = (float)f.GetValue(dataObject);
                    var fl = EditorGUILayout.FloatField(stringTitle, floatValue);
                    f.SetValue(dataObject, fl);
                }
                // draw bool
                else if (f.FieldType == typeof(bool))
                {
                    string stringTitle = f.Name;
                    bool boolValue = (bool)f.GetValue(dataObject);
                    var b = EditorGUILayout.Toggle(stringTitle, boolValue);
                    f.SetValue(dataObject, b);
                }
                // draw enum
                else if (f.FieldType.IsEnum)
                {
                    var enumType = f.FieldType;
                    var enumList = Enum.GetNames(enumType);
                    string stringTitle = f.Name;
                    int enumValue = (int)f.GetValue(dataObject);
                    var e = EditorGUILayout.Popup(enumValue, enumList);
                    f.SetValue(dataObject, e);
                }
                else if (IsSupportedList(f, dataObject))
                {
                    var listName = f.Name;
                    var objValue = f.GetValue(dataObject);
                    IList tempList;
                    GetTempList(objValue, out tempList);
                    var tempListName = GetTempListName(objValue);
                    ScriptableObject target = this;
                    SerializedObject so = new SerializedObject(target);
                    SerializedProperty stringsProperty = so.FindProperty(tempListName);

                    EditorGUILayout.PropertyField(stringsProperty, new GUIContent(listName), true); // True means show children
                    so.ApplyModifiedProperties(); // Remember to apply modified properties*/

                    f.SetValue(dataObject, tempList);
                }
            }
            RawCustomData = JsonUtility.ToJson(dataObject);
            RawCustomData = Compressor.Compress(RawCustomData);

            // draw info
            EditorGUILayout.HelpBox(string.Format("To create your own 'Custom Data' - create a new class, inherit from the class '{0}' and your class will automatically appear in the drop-down list. Only fields and the following data types are supported - 'int', 'float', 'string', 'enum', 'List<string>', 'List<float>', 'List<int>'", "CBSItemCustomData"), MessageType.Info);
        }

        protected virtual void DrawConfigs()
        {
            GUILayout.Space(15);
            // draw consumable
            EditorGUILayout.LabelField("Is Consumable");
            IsConsumable = EditorGUILayout.Toggle(IsConsumable);
            EditorGUILayout.HelpBox("Determines if item can be used. For example use Consumable option for potions. For static items - disable this option (Sword, armor, shield, etc.). Consumable cant be equippable", MessageType.Info);
            if (IsConsumable)
            {
                IsEquippable = false;
                GUILayout.Space(15);
                // draw uage count
                EditorGUILayout.LabelField("Usage Count");
                UsageCount = (uint)EditorGUILayout.IntField((int)UsageCount, GUILayout.Width(150));
                EditorGUILayout.HelpBox("Determines how many times the item can be used. After use, it is automatically removed from the invertoty. The value cannot be less than 1.", MessageType.Info);
                if (UsageCount < 1)
                    UsageCount = 1;
            }
            GUILayout.Space(15);
            // draw stackable
            EditorGUILayout.LabelField("Is Stackable");
            IsStackable = EditorGUILayout.Toggle(IsStackable);
            if (IsStackable)
            {
                IsTradable = false;
                IsEquippable = false;
            }

            EditorGUILayout.HelpBox("Determines if the item can be collected in one stack in the inventory. Stackable cant be tradable", MessageType.Info);
            GUILayout.Space(15);
            // draw equippable
            EditorGUILayout.LabelField("Is Equippable");
            IsEquippable = EditorGUILayout.Toggle(IsEquippable);
            if (IsEquippable)
            {
                IsConsumable = false;
                IsStackable = false;
            }
            EditorGUILayout.HelpBox("Determines if the item can be equip to the user/character. IsEquippable cant be consumable or stackable", MessageType.Info);
            GUILayout.Space(15);
            // draw tradable
            EditorGUILayout.LabelField("Is Tradable");
            IsTradable = EditorGUILayout.Toggle(IsTradable);
            if (IsTradable)
                IsStackable = false;
            EditorGUILayout.HelpBox("Determines if a player can trade this item with other players. Tradable cant be stackable", MessageType.Info);
            GUILayout.Space(15);
            // draw has life time
            EditorGUILayout.LabelField("Has Lifetime");
            HasLifeTime = EditorGUILayout.Toggle(HasLifeTime);
            EditorGUILayout.HelpBox("Determines if the item has a lifetime. The countdown begins after the item enters the invertony. After the passage of time - the item is automatically deleted.", MessageType.Info);

            if (HasLifeTime)
            {
                GUILayout.Space(15);
                // draw uage count
                EditorGUILayout.LabelField("Life time in seconds");
                LifeTime = (uint)EditorGUILayout.IntField((int)LifeTime, GUILayout.Width(150));
                EditorGUILayout.HelpBox("Lifetime of the item. Cannot be less than 5.", MessageType.Info);
                if (LifeTime < 5)
                    LifeTime = 5;
            }
            // draw recipe
            EditorGUILayout.LabelField("Is Recipe");
            IsRecipe = EditorGUILayout.Toggle(IsRecipe);
            if (string.IsNullOrEmpty(ID))
            {
                IsRecipe = false;
                EditorGUILayout.HelpBox("Item ID can not be empt", MessageType.Error);
            }
            EditorGUILayout.HelpBox("Determines if the item is recipe for creating another item. Recipe cant be consumable, equippable or stackable", MessageType.Info);
            if (IsRecipe)
            {
                IsConsumable = false;
                IsStackable = false;
                IsEquippable = false;
                if (!Recipes.HasRecipe(ID))
                {
                    Recipes.AddOrUpdateRecipe(ID, new CBSItemRecipe());
                }

                var recipe = Recipes.GetRecipe(ID);

                if (ItemsDependencyDrawer == null)
                {
                    var fabItems = BaseConfigurator.Get<ItemsConfigurator>().Items;
                    ItemsDependencyDrawer = new ItemsDependencyDrawer(recipe, Categories, fabItems, Currencies, this);
                }
                if (ItemRecipeDrawer == null)
                {
                    ItemRecipeDrawer = new ItemRecipeDrawer(recipe, ItemsDependencyDrawer);
                }

                ItemRecipeDrawer.Draw();
            }
            else
            {
                Recipes.RemoveRecipe(ID);
            }
        }

        private void DrawPrices()
        {
            if (Currencies != null && Currencies.Count != 0)
            {
                // draw currencies list
                GUILayout.Space(15);
                var contentTitle = new GUIStyle(GUI.skin.label);
                contentTitle.fontStyle = FontStyle.Bold;
                EditorGUILayout.LabelField("List of prices", contentTitle);

                if (Prices != null && Prices.Count != 0)
                {
                    for (int i = 0; i < Prices.Count; i++)
                    {
                        string key = Prices.Keys.ElementAt(i);
                        int val = (int)Prices.Values.ElementAt(i);
                        GUILayout.BeginHorizontal();
                        Prices[key] = (uint)EditorGUILayout.IntField(key, val);

                        if (GUILayout.Button("X", GUILayout.Width(20)))
                        {
                            Prices.Remove(key);
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
                        if (!Prices.ContainsKey(defaultKey))
                            Prices[defaultKey] = 0;
                    }
                }
                GUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("List of virtual currencies", MessageType.Info);
            }
            // real money price
            GUILayout.Space(15);
            EditorGUILayout.LabelField("Real Money price");
            EditorUtils.DrawRealMoneyPrice(CurrentData);
            EditorGUILayout.HelpBox("Real money price in USD", MessageType.Info);
        }

        public void DrawUpgrades()
        {
            if (ItemUpgrades != null)
            {
                ItemUpgradeDrawers = ItemUpgradeDrawers ?? new Dictionary<int, ItemUpgradeDrawer>();
                for (int i = 0; i < ItemUpgrades.Count; i++)
                {
                    var upgradeInstance = ItemUpgrades[i];
                    if (!ItemUpgradeDrawers.ContainsKey(i))
                    {
                        var fabItems = BaseConfigurator.Get<ItemsConfigurator>().Items;
                        var itemDependencyDrawer = new ItemsDependencyDrawer(upgradeInstance, Categories, fabItems, Currencies, this);
                        ItemUpgradeDrawers[i] = new ItemUpgradeDrawer(i, upgradeInstance, itemDependencyDrawer);
                    }
                    ItemUpgradeDrawers[i].Draw();
                    GUILayout.Space(3);
                }
            }

            GUILayout.Space(5);
            if (ItemUpgrades != null && ItemUpgrades.Count > 0)
            {
                if (EditorUtils.DrawButton("Remove last upgrade", Color.red, 12))
                {
                    ItemUpgrades = ItemUpgrades ?? new List<CBSItemUpgradeState>();
                    var upgradeCount = ItemUpgrades.Count;
                    if (upgradeCount == 0)
                        return;
                    ItemUpgrades.RemoveAt(upgradeCount - 1);
                    ItemUpgradeDrawers.Remove(upgradeCount - 1);
                }
            }
            GUILayout.Space(3);
            if (EditorUtils.DrawButton("Add new upgrade", Color.green, 12))
            {
                ItemUpgrades = ItemUpgrades ?? new List<CBSItemUpgradeState>();
                var upgradeCount = (uint)ItemUpgrades.Count;
                ItemUpgrades.Add(new CBSItemUpgradeState());
            }
        }

        private bool IsInputValid()
        {
            int byteCount = System.Text.ASCIIEncoding.Unicode.GetByteCount(RawCustomData);

            return byteCount < MaxRawBytes && IsRecipeValid() && IsUpgradesValid();
        }

        private bool IsRecipeValid()
        {
            if (IsRecipe && ItemRecipeDrawer != null)
            {
                return ItemRecipeDrawer.IsValid();
            }
            return true;
        }

        private bool IsUpgradesValid()
        {
            if (IsUpgradable && ItemUpgradeDrawers != null)
            {
                return !ItemUpgradeDrawers.Select(x => x.Value).Any(x => !x.IsValid());
            }
            return true;
        }

        private bool IsSupportedList(FieldInfo f, object target)
        {
            return f.FieldType.IsGenericType && (f.GetValue(target) is List<string> || f.GetValue(target) is List<int> || f.GetValue(target) is List<float>);
        }

        private dynamic GetTempList(object target, out IList targetlist)
        {
            if (target is List<string>)
            {
                TempStringList = target as List<string>;
                targetlist = TempStringList;
                return TempStringList;
            }
            if (target is List<int>)
            {
                TempIntList = target as List<int>;
                targetlist = TempIntList;
                return TempIntList;
            }
            if (target is List<float>)
            {
                TempFloatList = target as List<float>;
                targetlist = TempFloatList;
                return TempFloatList;
            }
            targetlist = null;
            return null;
        }

        private string GetTempListName(object target)
        {
            if (target is List<string>)
                return "TempStringList";
            if (target is List<int>)
                return "TempIntList";
            if (target is List<float>)
                return "TempFloatList";
            return null;
        }
    }
}
#endif
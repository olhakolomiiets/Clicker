#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.AdminModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Action = System.Action;

namespace CBS.Editor
{
    public class StoreConfigurator : BaseConfigurator
    {
        protected override string Title => "Store Configurator";

        private int SelectedToolBar { get; set; }

        private GUILayoutOption[] AddButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(120) };
            }
        }

        private GUILayoutOption[] UpdateButtonOptions
        {
            get
            {
                return new GUILayoutOption[] { GUILayout.Height(30), GUILayout.Width(220) };
            }
        }

        private List<CatalogItem> CachedItems { get; set; }
        private Dictionary<string, CatalogItem> ItemsDitcionary { get; set; }
        private Categories CachedItemCategories { get; set; }
        private Categories CachedPacksCategories { get; set; }
        private Categories CachedLootBoxCategories { get; set; }

        protected override bool DrawScrollView => true;

        private Vector2 CurrencyScroll { get; set; }
        private Vector2 PackScroll { get; set; }

        private bool IsStoreLoaded { get; set; }
        private StoresContainer Container { get; set; }
        private EditorData EditorData { get; set; }
        private StoreIcons StoreIcons { get; set; }
        private ItemsIcons ItemsIcons { get; set; }

        private Dictionary<string, GetStoreItemsResult> StoreResults;

        private Texture2D StoreEnabledTitleTex;
        private Texture2D StoreDisabledTitleTex;
        private Texture2D StoreContentTex;

        private Dictionary<string, Vector2> ScrollRect;

        private GetStoreItemsResult StoreToEdit { get; set; }
        private GetStoreItemsResult GlobalOfferStore { get; set; }
        private GetStoreItemsResult ProfileOfferStore { get; set; }

        private string TitleEntityToken;

        private ObjectCustomDataDrawer<CBSStoreCustomData> CustomDataDrawer { get; set; }
        private Dictionary<string, ObjectCustomDataDrawer<CBSStoreItemCustomData>> CustomDrawerPool;

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            StoreIcons = CBSScriptable.Get<StoreIcons>();
            ItemsIcons = CBSScriptable.Get<ItemsIcons>();
            StoreResults = new Dictionary<string, GetStoreItemsResult>();
            ScrollRect = new Dictionary<string, Vector2>();
            IsStoreLoaded = false;
            StoreEnabledTitleTex = EditorUtils.MakeColorTexture(EditorData.StoreEnabledTitle);
            StoreDisabledTitleTex = EditorUtils.MakeColorTexture(EditorData.StoreDisabledTitle);
            StoreContentTex = EditorUtils.MakeColorTexture(EditorData.StoreContent);
            CustomDataDrawer = new ObjectCustomDataDrawer<CBSStoreCustomData>(PlayfabUtils.DEFAULT_CUSTOM_DATA_SIZE, 830f);
            CustomDataDrawer.AutoReset = false;
            CustomDrawerPool = new Dictionary<string, ObjectCustomDataDrawer<CBSStoreItemCustomData>>();
            StoreToEdit = null;
            LoadStores();
        }

        protected override void OnDrawInside()
        {
            if (StoreToEdit == null)
            {
                // draw sub titles
                var lastSavedIndex = SelectedToolBar;
                SelectedToolBar = GUILayout.Toolbar(SelectedToolBar, new string[] { "Store", "Global special offers", "Profile special offers" });
                switch (SelectedToolBar)
                {
                    case 0:
                        DrawStores();
                        break;
                    case 1:
                        DrawGlobalOffers();
                        break;
                    case 2:
                        DrawProfileOffers();
                        break;
                    default:
                        break;
                }
                if (lastSavedIndex != SelectedToolBar)
                {
                    if (SelectedToolBar == 1)
                    {
                        // load global offers
                        LoadGlobalOffers();
                    }
                    else
                    {
                        GlobalOfferStore = null;
                    }

                    if (SelectedToolBar == 2)
                    {
                        // load profile offers
                        LoadProfileOffers();
                    }
                    else
                    {
                        ProfileOfferStore = null;
                    }
                }
            }
            else
            {
                DrawStoreToEdit();
            }
        }

        private void DrawStoreToEdit()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var storeMeta = StoreToEdit.MarketingData.Metadata;
            var storeMetaRaw = storeMeta == null ? JsonPlugin.EMPTY_JSON : storeMeta.ToString();
            var storeData = JsonPlugin.FromJson<CBSStoreMeta>(storeMetaRaw);

            var isEnabled = storeData.Enable;

            // draw buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Cancel", EditorData.EditColor, 12, AddButtonOptions))
            {
                StoreToEdit = null;
                GUIUtility.ExitGUI();
            }
            GUILayout.Space(5);
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, AddButtonOptions))
            {
                SaveStore(StoreToEdit);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // draw id
            EditorGUILayout.LabelField("Store ID", titleStyle);
            EditorGUILayout.LabelField(StoreToEdit.StoreId);
            GUILayout.Space(10);

            // draw enable
            EditorGUILayout.LabelField("Enable?", titleStyle);
            storeData.Enable = EditorGUILayout.Toggle(storeData.Enable);
            EditorGUILayout.HelpBox("Determines the general availability of the store for players.", MessageType.Info);
            GUILayout.Space(10);

            // draw name
            EditorGUILayout.LabelField("Display Name", titleStyle);
            StoreToEdit.MarketingData.DisplayName = EditorGUILayout.TextField(StoreToEdit.MarketingData.DisplayName, new GUILayoutOption[] { GUILayout.Width(400) });
            GUILayout.Space(10);

            // draw description
            var descriptionTitle = new GUIStyle(GUI.skin.textField);
            descriptionTitle.wordWrap = true;
            EditorGUILayout.LabelField("Description", titleStyle);
            StoreToEdit.MarketingData.Description = EditorGUILayout.TextArea(StoreToEdit.MarketingData.Description, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(150) });
            GUILayout.Space(10);

            // draw customs properties
            EditorGUILayout.LabelField("Custom Data", titleStyle);
            var rawData = CustomDataDrawer.Draw(storeData);
            GUILayout.Space(10);

            // clan limit
            EditorGUILayout.LabelField("Clan limit?", titleStyle);
            storeData.HasClanLimit = EditorGUILayout.Toggle(storeData.HasClanLimit);
            EditorGUILayout.HelpBox("Determines whether the store will be available only to clan members.", MessageType.Info);
            GUILayout.Space(10);

            // level limit
            EditorGUILayout.LabelField("Level limit?", titleStyle);
            storeData.HasLevelLimit = EditorGUILayout.Toggle(storeData.HasLevelLimit);
            if (storeData.HasLevelLimit)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Level", GUILayout.Width(100));
                storeData.LevelLimit = EditorGUILayout.IntField(storeData.LevelLimit, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Level Filter", GUILayout.Width(100));
                storeData.LevelFilter = (IntFilter)EditorGUILayout.EnumPopup(storeData.LevelFilter, new GUILayoutOption[] { GUILayout.Width(150) });
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Allows you to limit the store to players at a certain level.", MessageType.Info);
            GUILayout.Space(10);

            // statistic limit
            EditorGUILayout.LabelField("Statistic limit?", titleStyle);
            storeData.HasStatisticLimit = EditorGUILayout.Toggle(storeData.HasStatisticLimit);
            if (storeData.HasStatisticLimit)
            {
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Statistic Name", GUILayout.Width(100));
                storeData.StatisticLimitName = EditorGUILayout.TextField(storeData.StatisticLimitName, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Statistic Value", GUILayout.Width(100));
                storeData.StatisticLimitValue = EditorGUILayout.IntField(storeData.StatisticLimitValue, GUILayout.Width(50));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Statistic Filter", GUILayout.Width(100));
                storeData.StatisticFilter = (IntFilter)EditorGUILayout.EnumPopup(storeData.StatisticFilter, new GUILayoutOption[] { GUILayout.Width(150) });
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Allows you to limit the store to players at a certain statistic value.", MessageType.Info);
            GUILayout.Space(20);

            // store content title
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Store Content", titleStyle);
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Add Item", EditorData.AddColor, 12, AddButtonOptions))
            {
                ShowAddStoreItemDialog(onAdd =>
                {
                    var itemStore = onAdd;
                    StoreToEdit.Store = StoreToEdit.Store ?? new List<StoreItem>();
                    StoreToEdit.Store.Add(itemStore);
                });
            }
            GUILayout.EndHorizontal();

            if (StoreToEdit.Store == null || StoreToEdit.Store.Count == 0)
            {
                EditorGUILayout.HelpBox("PlayFab store requires at least one item to create a store.", MessageType.Error);
                return;
            }

            GUIStyle storeEnabledTitleStyle = new GUIStyle("HelpBox");
            storeEnabledTitleStyle.normal.background = StoreEnabledTitleTex;

            GUIStyle storeDisabledTitleStyle = new GUIStyle("HelpBox");
            storeDisabledTitleStyle.normal.background = StoreDisabledTitleTex;

            GUIStyle storeContentStyle = new GUIStyle("HelpBox");
            storeContentStyle.normal.background = StoreContentTex;

            GUILayout.Space(20);

            var storeList = StoreToEdit.Store;

            DrawStoreList(storeList, false, false);

            // save data
            StoreToEdit.MarketingData.Metadata = JsonPlugin.ToJson(storeData);
            StoreToEdit.Store = storeList;
        }

        private void DrawStoreList(List<StoreItem> storeList, bool globalOffer, bool profileOffer)
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            var descriptionTitle = new GUIStyle(GUI.skin.textField);
            descriptionTitle.wordWrap = true;

            GUIStyle storeEnabledTitleStyle = new GUIStyle("HelpBox");
            storeEnabledTitleStyle.normal.background = StoreEnabledTitleTex;

            GUIStyle storeDisabledTitleStyle = new GUIStyle("HelpBox");
            storeDisabledTitleStyle.normal.background = StoreDisabledTitleTex;

            GUIStyle storeContentStyle = new GUIStyle("HelpBox");
            storeContentStyle.normal.background = StoreContentTex;

            for (int i = 0; i < storeList.Count; i++)
            {
                var storeItem = storeList[i];
                var itemID = storeItem.ItemId;
                var slotRawData = storeItem.CustomData == null ? JsonPlugin.EMPTY_JSON : storeItem.CustomData.ToString();
                var metaData = new CBSStoreItemMeta();
                try
                {
                    metaData = JsonPlugin.FromJsonDecompress<CBSStoreItemMeta>(slotRawData);
                }
                catch
                {
                    metaData = JsonPlugin.FromJson<CBSStoreItemMeta>(slotRawData);
                }
                var itemExist = ItemsDitcionary.ContainsKey(itemID);

                if (!itemExist)
                {
                    storeList.RemoveAt(i);
                    continue;
                }
                var originItem = ItemsDitcionary[itemID];

                var slotDisplayName = metaData.SlotDisplayName;
                var slotDespription = metaData.Description;
                var isSlotEnabled = profileOffer ? true : metaData.Enable;
                var originalPrice = originItem.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
                var overridePrice = storeItem.VirtualCurrencyPrices ?? new Dictionary<string, uint>();
                var discounts = metaData.Discounts ?? new Dictionary<string, int>();
                var overridePosition = storeItem.DisplayPosition != null;
                var hasQuantityLimit = metaData.HasQuantityLimit;

                // check custom drawer
                if (!CustomDrawerPool.ContainsKey(itemID))
                {
                    CustomDrawerPool[itemID] = new ObjectCustomDataDrawer<CBSStoreItemCustomData>(PlayfabUtils.ITEM_CUSTOM_DATA_SIZE, 500f);
                    CustomDrawerPool[itemID].ProgressBarXOfseet = 10;
                    CustomDrawerPool[itemID].AutoReset = false;
                    CustomDrawerPool[itemID].Reset();
                }
                var customDataDrawer = CustomDrawerPool[itemID];

                // draw title
                using (var horizontalScope = new GUILayout.VerticalScope(isSlotEnabled ? storeEnabledTitleStyle : storeDisabledTitleStyle))
                {
                    // draw status
                    GUILayout.BeginHorizontal();
                    var statusColor = isSlotEnabled ? Color.green : Color.red;
                    EditorUtils.DrawButton(string.Empty, statusColor, 1, new GUILayoutOption[] { GUILayout.Width(30), GUILayout.Height(30) });
                    EditorGUILayout.LabelField(slotDisplayName, titleStyle);

                    if (globalOffer)
                    {
                        if (isSlotEnabled)
                        {
                            var tickNegative = false;
                            if (metaData.HasDuration)
                            {
                                var endDate = metaData.EndDate;
                                if (endDate != null)
                                {
                                    var localTime = DateTime.UtcNow;
                                    var endLocalTime = endDate.GetValueOrDefault();
                                    var timeSpan = endLocalTime.Subtract(localTime);
                                    tickNegative = timeSpan.Ticks <= 0;
                                    var totalDays = (int)timeSpan.TotalDays;
                                    var timeString = timeSpan.ToString(DateUtils.StoreTimerFormat);
                                    var sBuilder = new StringBuilder();
                                    if (tickNegative)
                                    {
                                        sBuilder.Append("Processing... ");
                                    }
                                    else
                                    {
                                        sBuilder.Append("Special offer end in ");
                                        sBuilder.Append(totalDays > 0 ? totalDays + " Days " : string.Empty);
                                        sBuilder.Append(timeString);
                                    }
                                    var dateTitle = sBuilder.ToString();
                                    EditorGUILayout.LabelField(dateTitle, titleStyle);
                                }
                            }
                            if (tickNegative)
                            {
                                if (EditorUtils.DrawButton("Update", EditorData.AddPrizeColor, 12, AddButtonOptions))
                                {
                                    LoadGlobalOffers();
                                }
                            }
                            else
                            {
                                if (EditorUtils.DrawButton("Stop offer", EditorData.RemoveColor, 12, AddButtonOptions))
                                {
                                    StopSpecialOffer(itemID);
                                }
                            }
                        }
                        else
                        {
                            if (EditorUtils.DrawButton("Start offer", EditorData.AddPrizeColor, 12, AddButtonOptions))
                            {
                                StartSpecialOffer(itemID);
                            }
                            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, AddButtonOptions))
                            {
                                SaveStore(GlobalOfferStore);
                            }
                            if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, AddButtonOptions))
                            {
                                string questionsText = string.Format("Are you sure you want to remove offer {0}?", slotDisplayName);
                                int option = EditorUtility.DisplayDialogComplex("Warning",
                                    questionsText,
                                    "Yes",
                                    "No",
                                    string.Empty);
                                switch (option)
                                {
                                    // ok.
                                    case 0:
                                        storeList.RemoveAt(i);
                                        SaveStore(GlobalOfferStore);
                                        break;
                                }
                            }
                        }
                    }
                    else
                    {
                        if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, AddButtonOptions))
                        {
                            string questionsText = string.Format("Are you sure you want to remove store slot {0}?", slotDisplayName);
                            int option = EditorUtility.DisplayDialogComplex("Warning",
                                questionsText,
                                "Yes",
                                "No",
                                string.Empty);
                            switch (option)
                            {
                                // ok.
                                case 0:
                                    storeList.RemoveAt(i);
                                    if (globalOffer)
                                    {
                                        SaveStore(GlobalOfferStore);
                                    }
                                    break;
                            }
                        }
                    }

                    GUILayout.EndHorizontal();
                }

                EditorGUI.BeginDisabledGroup(globalOffer && isSlotEnabled);

                using (var horizontalScope = new GUILayout.VerticalScope(storeContentStyle))
                {
                    GUILayout.BeginHorizontal();
                    // draw icon
                    var itemSprite = StoreIcons.GetSprite(itemID);
                    var texture = itemSprite == null ? null : itemSprite.texture;
                    GUILayout.Button(texture, new GUILayoutOption[] { GUILayout.Width(150), GUILayout.Height(150) });
                    // draw slot name
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Slot Display Name", titleStyle);
                    slotDisplayName = EditorGUILayout.TextField(slotDisplayName, new GUILayoutOption[] { GUILayout.Width(300) });
                    // draw icon
                    EditorGUILayout.LabelField("Slot Icon", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(150) });
                    itemSprite = (Sprite)EditorGUILayout.ObjectField((itemSprite as UnityEngine.Object), typeof(Sprite), false, new GUILayoutOption[] { GUILayout.Width(150) });
                    StoreIcons.SaveSprite(itemID, itemSprite);
                    // draw target
                    EditorGUILayout.LabelField("Target to sell", titleStyle);
                    GUILayout.BeginHorizontal();
                    var targetSprite = ItemsIcons.GetSprite(itemID);
                    var itemTexture = targetSprite == null ? null : targetSprite.texture;
                    GUILayout.Button(itemTexture, new GUILayoutOption[] { GUILayout.Width(50), GUILayout.Height(50) });
                    EditorGUILayout.LabelField(itemID);
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                    // draw description
                    descriptionTitle.wordWrap = true;
                    GUILayout.BeginVertical();
                    EditorGUILayout.LabelField("Description", titleStyle);
                    slotDespription = EditorGUILayout.TextArea(slotDespription, descriptionTitle, new GUILayoutOption[] { GUILayout.Height(130), GUILayout.Width(570) });
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    // draw enable
                    if (!globalOffer && !profileOffer)
                    {
                        EditorGUILayout.LabelField("Enable?", titleStyle);
                        isSlotEnabled = EditorGUILayout.Toggle(isSlotEnabled);
                        EditorGUILayout.HelpBox("Determines the general availability of the slot for store.", MessageType.Info);
                    }
                    GUILayout.Space(10);

                    // draw price
                    bool currenciesExist = originalPrice != null && originalPrice.Count > 0;
                    GUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Origin Price", titleStyle, GUILayout.Width(200));
                    EditorGUILayout.LabelField("Enable/Override for Store?", titleStyle, GUILayout.Width(250));
                    GUILayout.Space(70);
                    EditorGUILayout.LabelField("Store price", titleStyle, GUILayout.Width(325));
                    EditorGUILayout.LabelField("Discount", titleStyle, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                    GUILayout.Space(10);

                    if (currenciesExist)
                    {
                        var curList = originalPrice;
                        foreach (var currency in curList)
                        {
                            GUILayout.BeginHorizontal();

                            // draw origin price
                            var currencyKey = currency.Key;
                            if (currencyKey == PlayfabUtils.REAL_MONEY_CODE)
                            {
                                var curTexture = ResourcesUtils.GetRealMoneyImage();
                                GUILayout.Button(curTexture, GUILayout.Width(50), GUILayout.Height(50));
                                EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                EditorGUILayout.LabelField(originItem.GetRMPriceString(), GUILayout.Width(190));
                            }
                            else
                            {
                                var curSprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(currency.Key);
                                var curTexture = curSprite == null ? null : curSprite.texture;
                                GUILayout.Button(curTexture, GUILayout.Width(50), GUILayout.Height(50));
                                EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                EditorGUILayout.LabelField(currency.Value.ToString(), GUILayout.Width(190));
                            }

                            var isOverriden = overridePrice.ContainsKey(currencyKey);
                            var lastOverrideState = isOverriden;
                            isOverriden = EditorGUILayout.Toggle(isOverriden, GUILayout.Width(250));
                            if (lastOverrideState != isOverriden)
                            {
                                if (isOverriden)
                                {
                                    if (!overridePrice.ContainsKey(currencyKey))
                                    {
                                        overridePrice[currencyKey] = currency.Value;
                                    }
                                }
                                else
                                {
                                    if (overridePrice.ContainsKey(currencyKey))
                                        overridePrice.Remove(currencyKey);
                                    if (discounts.ContainsKey(currencyKey))
                                        discounts.Remove(currencyKey);
                                }
                            }

                            if (isOverriden)
                            {
                                // draw overriden price
                                if (currencyKey == PlayfabUtils.REAL_MONEY_CODE)
                                {
                                    var curTexture = ResourcesUtils.GetRealMoneyImage();
                                    GUILayout.Button(curTexture, GUILayout.Width(50), GUILayout.Height(50));
                                    EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                    overridePrice[currencyKey] = (uint)EditorGUILayout.IntField((int)overridePrice[currencyKey], GUILayout.Width(100));
                                    EditorGUILayout.LabelField(storeItem.GetRMOverridePriceString(), GUILayout.Width(50));
                                }
                                else
                                {
                                    var curSprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(currency.Key);
                                    var curTexture = curSprite == null ? null : curSprite.texture;
                                    GUILayout.Button(curTexture, GUILayout.Width(50), GUILayout.Height(50));
                                    EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                    overridePrice[currencyKey] = (uint)EditorGUILayout.IntField((int)overridePrice[currencyKey], GUILayout.Width(150));
                                }

                                var originPrice = currency.Value;
                                var overrideValue = overridePrice[currencyKey];

                                var labelStyle = new GUIStyle(GUI.skin.label);
                                labelStyle.fontStyle = FontStyle.Bold;
                                labelStyle.fontSize = 12;

                                GUILayout.Space(100);

                                if (originPrice != overrideValue)
                                {
                                    if (originPrice > overrideValue)
                                    {
                                        labelStyle.normal.textColor = Color.green;
                                        var discount = originPrice - overrideValue;
                                        var discountPresent = Mathf.FloorToInt((float)discount / (float)originPrice * 100f);
                                        EditorGUILayout.LabelField(" - " + discountPresent.ToString() + "%", labelStyle, GUILayout.Width(100));
                                        discounts[currencyKey] = discountPresent;
                                    }
                                    else
                                    {
                                        labelStyle.normal.textColor = Color.red;
                                        var overPrice = overrideValue - originPrice;
                                        var overPersent = Mathf.FloorToInt((float)overPrice / (float)overrideValue * 100f);
                                        EditorGUILayout.LabelField(" + " + overPersent.ToString() + "%", labelStyle, GUILayout.Width(100));
                                    }

                                }
                            }

                            GUILayout.EndHorizontal();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No prices found for the item. This is not allowed for Store", MessageType.Error);
                    }
                    GUILayout.Space(10);

                    if (profileOffer || globalOffer)
                    {
                        var hasDuration = metaData.HasDuration;
                        EditorGUILayout.LabelField("Time limited?", titleStyle);
                        hasDuration = EditorGUILayout.Toggle(hasDuration);
                        var offerDuration = metaData.OfferDuration;
                        if (hasDuration)
                        {
                            EditorGUILayout.LabelField("Offer duration in seconds", titleStyle);
                            GUILayout.BeginHorizontal();
                            offerDuration = EditorGUILayout.IntField(offerDuration, GUILayout.Width(200));
                            EditorGUILayout.LabelField("seconds");
                            GUILayout.EndHorizontal();
                            if (offerDuration < 0)
                            {
                                offerDuration = 0;
                            }
                            else if (offerDuration == 0)
                            {
                                EditorGUILayout.HelpBox("Offer duration cannot be 0.", MessageType.Error);
                            }

                            EditorGUILayout.HelpBox("Determines how much time will be available to the offer player after he receives it.", MessageType.Info);
                        }

                        metaData.HasDuration = hasDuration;
                        metaData.OfferDuration = offerDuration;
                    }

                    if (!globalOffer && !profileOffer)
                    {
                        // draw position
                        EditorGUILayout.LabelField("Override Display Position?", titleStyle);
                        var lastOverridePosition = overridePosition;
                        overridePosition = EditorGUILayout.Toggle(overridePosition, GUILayout.Width(250));
                        if (lastOverridePosition != overridePosition)
                        {
                            if (overridePosition)
                            {
                                storeItem.DisplayPosition = 0;
                            }
                            else
                            {
                                storeItem.DisplayPosition = null;
                            }
                        }
                        if (overridePosition)
                        {
                            GUILayout.Space(5);
                            EditorGUILayout.LabelField("Display Position", titleStyle);
                            var enteredPosition = EditorGUILayout.IntField((int)storeItem.DisplayPosition, GUILayout.Width(250));
                            if (enteredPosition < 0)
                                enteredPosition = 0;
                            storeItem.DisplayPosition = (uint)enteredPosition;
                            EditorGUILayout.HelpBox("Intended display position for this item. Note that 0 is the first position.", MessageType.Info);
                        }
                        GUILayout.Space(10);
                    }

                    if (!globalOffer && !profileOffer)
                    {
                        // quantity limit
                        EditorGUILayout.LabelField("Quantity limit?", titleStyle);
                        hasQuantityLimit = EditorGUILayout.Toggle(hasQuantityLimit, GUILayout.Width(250));
                        if (hasQuantityLimit)
                        {
                            EditorGUILayout.LabelField("Quantity", titleStyle);
                            GUILayout.BeginHorizontal();
                            metaData.QuantityLimit = EditorGUILayout.IntField(metaData.QuantityLimit, GUILayout.Width(250));
                            EditorGUILayout.LabelField("per", GUILayout.Width(30));
                            metaData.QuanityLimitPeriod = (DatePeriod)EditorGUILayout.EnumPopup(metaData.QuanityLimitPeriod, new GUILayoutOption[] { GUILayout.Width(150) });
                            GUILayout.EndHorizontal();
                        }
                        GUILayout.Space(10);
                    }

                    // draw custom data
                    EditorGUILayout.LabelField("Custom Data", titleStyle);
                    customDataDrawer.Draw(metaData);
                }

                EditorGUI.EndDisabledGroup();

                GUILayout.Space(50);

                metaData.Discounts = discounts;
                metaData.HasQuantityLimit = hasQuantityLimit;
                metaData.Description = slotDespription;
                metaData.SlotDisplayName = slotDisplayName;
                metaData.Enable = isSlotEnabled;
                storeItem.CustomData = JsonPlugin.ToJsonCompress(metaData);
                storeItem.VirtualCurrencyPrices = overridePrice;
            }
        }

        private void DrawStores()
        {
            if (!IsStoreLoaded)
                return;

            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;

            GUILayout.Space(5);
            EditorUtils.DrawUILine(Color.black, 2, 20);
            GUILayout.Space(5);
            GUILayout.BeginHorizontal();

            // draw activity
            GUILayout.Space(5);
            EditorGUILayout.LabelField("Activity", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(60) });

            // draw id
            GUILayout.Space(30);
            EditorGUILayout.LabelField("ID", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(50) });

            // draw name
            GUILayout.Space(155);
            EditorGUILayout.LabelField("Store name", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(200) });

            // draw count
            GUILayout.Space(40);
            EditorGUILayout.LabelField("Items Count", titleStyle, new GUILayoutOption[] { GUILayout.MaxWidth(100) });

            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("New Store", EditorData.AddColor, 12, AddButtonOptions))
            {
                ShowProgress();
                PlayFabAdminAPI.GetCatalogItems(new GetCatalogItemsRequest
                {
                    CatalogVersion = CatalogKeys.ItemsCatalogID
                }, onGet =>
                {
                    HideProgress();
                    var fabItems = onGet.Catalog;
                    var ids = fabItems.Select(x => x.ItemId).ToArray();
                    AddStoreWindow.Show(ids, (storeID, storeName, defaultItem) =>
                    {
                        AddNewStore(storeID, storeName, defaultItem);
                    });
                }, OnFabError);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
            EditorUtils.DrawUILine(Color.black, 2, 20);
            GUILayout.Space(5);

            //StoreEnabledTitleTex = EditorUtils.MakeColorTexture(EditorData.StoreEnabledTitle);
            //StoreDisabledTitleTex = EditorUtils.MakeColorTexture(EditorData.StoreDisabledTitle);
            //StoreContentTex = EditorUtils.MakeColorTexture(EditorData.StoreContent);

            GUIStyle storeEnabledTitleStyle = new GUIStyle("HelpBox");
            storeEnabledTitleStyle.normal.background = StoreEnabledTitleTex;

            GUIStyle storeDisabledTitleStyle = new GUIStyle("HelpBox");
            storeDisabledTitleStyle.normal.background = StoreDisabledTitleTex;

            GUIStyle storeContentStyle = new GUIStyle("HelpBox");
            storeContentStyle.normal.background = StoreContentTex;


            // draw stores
            for (int i = 0; i < StoreResults.Count; i++)
            {
                var storePair = StoreResults.ElementAt(i);
                var storeID = storePair.Key;
                var storeResult = storePair.Value;
                var marketingData = storeResult.MarketingData;
                var rawData = marketingData.Metadata ?? JsonPlugin.EMPTY_JSON;
                var storeName = marketingData.DisplayName;
                var itemsCount = storeResult.Store.Count;
                var itemList = storeResult.Store;
                var metaData = JsonPlugin.FromJson<CBSStoreMeta>(rawData.ToString());
                var isEnabled = metaData.Enable;

                // draw title
                using (var horizontalScope = new GUILayout.VerticalScope(isEnabled ? storeEnabledTitleStyle : storeDisabledTitleStyle))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    var statusColor = isEnabled ? Color.green : Color.red;
                    EditorUtils.DrawButton(string.Empty, statusColor, 1, new GUILayoutOption[] { GUILayout.Width(30), GUILayout.Height(30) });
                    GUILayout.Space(50);
                    EditorGUILayout.LabelField(storeID, titleStyle, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) });
                    GUILayout.Space(105);
                    EditorGUILayout.LabelField(storeName, titleStyle, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) });
                    GUILayout.Space(170);
                    EditorGUILayout.LabelField(itemsCount.ToString(), titleStyle, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) });
                    GUILayout.FlexibleSpace();
                    if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, AddButtonOptions))
                    {
                        string questionsText = string.Format("Are you sure you want to remove store {0}?", storeID);
                        int option = EditorUtility.DisplayDialogComplex("Warning",
                            questionsText,
                            "Yes",
                            "No",
                            string.Empty);
                        switch (option)
                        {
                            // ok.
                            case 0:
                                RemoveStore(storeID);
                                break;
                        }
                    }
                    GUILayout.Space(5);
                    if (EditorUtils.DrawButton("Edit", EditorData.EditColor, 12, AddButtonOptions))
                    {
                        CustomDataDrawer.Reset();
                        StoreToEdit = storeResult.Copy();
                    }
                    GUILayout.EndHorizontal();
                }
                // draw content
                Vector2 rectPos = Vector2.zero;
                if (ScrollRect.ContainsKey(storeID))
                {
                    rectPos = ScrollRect[storeID];
                }
                else
                {
                    ScrollRect[storeID] = rectPos;
                }
                using (var horizontalScope = new GUILayout.VerticalScope(storeContentStyle, GUILayout.Height(130)))
                {
                    rectPos = EditorGUILayout.BeginScrollView(rectPos);
                    GUILayout.BeginHorizontal();

                    for (int j = 0; j < itemList.Count; j++)
                    {
                        var item = itemList[j];
                        var itemID = item.ItemId;
                        var slotRawData = item.CustomData == null ? JsonPlugin.EMPTY_JSON : item.CustomData.ToString();
                        var itemMetaData = new CBSStoreItemMeta();
                        try
                        {
                            itemMetaData = JsonPlugin.FromJsonDecompress<CBSStoreItemMeta>(slotRawData);
                        }
                        catch
                        {
                            itemMetaData = JsonPlugin.FromJson<CBSStoreItemMeta>(slotRawData);
                        }
                        var itemEnabled = itemMetaData.Enable;
                        var slotDisplayName = itemMetaData.SlotDisplayName;
                        if (!itemEnabled)
                            continue;
                        var itemTexture = StoreIcons.GetSprite(itemID);
                        var texture = itemTexture == null ? null : itemTexture.texture;
                        GUILayout.Button(texture, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(100) });

                        GUILayout.BeginVertical(GUILayout.Width(100));
                        GUILayout.Label(slotDisplayName, GUILayout.Width(100));

                        var currenciesExist = item.VirtualCurrencyPrices != null && item.VirtualCurrencyPrices.Count > 0;
                        if (currenciesExist)
                        {
                            var curList = item.VirtualCurrencyPrices;
                            foreach (var currency in curList)
                            {
                                GUILayout.BeginHorizontal();

                                // draw origin price
                                var currencyKey = currency.Key;
                                var currencyValue = currency.Value;
                                if (currencyKey == PlayfabUtils.REAL_MONEY_CODE)
                                {
                                    var curTexture = ResourcesUtils.GetRealMoneyImage();
                                    GUILayout.Button(curTexture, GUILayout.Width(20), GUILayout.Height(20));
                                    EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                    EditorGUILayout.LabelField(item.GetRMPriceString(), GUILayout.Width(100));
                                }
                                else
                                {
                                    var curSprite = CBSScriptable.Get<CurrencyIcons>().GetSprite(currency.Key);
                                    var curTexture = curSprite == null ? null : curSprite.texture;
                                    GUILayout.Button(curTexture, GUILayout.Width(20), GUILayout.Height(20));
                                    EditorGUILayout.LabelField(currency.Key, GUILayout.Width(25));
                                    EditorGUILayout.LabelField(currencyValue.ToString(), GUILayout.Width(100));
                                }

                                GUILayout.EndHorizontal();
                            }
                        }

                        GUILayout.EndVertical();
                    }

                    GUILayout.EndHorizontal();
                    EditorGUILayout.EndScrollView();
                    ScrollRect[storeID] = rectPos;
                }

                GUILayout.Space(25);
            }
        }

        private void DrawGlobalOffers()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            if (GlobalOfferStore == null)
                return;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Global offers list", titleStyle);
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Add new offer", EditorData.AddColor, 12, AddButtonOptions))
            {
                ShowAddStoreItemDialog(onAdd =>
                {
                    var itemStore = onAdd;
                    GlobalOfferStore.Store = GlobalOfferStore.Store ?? new List<StoreItem>();
                    GlobalOfferStore.Store.Add(itemStore);
                    SaveStore(GlobalOfferStore);
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // load content
            var storeList = GlobalOfferStore.Store ?? new List<StoreItem>();

            DrawStoreList(storeList, true, false);

            GlobalOfferStore.Store = storeList;
        }

        private void DrawProfileOffers()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 12;

            if (ProfileOfferStore == null)
                return;

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Profile offers list", titleStyle);
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, AddButtonOptions))
            {
                SaveStore(ProfileOfferStore);
            }
            if (EditorUtils.DrawButton("Add new offer", EditorData.AddColor, 12, AddButtonOptions))
            {
                ShowAddStoreItemDialog(onAdd =>
                {
                    var itemStore = onAdd;
                    ProfileOfferStore.Store = ProfileOfferStore.Store ?? new List<StoreItem>();
                    ProfileOfferStore.Store.Add(itemStore);
                    SaveStore(ProfileOfferStore);
                });
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);

            // load content
            var storeList = ProfileOfferStore.Store ?? new List<StoreItem>();

            DrawStoreList(storeList, false, true);

            ProfileOfferStore.Store = storeList;
        }

        private void LoadStores()
        {
            ShowProgress();
            var itemConfig = new ItemsConfigurator();
            itemConfig.GetItemsCatalog(itemsResult =>
            {
                HideProgress();
                var items = itemsResult.Catalog;
                ItemsDitcionary = items.ToDictionary(x => x.ItemId, x => x);

                ShowProgress();
                var request = new PlayFab.ServerModels.GetTitleDataRequest
                {
                    Keys = new List<string> { TitleKeys.StoreKey }
                };
                PlayFabServerAPI.GetTitleInternalData(request, OnGetStoreIDs, OnFabError);
            });

        }

        private void OnGetStoreIDs(PlayFab.ServerModels.GetTitleDataResult result)
        {
            HideProgress();
            var data = result.Data;
            if (data.ContainsKey(TitleKeys.StoreKey))
            {
                var rawData = data[TitleKeys.StoreKey];
                Container = JsonPlugin.FromJson<StoresContainer>(rawData);
            }
            else
            {
                Container = new StoresContainer();
            }
            IsStoreLoaded = true;
            LoadStoresByIDs();
        }

        private void OnFabError(PlayFabError error)
        {
            HideProgress();
            Debug.LogError(error.GenerateErrorReport());
        }

        private void LoadStoresByIDs()
        {
            var ids = Container.GetStoreIDs();
            foreach (var id in ids)
            {
                var request = new GetStoreItemsRequest
                {
                    CatalogVersion = CatalogKeys.ItemsCatalogID,
                    StoreId = id
                };
                PlayFabAdminAPI.GetStoreItems(request, OnGetStore, OnFabError);
            }
        }

        private void LoadGlobalOffers()
        {
            ShowProgress();
            var request = new GetStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = StoresContainer.GLOBAL_OFFER_STORE_ID
            };
            PlayFabAdminAPI.GetStoreItems(request, onGet =>
            {
                HideProgress();
                GlobalOfferStore = onGet;
            }, onError =>
            {
                HideProgress();
                if (onError.Error == PlayFabErrorCode.StoreNotFound)
                {
                    GlobalOfferStore = new GetStoreItemsResult
                    {
                        StoreId = StoresContainer.GLOBAL_OFFER_STORE_ID
                    };
                }
                else
                {
                    OnFabError(onError);
                }
            });
        }

        private void LoadProfileOffers()
        {
            ShowProgress();
            var request = new GetStoreItemsRequest
            {
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                StoreId = StoresContainer.PROFILE_OFFER_STORE_ID
            };
            PlayFabAdminAPI.GetStoreItems(request, onGet =>
            {
                HideProgress();
                ProfileOfferStore = onGet;
            }, onError =>
            {
                HideProgress();
                if (onError.Error == PlayFabErrorCode.StoreNotFound)
                {
                    ProfileOfferStore = new GetStoreItemsResult
                    {
                        StoreId = StoresContainer.PROFILE_OFFER_STORE_ID
                    };
                }
                else
                {
                    OnFabError(onError);
                }
            });
        }

        private void OnGetStore(GetStoreItemsResult result)
        {
            var storeID = result.StoreId;
            StoreResults[storeID] = result;
        }

        private void AddNewStore(string storeID, string storeName, string defaultItemID)
        {
            ShowProgress();
            var addRequest = new UpdateStoreItemsRequest
            {
                StoreId = storeID,
                CatalogVersion = CatalogKeys.ItemsCatalogID,
                Store = new List<StoreItem>() { new StoreItem { ItemId = defaultItemID } },
                MarketingData = new StoreMarketingModel
                {
                    DisplayName = storeName
                }
            };
            PlayFabAdminAPI.SetStoreItems(addRequest, onAdd =>
            {
                var tempContainer = Container ?? new StoresContainer();
                tempContainer.AddStoreID(storeID);
                var rawContainer = JsonPlugin.ToJson(tempContainer);
                var updateRequest = new PlayFab.ServerModels.SetTitleDataRequest
                {
                    Key = TitleKeys.StoreKey,
                    Value = rawContainer
                };
                PlayFabServerAPI.SetTitleInternalData(updateRequest, onUpdate =>
                {
                    HideProgress();
                    LoadStores();
                }, OnFabError);
            }, OnFabError);
        }

        private void CheckAndSaveLimitationData(GetStoreItemsResult store, Action onCheck = null)
        {
            var items = store.Store;
            var storeID = store.StoreId;
            foreach (var storeItem in items)
            {
                var itemID = storeItem.ItemId;
                var slotRawData = storeItem.CustomData == null ? JsonPlugin.EMPTY_JSON : storeItem.CustomData.ToString();
                var metaData = new CBSStoreItemMeta();
                try
                {
                    metaData = JsonPlugin.FromJsonDecompress<CBSStoreItemMeta>(slotRawData);
                }
                catch
                {
                    metaData = JsonPlugin.FromJson<CBSStoreItemMeta>(slotRawData);
                }
                if (metaData.HasQuantityLimit)
                {
                    Container.AddOrUpdateLimitation(storeID, itemID, new StoreLimitationMeta
                    {
                        LimitPeriod = metaData.QuanityLimitPeriod,
                        MaxQuantity = metaData.QuantityLimit
                    });
                }
                else
                {
                    Container.CheckForRemoveLimitation(storeID, itemID);
                }
            }

            var tempContainer = Container ?? new StoresContainer();
            var rawContainer = JsonPlugin.ToJson(tempContainer);
            var updateRequest = new PlayFab.ServerModels.SetTitleDataRequest
            {
                Key = TitleKeys.StoreKey,
                Value = rawContainer
            };
            PlayFabServerAPI.SetTitleInternalData(updateRequest, onUpdate =>
            {
                HideProgress();
                onCheck?.Invoke();
            }, OnFabError);
        }

        private void SaveStore(GetStoreItemsResult store, Action onSave = null)
        {
            ShowProgress();
            var request = new UpdateStoreItemsRequest
            {
                StoreId = store.StoreId,
                CatalogVersion = store.CatalogVersion,
                MarketingData = store.MarketingData,
                Store = store.Store
            };
            PlayFabAdminAPI.SetStoreItems(request, onUpdate =>
            {
                onSave?.Invoke();
                StoreToEdit = null;
                CheckAndSaveLimitationData(store, () =>
                {
                    LoadStores();
                });
            }, OnFabError);
        }

        private void RemoveStore(string storeID)
        {
            ShowProgress();
            var request = new DeleteStoreRequest
            {
                StoreId = storeID
            };
            PlayFabAdminAPI.DeleteStore(request, onDelete =>
            {
                var tempContainer = Container ?? new StoresContainer();
                tempContainer.RemoveID(storeID);

                var store = StoreResults[storeID];
                var items = store.Store;
                foreach (var storeItem in items)
                {
                    var itemID = storeItem.ItemId;
                    tempContainer.CheckForRemoveLimitation(storeID, itemID);
                }

                var rawContainer = JsonPlugin.ToJson(tempContainer);
                StoreResults.Remove(storeID);

                var updateRequest = new PlayFab.ServerModels.SetTitleDataRequest
                {
                    Key = TitleKeys.StoreKey,
                    Value = rawContainer
                };
                PlayFabServerAPI.SetTitleInternalData(updateRequest, onUpdate =>
                {
                    HideProgress();
                    LoadStores();
                }, OnFabError);
            }, OnFabError);
        }

        private void StartSpecialOffer(string itemID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.StartSpecialOfferMethod,
                    FunctionParameter = new FunctionIDRequest
                    {
                        ID = itemID
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet =>
                {
                    var cbsError = OnGet.GetCBSError();
                    if (cbsError != null)
                    {
                        AddErrorLog(cbsError);
                        HideProgress();
                    }
                    else
                    {
                        var functionResult = OnGet.GetResult<FunctionStartOfferResult>();
                        var endDate = functionResult.EndDate;
                        var now = DateTime.UtcNow;
                        HideProgress();
                        LoadGlobalOffers();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void StopSpecialOffer(string itemID)
        {
            ShowProgress();
            GetEntityToken(() =>
            {
                var request = new PlayFab.CloudScriptModels.ExecuteFunctionRequest
                {
                    FunctionName = AzureFunctions.StopSpecialOfferMethod,
                    FunctionParameter = new FunctionIDRequest
                    {
                        ID = itemID
                    }
                };
                PlayFabCloudScriptAPI.ExecuteFunction(request, OnGet =>
                {
                    var cbsError = OnGet.GetCBSError();
                    if (cbsError != null)
                    {
                        AddErrorLog(cbsError);
                        HideProgress();
                    }
                    else
                    {
                        var functionResult = OnGet.GetResult<FunctionStopOfferResult>();
                        HideProgress();
                        LoadGlobalOffers();
                    }
                }, OnFailed =>
                {
                    AddErrorLog(OnFailed);
                    HideProgress();
                });
            });
        }

        private void GetEntityToken(Action onGet)
        {
            var request = new PlayFab.AuthenticationModels.GetEntityTokenRequest();

            TitleEntityToken = null;

            PlayFabAuthenticationAPI.GetEntityToken(
                request,
                result =>
                {
                    TitleEntityToken = result.EntityToken;
                    onGet?.Invoke();
                },
                error =>
                {
                    AddErrorLog(error);
                    HideProgress();
                }
            );
        }

        private void ShowAddStoreItemDialog(Action<StoreItem> modifyCallback)
        {
            if (CachedItemCategories == null || CachedItems == null || CachedLootBoxCategories == null || CachedPacksCategories == null)
            {
                ShowProgress();
                var itemConfig = new ItemsConfigurator();
                itemConfig.GetTitleData(categoriesResult =>
                {
                    if (categoriesResult.Data.ContainsKey(TitleKeys.ItemsCategoriesKey))
                    {
                        var rawData = categoriesResult.Data[TitleKeys.ItemsCategoriesKey];
                        CachedItemCategories = JsonUtility.FromJson<Categories>(rawData);
                    }
                    else
                    {
                        CachedItemCategories = new Categories();
                    }

                    if (categoriesResult.Data.ContainsKey(TitleKeys.LootboxesCategoriesKey))
                    {
                        var rawData = categoriesResult.Data[TitleKeys.LootboxesCategoriesKey];
                        CachedLootBoxCategories = JsonUtility.FromJson<Categories>(rawData);
                    }
                    else
                    {
                        CachedLootBoxCategories = new Categories();
                    }

                    if (categoriesResult.Data.ContainsKey(TitleKeys.PackCategoriesKey))
                    {
                        var rawData = categoriesResult.Data[TitleKeys.PackCategoriesKey];
                        CachedPacksCategories = JsonUtility.FromJson<Categories>(rawData);
                    }
                    else
                    {
                        CachedPacksCategories = new Categories();
                    }


                    // get item catalog
                    itemConfig.GetItemsCatalog(itemsResult =>
                    {
                        HideProgress();
                        CachedItems = itemsResult.Catalog;
                        AddStoreItemWindow.Show(new StoreItemWindowRequest
                        {
                            itemCategories = CachedItemCategories,
                            lootboxCategories = CachedLootBoxCategories,
                            packCategories = CachedPacksCategories,
                            items = CachedItems,
                            modifyCallback = modifyCallback
                        });
                        //GUIUtility.ExitGUI();
                    });
                });
            }
            else
            {
                // show prize windows
                AddStoreItemWindow.Show(new StoreItemWindowRequest
                {
                    itemCategories = CachedItemCategories,
                    lootboxCategories = CachedLootBoxCategories,
                    packCategories = CachedPacksCategories,
                    items = CachedItems,
                    modifyCallback = modifyCallback,
                });
                GUIUtility.ExitGUI();
            }
        }
    }
}
#endif

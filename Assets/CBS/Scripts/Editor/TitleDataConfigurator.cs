#if ENABLE_PLAYFABADMIN_API
using CBS.Editor.Window;
using CBS.Models;
using CBS.Scriptable;
using CBS.Utils;
using PlayFab;
using PlayFab.AdminModels;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class TitleDataConfigurator : BaseConfigurator
    {
        protected override string Title => "Title Data Configuration";

        protected override bool DrawScrollView => true;
        private Rect CategoriesRect = new Rect(0, 0, 150, 700);
        private Rect ScrollRect = new Rect(0, 100, 150, 600);
        private Rect ItemsRect = new Rect(200, 100, 855, 700);
        private Vector2 PositionScroll { get; set; }
        private Vector2 TitleScroll { get; set; }
        private TitleDataContainer DataContainer { get; set; }
        private int DataIndex { get; set; }
        private CBSTitleData SelectedData { get; set; }
        private ObjectCustomDataDrawer<TitleCustomData> CustomDataDrawer { get; set; }

        private EditorData EditorData { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            DataContainer = new TitleDataContainer();
            CustomDataDrawer = new ObjectCustomDataDrawer<TitleCustomData>(PlayfabUtils.TITLE_DATA_SIZE, 830f);

            GetTitleData();
        }

        protected override void OnDrawInside()
        {
            DrawTitles();
            DrawTitleData();
        }

        private void DrawTitles()
        {
            using (var areaScope = new GUILayout.AreaScope(CategoriesRect))
            {
                GUILayout.BeginVertical();

                int categoryHeight = 30;
                var allData = DataContainer.GetAll();
                int categoriesCount = allData.Count;
                var gridRect = new Rect(0, 0, 150, categoryHeight * categoriesCount);
                var scrollRect = gridRect;
                scrollRect.height += categoryHeight*2;
                scrollRect.width = 0;
                TitleScroll = GUI.BeginScrollView(ScrollRect, TitleScroll, scrollRect);

                if (categoriesCount > 0)
                {
                    var categoriesMenu = allData.Select(x => x.Value.DataKey).ToArray();
                    DataIndex = GUI.SelectionGrid(gridRect, DataIndex, categoriesMenu, 1);
                    if (DataIndex >= allData.Count)
                        DataIndex = 0;
                    string selctedCategory = categoriesMenu[DataIndex];
                    SelectedData = allData.ElementAt(DataIndex).Value;
                }
                
                var oldColor = GUI.color;
                GUI.backgroundColor = EditorData.AddColor;
                var style = new GUIStyle(GUI.skin.button);
                style.fontStyle = FontStyle.Bold;
                style.fontSize = 12;
                if (GUI.Button(new Rect(0,categoryHeight + categoryHeight * categoriesCount, 150, categoryHeight), "Add new Instance", style))
                {
                    AddTitleDataWindow.Show(onAdd =>
                    {
                        var newInstance = onAdd;
                        DataContainer.Add(newInstance);
                    });
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = oldColor;

                GUILayout.EndVertical();
                
                GUI.EndScrollView();
            }
        }

        private void DrawTitleData()
        {
            if (SelectedData == null)
                return;

            using (var areaScope = new GUILayout.AreaScope(ItemsRect))
            {
                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.fontSize = 12;

                EditorGUILayout.LabelField("Data Key", titleStyle);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(SelectedData.DataKey);

                GUILayout.FlexibleSpace();
                if (EditorUtils.DrawButton("Remove", EditorData.RemoveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                {
                    int option = EditorUtility.DisplayDialogComplex("Warning",
                            "Are you sure you want to remove this Title Data?",
                            "Yes",
                            "No",
                            string.Empty);
                    switch (option)
                    {
                        // ok.
                        case 0:
                            RemoveTitleData(SelectedData);
                            break;
                    }
                    if (SelectedData == null)
                    {
                        DataIndex = 0;
                        return;
                    }
                }

                if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
                {
                    SaveTitleData(SelectedData);
                }

                GUILayout.EndHorizontal();

                // draw customs properties
                PositionScroll = GUILayout.BeginScrollView(PositionScroll);
                EditorGUILayout.LabelField("Custom Data", titleStyle);
                var rawData = CustomDataDrawer.Draw(SelectedData);
                GUILayout.Space(150);
                GUILayout.EndScrollView();
            }
        }

        public void GetTitleData(Action<TitleDataContainer> result = null)
        {
            ShowProgress();
            var request = new GetTitleDataRequest();
            PlayFabAdminAPI.GetTitleData(request, onGet =>
            {
                var data = onGet.Data;
                DataContainer = new TitleDataContainer(data);
                result?.Invoke(DataContainer);
                HideProgress();
            }, onFailed =>
            {
                result?.Invoke(new TitleDataContainer());
                AddErrorLog(onFailed);
                HideProgress();
            });
        }

        private void SaveTitleData(CBSTitleData data)
        {
            ShowProgress();
            var dataKey = data.DataKey;
            var dataRaw = JsonPlugin.ToJsonCompress(data);
            var request = new SetTitleDataRequest
            {
                Key = dataKey,
                Value = dataRaw
            };
            PlayFabAdminAPI.SetTitleData(request, onGet =>
            {
                HideProgress();
            }, onFailed =>
            {
                AddErrorLog(onFailed);
                HideProgress();
            });
        }

        private void RemoveTitleData(CBSTitleData data)
        {
            ShowProgress();
            var dataKey = data.DataKey;
            var request = new SetTitleDataRequest
            {
                Key = dataKey,
                Value = null
            };
            PlayFabAdminAPI.SetTitleData(request, onGet =>
            {
                DataContainer.Remove(SelectedData.DataKey);
                SelectedData = null;
                HideProgress();
            }, onFailed =>
            {
                AddErrorLog(onFailed);
                HideProgress();
            });
        }
    }
}
#endif

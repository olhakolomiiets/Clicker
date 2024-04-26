#if ENABLE_PLAYFABADMIN_API
using CBS.Models;
using CBS.Scriptable;
using PlayFab;
using PlayFab.AdminModels;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor
{
    public class FriendsConfigurator : BaseConfigurator
    {
        protected override string Title => "Friends Configuration";

        private FriendsMetaData FriendsMeta { get; set; }

        protected override bool DrawScrollView => true;

        private int MaxFriendsCount { get; set; } = 20;
        private int MaxRequestedCount { get; set; } = 20;

        private EditorData EditorData { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            GetFriendsData();
        }

        protected override void OnDrawInside()
        {
            if (FriendsMeta == null)
                return;

            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;

            // draw max friends
            EditorGUILayout.LabelField("Max friends count", titleStyle);
            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            MaxFriendsCount = EditorGUILayout.IntField(MaxFriendsCount, new GUILayoutOption[] { GUILayout.Width(100) });
            if (MaxFriendsCount < 0)
            {
                MaxFriendsCount = 0;
            }
            if (MaxFriendsCount > FriendsMetaData.MAX_FRIEND_VALUE)
            {
                MaxFriendsCount = FriendsMetaData.MAX_FRIEND_VALUE;
            }
            GUILayout.FlexibleSpace();
            if (EditorUtils.DrawButton("Save", EditorData.SaveColor, 12, new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) }))
            {
                SaveDataByKey(TitleKeys.FriendsDataKey, JsonPlugin.ToJson(FriendsMeta));
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Max friends count. Max value = " + FriendsMetaData.MAX_FRIEND_VALUE.ToString(), MessageType.Info);

            // draw max requested
            EditorGUILayout.LabelField("Max requested friends count", titleStyle);
            GUILayout.Space(10);
            MaxRequestedCount = EditorGUILayout.IntField(MaxRequestedCount, new GUILayoutOption[] { GUILayout.Width(100) });
            if (MaxRequestedCount < 0)
            {
                MaxRequestedCount = 0;
            }
            if (MaxRequestedCount > FriendsMetaData.MAX_REQUESTED_VALUE)
            {
                MaxRequestedCount = FriendsMetaData.MAX_REQUESTED_VALUE;
            }
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("The maximum number of users that can be tagged as 'Requested Friends'. Max value = " + FriendsMetaData.MAX_REQUESTED_VALUE.ToString(), MessageType.Info);

            FriendsMeta.MaxFriend = MaxFriendsCount;
            FriendsMeta.MaxRequested = MaxRequestedCount;
        }

        private void GetFriendsData()
        {
            ShowProgress();
            var keys = new List<string>();
            keys.Add(TitleKeys.FriendsDataKey);
            var request = new GetTitleDataRequest
            {
                Keys = keys
            };
            PlayFabAdminAPI.GetTitleInternalData(request, OnInternalDataGetted, OnGetDataFailed);
        }

        private void OnInternalDataGetted(GetTitleDataResult result)
        {
            HideProgress();
            var dictionary = result.Data;
            bool keyExist = dictionary.ContainsKey(TitleKeys.FriendsDataKey);
            string rawValue = keyExist ? dictionary[TitleKeys.FriendsDataKey] : string.Empty;
            FriendsMeta = string.IsNullOrEmpty(rawValue) ? FriendsMetaData.Default() : JsonPlugin.FromJson<FriendsMetaData>(rawValue);
            MaxFriendsCount = FriendsMeta.MaxFriend;
            MaxRequestedCount = FriendsMeta.MaxRequested;
        }

        private void OnGetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }

        private void SaveDataByKey(string key, string value)
        {
            ShowProgress();
            var request = new SetTitleDataRequest
            {
                Key = key,
                Value = value
            };
            PlayFabAdminAPI.SetTitleInternalData(request, OnSetData, OnSetDataFailed);
        }

        private void OnSetData(SetTitleDataResult result)
        {
            HideProgress();
            GetFriendsData();
        }

        private void OnSetDataFailed(PlayFabError error)
        {
            AddErrorLog(error);
            HideProgress();
        }
    }
}
#endif

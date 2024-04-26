
#if ENABLE_PLAYFABADMIN_API
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CBS.Editor.Window
{
    public class AddBanWindow : EditorWindow
    {
        private static Action<PlayFab.AdminModels.BanUsersRequest> AddCallback { get; set; }

        private int EnteredHours { get; set; } = 1;
        private string EnteredReason { get; set; }
        private static string ProfileID { get; set; }

        public static void Show(string profileID, Action<PlayFab.AdminModels.BanUsersRequest> modifyCallback)
        {
            AddCallback = modifyCallback;
            ProfileID = profileID;

            AddBanWindow window = ScriptableObject.CreateInstance<AddBanWindow>();
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
                EditorGUILayout.LabelField("Add new ban for profile", titleStyle);

                GUILayout.Space(30);

                GUILayout.Label("Ban Time", GUILayout.Width(120));
                EnteredHours = EditorGUILayout.IntField(EnteredHours);

                GUILayout.Space(5);

                EditorGUILayout.HelpBox("Ban time in hours", MessageType.Info);

                GUILayout.Space(5);

                GUILayout.Label("Ban Reason", GUILayout.Width(120));
                EnteredReason = EditorGUILayout.TextArea(EnteredReason, new GUILayoutOption[] { GUILayout.Height(150) });

                GUILayout.EndVertical();

                GUILayout.Space(30);

                GUILayout.BeginHorizontal();

                if (GUILayout.Button("Ban profile"))
                {
                    var banRequest = new PlayFab.AdminModels.BanRequest
                    {
                        PlayFabId = ProfileID,
                        DurationInHours = (uint)EnteredHours,
                        Reason = EnteredReason
                    };
                    var banList = new List<PlayFab.AdminModels.BanRequest>();
                    banList.Add(banRequest);
                    var banRequests = new PlayFab.AdminModels.BanUsersRequest
                    {
                        Bans = banList
                    };
                    AddCallback?.Invoke(banRequests);
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
#endif

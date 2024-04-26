#if ENABLE_PLAYFABADMIN_API
using CBS.Scriptable;
using CBS.Utils;
using System.Linq;
using UnityEditor;
using UnityEditor.Purchasing;
using UnityEngine;

namespace CBS.Editor
{
    public class IAPConfigurator : BaseConfigurator
    {
        protected override string Title => "IAP Configuration";

        protected override bool DrawScrollView => true;

        private BuildTargetGroup[] SupportedIAPPlatforms
        {
            get
            {
                return new BuildTargetGroup[]
                {
                    BuildTargetGroup.Android,
                    BuildTargetGroup.iOS
                };
            }
        }

        private EditorData EditorData { get; set; }
        private IAPConfig IAPConfig { get; set; }
        private SerializedObject SerializedTarget { get; set; }

        public override void Init(MenuTitles title)
        {
            base.Init(title);
            EditorData = CBSScriptable.Get<EditorData>();
            IAPConfig = CBSScriptable.Get<IAPConfig>();
            SerializedTarget = new SerializedObject(IAPConfig);
        }

        protected override void OnDrawInside()
        {
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.fontSize = 14;

            var enableIAP = CBSScriptingSymbols.IsEnabledDefineSymbols(CBSScriptingSymbols.IAP_SYMBOL);
            var externalIDs = IAPConfig.ExternalIDs;

            // check supported platform
            var target = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (!SupportedIAPPlatforms.Contains(target))
            {
                var warningMessage = string.Format("{0} platform not supported for cbs in app purchase.", target.ToString());
                EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                return;
            }

            // check unity iap enabled
            var unityIAPEnable = PurchasingSettings.enabled;
            if (!unityIAPEnable)
            {
                EditorGUILayout.HelpBox("Unity in app purchase is disabled. Please check setup tutorial to enable IAP service.", MessageType.Error);
                return;
            }

            // draw configs
            var lastEnableIAP = EditorGUILayout.Toggle("Enable IAP", enableIAP, new GUILayoutOption[] { GUILayout.Width(400) });
            if (lastEnableIAP != enableIAP)
            {
                SetIAPActivity(lastEnableIAP);
            }
            enableIAP = lastEnableIAP;
            GUILayout.Space(10);
            EditorGUILayout.HelpBox("Currently In App Purchase available for iOS/Android only.", MessageType.Info);
            if (enableIAP)
            {
                GUILayout.Space(10);
                // draw external ids
                var idsProperty = SerializedTarget.FindProperty("ExternalIDs");
                EditorGUILayout.PropertyField(idsProperty, new GUIContent("External purchase IDs"), true); // True means show children
                SerializedTarget.ApplyModifiedProperties();
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("By default, CBS will register all Catalog items as purchasable items with the IAP. But you can add your externals additional ids to make purchases.", MessageType.Info);
            }

            // save data
            IAPConfig.EnableIAP = enableIAP;
            IAPConfig.ExternalIDs = externalIDs;
            IAPConfig.Save();
        }

        public void SetIAPActivity(bool enabled)
        {
            if (enabled)
            {
                CBSScriptingSymbols.AddCompileDefine(CBSScriptingSymbols.IAP_SYMBOL, SupportedIAPPlatforms);
            }
            else
            {
                CBSScriptingSymbols.RemoveCompileDefine(CBSScriptingSymbols.IAP_SYMBOL, SupportedIAPPlatforms);
            }
        }
    }
}
#endif

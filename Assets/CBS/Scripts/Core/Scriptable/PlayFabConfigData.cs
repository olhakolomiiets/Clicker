using UnityEngine;

namespace CBS.Scriptable
{
    [CreateAssetMenu(fileName = "PlayFabConfigData", menuName = "CBS/Add new PlayFab Config Data")]
    public class PlayFabConfigData : ExternalData
    {
        private static string DefaultAzureProjectPath = "/../CBSAzureFunctionsProject";

        public override string ResourcePath => "PlayFabConfigData";
        public override string EditorPath => "Assets/CBS_External/Resources";

        public override string EditorAssetName => "PlayFabConfigData.asset";

        public string ExternalClassToRegisterAzureFunction;

        public string AzureProjectPath;

        public string GetAzureProjectPath()
        {
            if (string.IsNullOrEmpty(AzureProjectPath))
                return DefaultAzureProjectPath;
            return AzureProjectPath;
        }
    }
}

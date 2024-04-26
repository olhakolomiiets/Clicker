using PlayFab.ClientModels;

namespace CBS.Models
{
    public class CBSGetAccountInfoResult : CBSBaseResult
    {
        public string DisplayName;
        public string AvatarUrl;
        public UserAccountInfo PlayFabResult;
    }
}

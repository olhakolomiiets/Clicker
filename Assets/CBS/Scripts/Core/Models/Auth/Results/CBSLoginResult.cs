using CBS.Core.Auth;
using PlayFab.ClientModels;
using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSLoginResult : BaseAuthResult
    {
        public bool IsNew;
        public LoginResult Result;
        public BaseCredential Credential;
        public GetCatalogItemsResult ItemsResult;
        public Dictionary<string, string> ItemsCategoryData;
    }
}

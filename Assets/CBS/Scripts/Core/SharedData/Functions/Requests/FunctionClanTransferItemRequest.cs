using PlayFab;

namespace CBS.Models
{
    public class FunctionClanTransferItemRequest : FunctionBaseRequest
    {
        public string ClanID;
        public string ItemInstanceID;
        public PlayFabAuthenticationContext ProfileAuthContext;
    }
}

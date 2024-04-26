

namespace CBS.Models
{
    public class ChatBanRequest : FunctionBaseRequest
    {
        public string ProfileIDForBan;
        public string ChatID;
        public string Reason;
        public int BanHours;
    }
}


namespace CBS.Models
{
    public class CBSBanProfileRequest : CBSBaseRequest
    {
        public string ProfileIDToBan;
        public string Reason;
        public uint BanHours;
    }
}

using CBS.SharedData;

namespace CBS.Models
{
    public class FunctionPostLoginRequest : FunctionBaseRequest
    {
        public bool NewlyCreated;
        public bool AuthGenerateName;
        public bool PreloadPlayerLevel;
        public bool PreloadAccountData;
        public bool PreloadClan;
        public string RandomNamePrefix;
        public NewlyCreatedCheck NewPlayerChecker;
        public LoadCatalogItems LoadItems;
    }
}

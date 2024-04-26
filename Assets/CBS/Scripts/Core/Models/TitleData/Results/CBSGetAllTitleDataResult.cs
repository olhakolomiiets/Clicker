using System.Collections.Generic;

namespace CBS.Models
{
    public class CBSGetAllTitleDataResult : CBSBaseResult
    {
        public Dictionary<string, CBSTitleData> DataDictionary;
    }
}

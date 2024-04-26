using System;
using System.Collections.Generic;

namespace CBS.Models
{
    [Serializable]
    public class Categories
    {
        public string TitleKey { get; set; }

        public List<string> List;
    }
}

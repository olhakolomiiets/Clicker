using System.Collections.Generic;

namespace CBS.Models
{
    public class TitleDataContainer
    {
        public Dictionary<string, CBSTitleData> TitleData;

        public TitleDataContainer()
        {
            TitleData = new Dictionary<string, CBSTitleData>();
        }

        public TitleDataContainer(Dictionary<string, string> inputData)
        {
            if (inputData == null)
                TitleData = new Dictionary<string, CBSTitleData>();
            foreach (var dataPair in inputData)
            {
                var dataKey = dataPair.Key;
                var dataRaw = dataPair.Value;
                try
                {
                    var cbsTitleData = JsonPlugin.FromJsonDecompress<CBSTitleData>(dataRaw);
                    Add(dataKey, cbsTitleData);
                }
                catch { }
            }
        }

        public void Add(string dataKey, CBSTitleData titleData)
        {
            if (TitleData == null)
                TitleData = new Dictionary<string, CBSTitleData>();
            TitleData[dataKey] = titleData;
        }

        public void Add(CBSTitleData titleData)
        {
            if (TitleData == null)
                TitleData = new Dictionary<string, CBSTitleData>();
            var dataKey = titleData.DataKey;
            TitleData[dataKey] = titleData;
        }

        public void Remove(string dataKey)
        {
            if (TitleData == null)
                return;
            TitleData.Remove(dataKey);
        }

        public Dictionary<string, CBSTitleData> GetAll()
        {
            if (TitleData == null)
                TitleData = new Dictionary<string, CBSTitleData>();
            return TitleData;
        }
    }
}


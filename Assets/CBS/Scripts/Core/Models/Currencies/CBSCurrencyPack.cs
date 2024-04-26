using CBS.Models;
using CBS.Utils;
using PlayFab.ClientModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CBS
{
    public class CBSCurrencyPack
    {
        public string ID { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string PriceTitle { get; private set; }
        public string ExternalURL { get; private set; }
        public string ItemClass { get; private set; }
        public Dictionary<string, CBSCurrency> Currencies { get; private set; }

        private string CustomData { get; set; }

        public string Tag
        {
            get
            {
                bool exist = Detail.Tags != null && Detail.Tags.Count != 0;
                return exist ? Detail.Tags.FirstOrDefault() : string.Empty;
            }
        }

        public CatalogItem Detail { get; private set; }

        public CBSCurrencyPack(CatalogItem pack)
        {
            Detail = pack;
            ID = pack.ItemId;
            DisplayName = pack.DisplayName;
            Description = pack.Description;
            PriceTitle = pack.GetRMPriceString();
            ExternalURL = pack.ItemImageUrl;
            Currencies = pack.Bundle.GetCBSCurrencies();
            CustomData = pack.CustomData;
            ItemClass = pack.ItemClass;
        }

        public virtual T GetCustomData<T>() where T : CBSCurrencyPackCustomData
        {
            try
            {
                return JsonPlugin.FromJsonDecompress<T>(CustomData);
            }
            catch
            {
                return JsonPlugin.FromJson<T>(CustomData);
            }
        }

        public Dictionary<string, object> GetCustomDataAsDictionary()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes()).FirstOrDefault(x => x.Name == ItemClass);
            var data = JsonPlugin.FromJsonDecompress(CustomData, type);
            var baseList = typeof(CBSCurrencyPackCustomData).GetFields().Where(f => f.IsPublic).Select(x => x.Name).ToList();
            var list = type.GetFields().Where(f => f.IsPublic && !baseList.Contains(f.Name));
            return list.ToDictionary(x => x.Name, x => x.GetValue(data));
        }
    }
}

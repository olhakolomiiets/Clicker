using CBS.Models;
using CBS.Scriptable;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CBS
{
    public abstract class CBSBaseItem
    {
        public string ItemID { get; protected set; }
        public string DisplayName { get; protected set; }
        public string Description { get; protected set; }
        public string Category { get; protected set; }
        public string ItemClass { get; protected set; }
        public string ExternalIconURL { get; protected set; }
        public ItemType Type { get; protected set; }
        public Dictionary<string, uint> Prices { get; protected set; } = new Dictionary<string, uint>();

        internal string CustomData { get; set; }

        public virtual T GetCustomData<T>() where T : CBSItemCustomData
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
            var data = new object();
            try
            {
                data = JsonPlugin.FromJsonDecompress(CustomData, type);
            }
            catch
            {
                data = JsonPlugin.FromJson(CustomData, type);
            }
            var baseList = typeof(CBSItemCustomData).GetFields().Where(f => f.IsPublic).Select(x => x.Name).ToList();
            var list = type.GetFields().Where(f => f.IsPublic && !baseList.Contains(f.Name));
            return list.ToDictionary(x => x.Name, x => x.GetValue(data));
        }

        public GameObject GetLinkedPrefab()
        {
            var data = CBSScriptable.Get<LinkedPrefabData>();
            return data.GetLinkedData(ItemID);
        }

        public TScriptable GetLinkedScriptable<TScriptable>() where TScriptable : ScriptableObject
        {
            var data = CBSScriptable.Get<LinkedScriptableData>();
            return data.GetScriptableData<TScriptable>(ItemID);
        }

        public abstract CBSItemRecipe GetRecipeData();

        public abstract List<CBSItemUpgradeState> GetUpgradeList();
    }
}

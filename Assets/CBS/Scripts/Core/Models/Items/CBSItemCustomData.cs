using CBS.Models;
using System;

namespace CBS
{
    [Serializable]
    public class CBSItemCustomData : CBSBaseCustomData
    {
        public bool IsEquippable;
        public bool IsConsumable;
        public bool IsStackable;
        public bool IsTradable;
        public bool IsRecipe;
        public ItemType ItemType;
    }
}

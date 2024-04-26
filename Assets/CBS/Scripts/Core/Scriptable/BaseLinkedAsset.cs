namespace CBS.Scriptable
{
    [System.Serializable]
    public class BaseLinkedAsset<TBase> where TBase : UnityEngine.Object
    {
        public string ID;
        public TBase Asset;
    }
}

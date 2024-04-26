using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CBS.Scriptable
{
    public abstract class LinkedData<TBase> : CBSScriptable where TBase : UnityEngine.Object
    {
        public List<BaseLinkedAsset<TBase>> AssetList;

        public abstract string EditorPath { get; }
        public abstract string EditorAssetName { get; }

        internal override T Load<T>()
        {
#if UNITY_EDITOR
            var pathExist = Directory.Exists(EditorPath);
            if (!pathExist)
            {
                var directory = Directory.CreateDirectory(EditorPath);
                UnityEditor.AssetDatabase.Refresh();
            }


            var pathToAsset = EditorPath + "/" + EditorAssetName;
            var fileExists = File.Exists(pathToAsset);

            if (!fileExists)
            {
                T asset = CreateInstance<T>();
                UnityEditor.AssetDatabase.CreateAsset(asset, pathToAsset);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh();
            }
#endif
            return base.Load<T>();
        }

        public virtual TBase GetLinkedData(string id)
        {
            if (AssetList == null) return null;
            if (string.IsNullOrEmpty(id)) return null;
            var existedData = AssetList.FirstOrDefault(x => x.ID == id);
            return existedData == null ? null : existedData.Asset;
        }

#if UNITY_EDITOR
        public void SaveAssetData(string id, TBase asset)
        {
            if (AssetList == null)
                AssetList = new List<BaseLinkedAsset<TBase>>();
            if (string.IsNullOrEmpty(id)) return;

            var existedData = AssetList.FirstOrDefault(x => x.ID == id);

            if (existedData != null)
            {
                existedData.Asset = asset;
            }
            else
            {
                AssetList.Add(new BaseLinkedAsset<TBase>
                {
                    ID = id,
                    Asset = asset
                });
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void RemoveAsset(string id)
        {
            if (AssetList == null)
                return;
            if (string.IsNullOrEmpty(id)) return;
            var existedData = AssetList.FirstOrDefault(x => x.ID == id);

            if (existedData == null)
                return;

            AssetList.Remove(existedData);
            AssetList.TrimExcess();

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}

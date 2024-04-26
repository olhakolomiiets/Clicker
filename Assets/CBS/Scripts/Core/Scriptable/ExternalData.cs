using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace CBS.Scriptable
{
    public abstract class ExternalData : CBSScriptable
    {
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
    }
}

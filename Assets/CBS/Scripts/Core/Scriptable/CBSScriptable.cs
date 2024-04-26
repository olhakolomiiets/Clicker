using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBS.Scriptable
{
    public abstract class CBSScriptable : ScriptableObject
    {
        private static Dictionary<Type, CBSScriptable> Scribtables { get; set; } = new Dictionary<Type, CBSScriptable>();

        public abstract string ResourcePath { get; }

        internal virtual T Load<T>() where T : CBSScriptable
        {
            return Resources.Load<T>(ResourcePath);
        }

        internal virtual void Initialize()
        {

        }

        public void Save()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public static T Get<T>() where T : CBSScriptable
        {
            var dataType = typeof(T);
            bool containData = Scribtables.ContainsKey(dataType);
            if (containData)
            {
                return (T)Scribtables[dataType];
            }
            else
            {
                var newData = CreateInstance<T>().Load<T>();
                newData.Initialize();
                Scribtables[dataType] = newData;
                return newData;
            }
        }
    }
}

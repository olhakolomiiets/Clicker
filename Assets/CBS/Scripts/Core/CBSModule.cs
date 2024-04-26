using CBS.Context;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CBS
{
    public abstract class CBSModule
    {
        private readonly string CoroutineRunnerTitle = "[CBSCorotineRunner]";
        private static Dictionary<Type, CBSModule> Modules { get; set; } = new Dictionary<Type, CBSModule>();

        private ICoroutineRunner CoroutineRunnerInstance;
        public ICoroutineRunner CoroutineRunner
        {
            get
            {
                if (CoroutineRunnerInstance == null)
                {
                    var monoContainer = new GameObject(CoroutineRunnerTitle);
                    UnityEngine.Object.DontDestroyOnLoad(monoContainer);
                    CoroutineRunnerInstance = monoContainer.AddComponent<CoroutineRunner>();
                }
                return CoroutineRunnerInstance;
            }
        }

        public CBSModule() => Init();

        protected virtual void Init() { }

        public virtual void Bind() { }

        public static T Get<T>() where T : CBSModule, new()
        {
            var moduleType = typeof(T);
            bool containModule = Modules.ContainsKey(moduleType);
            if (containModule)
            {
                return (T)Modules[moduleType];
            }
            else
            {
                var newModule = new T();
                Modules[moduleType] = newModule;
                return newModule;
            }
        }

        internal void LogoutProcces()
        {
            foreach (var module in Modules)
                module.Value?.OnLogout();
        }

        protected virtual void OnLogout() { }
    }
}

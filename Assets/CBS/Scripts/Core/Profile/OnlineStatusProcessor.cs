using CBS.Context;
using CBS.Models;
using CBS.Scriptable;
using PlayFab.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CBS
{
    public class OnlineStatusProcessor : IDisposable
    {
        private readonly IProfile Profile;
        private readonly ProfileConfigData ConfigData;
        private readonly OnlineStatusBehavior UpdateBehavior;
        private readonly int UpdateInterval;
        private readonly List<string> Triggers;
        private readonly ICoroutineRunner CoroutineRunner;

        private bool IsStarted { get; set; }

        public OnlineStatusProcessor(IProfile profile, ProfileConfigData configData, ICoroutineRunner coroutineRunner)
        {
            Profile = profile;
            ConfigData = configData;
            UpdateBehavior = ConfigData.OnlineUpdateBehavior;
            UpdateInterval = ConfigData.UpdateInterval;
            Triggers = ConfigData.TriggerMethods;
            CoroutineRunner = coroutineRunner;
        }

        public void StartUpdate()
        {
            IsStarted = true;
            if (UpdateBehavior == OnlineStatusBehavior.LOOP_UPDATE)
            {
                CoroutineRunner.StartCoroutine(UpdateLoop());
            }
            else if (UpdateBehavior == OnlineStatusBehavior.WHEN_SPECIFIC_CALLS)
            {
                PlayFabHttp.ApiProcessingEventHandler += OnApiCall;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        private IEnumerator UpdateLoop()
        {
            while (IsStarted)
            {
                yield return new WaitForSeconds(UpdateInterval);
                Profile.UpdateOnlineState();
            }
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                Dispose();
            }
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
#endif
        private void OnApiCall(ApiProcessingEventArgs args)
        {
            var type = args.Request.GetType();
            if (type == typeof(PlayFab.CloudScriptModels.ExecuteFunctionRequest))
            {
                var request = args.Request as PlayFab.CloudScriptModels.ExecuteFunctionRequest;
                if (request != null && Triggers != null)
                {
                    var functionName = request.FunctionName;
                    if (Triggers.Contains(functionName))
                    {
                        Profile.UpdateOnlineState();
                    }
                }
            }
        }

        public void Dispose()
        {
            if (UpdateBehavior == OnlineStatusBehavior.WHEN_SPECIFIC_CALLS)
            {
                PlayFabHttp.ApiProcessingEventHandler -= OnApiCall;
            }
            IsStarted = false;
            CoroutineRunner.StopAllCoroutines();
        }
    }
}

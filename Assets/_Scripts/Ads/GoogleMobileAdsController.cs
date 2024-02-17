using System;
using System.Collections.Generic;
using UnityEngine;
using GoogleMobileAds.Api;
using UnityEngine.SceneManagement;

namespace GoogleMobileAds.Samples
{

    [AddComponentMenu("GoogleMobileAds/GoogleMobileAdsController")]
    public class GoogleMobileAdsController : MonoBehaviour
    {
        private static bool _isInitialized;


        private void Start()
        {
            if (_isInitialized)
            {
                return;
            }

            MobileAds.SetiOSAppPauseOnBackground(true);

            MobileAds.RaiseAdEventsOnUnityMainThread = true;

            List<string> deviceIds = new List<string>()
            {
                AdRequest.TestDeviceSimulator,
                // Add your test device IDs (replace with your own device IDs).
                #if UNITY_IPHONE
                "96e23e80653bb28980d3f40beb58915c"
                #elif UNITY_ANDROID
                "75EF8D155528C04DACBBA6F36F433035"
                #endif
            };

            RequestConfiguration requestConfiguration = new RequestConfiguration
            {
                TestDeviceIds = deviceIds
            };
            MobileAds.SetRequestConfiguration(requestConfiguration);

            Debug.Log("Google Mobile Ads Initializing.");
            MobileAds.Initialize((InitializationStatus initstatus) =>
            {
                if (initstatus == null)
                {
                    Debug.LogError("Google Mobile Ads initialization failed.");
                    return;
                }

                var adapterStatusMap = initstatus.getAdapterStatusMap();
                if (adapterStatusMap != null)
                {
                    foreach (var item in adapterStatusMap)
                    {
                        Debug.Log(string.Format("Adapter {0} is {1}",
                            item.Key,
                            item.Value.InitializationState));
                    }
                }

                Debug.Log("Google Mobile Ads initialization complete.");
                _isInitialized = true;
            });
        }

        public void OpenAdInspector()
        {
            Debug.Log("Opening ad Inspector.");
            MobileAds.OpenAdInspector((AdInspectorError error) =>
            {
                if (error != null)
                {
                    Debug.Log("Ad Inspector failed to open with error: " + error);
                    return;
                }

                Debug.Log("Ad Inspector opened successfully.");
            });
        }
    }
}
using System;
using UnityEngine;
using GoogleMobileAds.Api;

namespace GoogleMobileAds.Sample
{
    [AddComponentMenu("GoogleMobileAds/BannerViewController")]
    public class BannerViewController : MonoBehaviour
    {

#if UNITY_ANDROID
        private const string _adUnitId = "ca-app-pub-3940256099942544/6300978111";
#elif UNITY_IPHONE
        private const string _adUnitId = "ca-app-pub-3940256099942544/2934735716";
#else
        private const string _adUnitId = "unused";
#endif

        private BannerView _bannerView;

        public void OnGUI()
        {
            GUI.skin.label.fontSize = 60;
            Rect textOutputRect = new Rect(
              0.15f * Screen.width,
              0.25f * Screen.height,
              0.7f * Screen.width,
              0.3f * Screen.height);
        }

        public void CreateBannerView()
        {
            Debug.Log("Creating banner view.");
           
            if (_bannerView != null)
            {
                DestroyAd();
            }

            AdSize adaptiveSize = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
            _bannerView = new BannerView(_adUnitId, adaptiveSize, AdPosition.Bottom);

            ListenToAdEvents();

            Debug.Log("Banner view created.");
        }


        public void LoadAd()
        {
            if (_bannerView == null)
            {
                CreateBannerView();
            }

            var adRequest = new AdRequest();

            Debug.Log("Loading banner ad.");
            _bannerView.LoadAd(adRequest);
        }

        public void ShowAd()
        {
            if (_bannerView != null)
            {
                Debug.Log("Showing banner view.");
                _bannerView.Show();
            }
        }

        public void HideAd()
        {
            if (_bannerView != null)
            {
                Debug.Log("Hiding banner view.");
                _bannerView.Hide();
            }
        }

        public void DestroyAd()
        {
            if (_bannerView != null)
            {
                Debug.Log("Destroying banner view.");
                _bannerView.Destroy();
                _bannerView = null;
            }

            //AdLoadedStatus?.SetActive(false);
        }

        public void LogResponseInfo()
        {
            if (_bannerView != null)
            {
                var responseInfo = _bannerView.GetResponseInfo();
                if (responseInfo != null)
                {
                    UnityEngine.Debug.Log(responseInfo);
                }
            }
        }

        private void ListenToAdEvents()
        {
            // Raised when an ad is loaded into the banner view.
            _bannerView.OnBannerAdLoaded += () =>
            {
                Debug.Log("Banner view loaded an ad with response : "
                    + _bannerView.GetResponseInfo());

                // Inform the UI that the ad is ready.
               // AdLoadedStatus?.SetActive(true);
            };
            // Raised when an ad fails to load into the banner view.
            _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                Debug.LogError("Banner view failed to load an ad with error : " + error);
            };
            // Raised when the ad is estimated to have earned money.
            _bannerView.OnAdPaid += (AdValue adValue) =>
            {
                Debug.Log(String.Format("Banner view paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
            };
            // Raised when an impression is recorded for an ad.
            _bannerView.OnAdImpressionRecorded += () =>
            {
                Debug.Log("Banner view recorded an impression.");
            };
            // Raised when a click is recorded for an ad.
            _bannerView.OnAdClicked += () =>
            {
                Debug.Log("Banner view was clicked.");
            };
            // Raised when an ad opened full screen content.
            _bannerView.OnAdFullScreenContentOpened += () =>
            {
                Debug.Log("Banner view full screen content opened.");
            };
            // Raised when the ad closed full screen content.
            _bannerView.OnAdFullScreenContentClosed += () =>
            {
                Debug.Log("Banner view full screen content closed.");
            };
        }
    }
}
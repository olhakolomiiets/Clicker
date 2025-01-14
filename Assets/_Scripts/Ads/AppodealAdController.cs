using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;
using AppodealStack.Monetization.Api;
using AppodealStack.Monetization.Common;
using UnityEngine.Events;

public class AppodealAdController : MonoBehaviour
{
    #region Constants

    private const string InterstitialShow = "Show Interstitial";
    private const string InterstitialCache = "Cache Interstitial";
    private const string InterstitialCaching = "Caching Interstitial";
    private const string RewardedVideoShow = "Show Rewarded Video";
    private const string RewardedVideoCache = "Cache Rewarded Video";
    private const string RewardedVideoCaching = "Caching Rewarded Video";

    #endregion

    #region Appodeal Application Key

#if UNITY_EDITOR && !UNITY_ANDROID && !UNITY_IOS
        private const string AppKey = "";
#elif UNITY_ANDROID
    private const string AppKey = "79c2066d1b83c6b6db26d9b375c21217564af68211409e84";
#elif UNITY_IOS
        private const string AppKey = "";
#else
	    private const string AppKey = "";
#endif

    #endregion

    #region UNITY EVENTS

    [HideInInspector] public UnityEvent OnUserEarnedRewardEvent;
    [HideInInspector] public UnityEvent OnAdClosedEvent;
    [HideInInspector] public UnityEvent RewardedAdLoadedEvent;
    [HideInInspector] public UnityEvent RewardedAdLoadedWithErrorEvent;

    #endregion


    #region MonoBehavior Functions
    private void Start()
    {
        SetAppodealCallbacks();

        int adTypes = AppodealAdType.Interstitial | AppodealAdType.Banner | AppodealAdType.RewardedVideo | AppodealAdType.Mrec;
        Appodeal.Initialize(AppKey, adTypes);

        Appodeal.Cache(AppodealAdType.Interstitial);
        Appodeal.Cache(AppodealAdType.RewardedVideo);
    }

    private void OnDestroy()
    {
        Appodeal.Destroy(AppodealAdType.Banner);
    }
    #endregion

    #region Appodeal Monetization
    public void ShowInterstitial()
    {
        if (Appodeal.IsLoaded(AppodealAdType.Interstitial) && Appodeal.CanShow(AppodealAdType.Interstitial, "default") && !Appodeal.IsPrecache(AppodealAdType.Interstitial))
        {
            Appodeal.Show(AppodealShowStyle.Interstitial);
        }
        else if (!Appodeal.IsAutoCacheEnabled(AppodealAdType.Interstitial))
        {
            Appodeal.Cache(AppodealAdType.Interstitial);
        }
    }

    public void ShowRewardedVideo()
    {
        if (Appodeal.IsLoaded(AppodealAdType.RewardedVideo) && Appodeal.CanShow(AppodealAdType.RewardedVideo, "default"))
        {
            Appodeal.Show(AppodealShowStyle.RewardedVideo);
        }
        else if (!Appodeal.IsAutoCacheEnabled(AppodealAdType.RewardedVideo))
        {
            Appodeal.Cache(AppodealAdType.RewardedVideo);
        }
    }

    public void ShowBannerBottom()
    {
        Appodeal.Show(AppodealShowStyle.BannerBottom, "default");
    }

    public void ShowBannerTop()
    {
        Appodeal.Show(AppodealShowStyle.BannerTop, "default");
    }

    public void ShowBannerLeft()
    {
        Appodeal.Show(AppodealShowStyle.BannerLeft, "default");
    }

    public void ShowBannerRight()
    {
        Appodeal.Show(AppodealShowStyle.BannerRight, "default");
    }

    public void HideBanner()
    {
        Appodeal.Hide(AppodealAdType.Banner);
    }

    public void ShowBannerView()
    {
        Appodeal.ShowBannerView(AppodealViewPosition.VerticalBottom, AppodealViewPosition.HorizontalCenter, "default");
    }

    public void HideBannerView()
    {
        Appodeal.HideBannerView();
    }

    public void ShowMrecView()
    {
        Appodeal.ShowMrecView(AppodealViewPosition.VerticalTop, AppodealViewPosition.HorizontalCenter, "default");
    }

    public void HideMrecView()
    {
        Appodeal.HideMrecView();
    }

    #endregion

    #region Appodeal Callbacks Initialization

    private void SetAppodealCallbacks()
    {
        AppodealCallbacks.Sdk.OnInitialized += OnInitializationFinished;

        AppodealCallbacks.AdRevenue.OnReceived += (_, args) =>
        {
            Debug.Log($"[APDUnity] [Callback] OnAdRevenueReceived({args.Ad.ToJsonString(true)})");
        };

        AppodealCallbacks.Mrec.OnLoaded += (_, args) => OnMrecLoaded(args.IsPrecache);
        AppodealCallbacks.Mrec.OnFailedToLoad += (_, _) => OnMrecFailedToLoad();
        AppodealCallbacks.Mrec.OnShown += (_, _) => OnMrecShown();
        AppodealCallbacks.Mrec.OnShowFailed += (_, _) => OnMrecShowFailed();
        AppodealCallbacks.Mrec.OnClicked += (_, _) => OnMrecClicked();
        AppodealCallbacks.Mrec.OnExpired += (_, _) => OnMrecExpired();

        AppodealCallbacks.Banner.OnLoaded += OnBannerLoaded;
        AppodealCallbacks.Banner.OnFailedToLoad += OnBannerFailedToLoad;
        AppodealCallbacks.Banner.OnShown += OnBannerShown;
        AppodealCallbacks.Banner.OnShowFailed += OnBannerShowFailed;
        AppodealCallbacks.Banner.OnClicked += OnBannerClicked;
        AppodealCallbacks.Banner.OnExpired += OnBannerExpired;

        AppodealCallbacks.Interstitial.OnLoaded += OnInterstitialLoaded;
        AppodealCallbacks.Interstitial.OnFailedToLoad += OnInterstitialFailedToLoad;
        AppodealCallbacks.Interstitial.OnShown += OnInterstitialShown;
        AppodealCallbacks.Interstitial.OnShowFailed += OnInterstitialShowFailed;
        AppodealCallbacks.Interstitial.OnClosed += OnInterstitialClosed;
        AppodealCallbacks.Interstitial.OnClicked += OnInterstitialClicked;
        AppodealCallbacks.Interstitial.OnExpired += OnInterstitialExpired;

        AppodealCallbacks.RewardedVideo.OnLoaded += OnRewardedVideoLoaded;
        AppodealCallbacks.RewardedVideo.OnFailedToLoad += OnRewardedVideoFailedToLoad;
        AppodealCallbacks.RewardedVideo.OnShown += OnRewardedVideoShown;
        AppodealCallbacks.RewardedVideo.OnShowFailed += OnRewardedVideoShowFailed;
        AppodealCallbacks.RewardedVideo.OnClosed += OnRewardedVideoClosed;
        AppodealCallbacks.RewardedVideo.OnFinished += OnRewardedVideoFinished;
        AppodealCallbacks.RewardedVideo.OnClicked += OnRewardedVideoClicked;
        AppodealCallbacks.RewardedVideo.OnExpired += OnRewardedVideoExpired;
    }

    #endregion

    #region Initialization Callback

    private void OnInitializationFinished(object sender, SdkInitializedEventArgs e)
    {
        string output = e.Errors == null ? String.Empty : String.Join(", ", e.Errors);
        Debug.Log($"[APDUnity] [Callback] OnInitializationFinished(errors:[{output}])");

        Debug.Log($"[APDUnity] [Appodeal] IsAutoCacheEnabled() for banner: {Appodeal.IsAutoCacheEnabled(AppodealAdType.Banner)}");
        Debug.Log($"[APDUnity] [Appodeal] IsInitialized() for banner: {Appodeal.IsInitialized(AppodealAdType.Banner)}");
        Debug.Log($"[APDUnity] [Appodeal] IsSmartBannersEnabled(): {Appodeal.IsSmartBannersEnabled()}");
        Debug.Log($"[APDUnity] [Appodeal] GetUserId(): {Appodeal.GetUserId()}");
        Debug.Log($"[APDUnity] [Appodeal] GetSegmentId(): {Appodeal.GetSegmentId()}");
        Debug.Log($"[APDUnity] [Appodeal] GetReward(): {Appodeal.GetReward().ToJsonString()}");
        Debug.Log($"[APDUnity] [Appodeal] GetNativeSDKVersion(): {Appodeal.GetNativeSDKVersion()}");

        var networksList = Appodeal.GetNetworks(AppodealAdType.RewardedVideo);
        output = networksList == null ? String.Empty : String.Join(", ", (networksList.ToArray()));
        Debug.Log($"[APDUnity] [Appodeal] GetNetworks() for RV: {output}");

        networksList = Appodeal.GetNetworks(AppodealAdType.Interstitial);
        output = networksList == null ? String.Empty : String.Join(", ", (networksList.ToArray()));
        Debug.Log($"[APDUnity] [Appodeal] GetNetworks() for Interstitial: {output}");

        networksList = Appodeal.GetNetworks(AppodealAdType.Banner);
        output = networksList == null ? String.Empty : String.Join(", ", (networksList.ToArray()));
        Debug.Log($"[APDUnity] [Appodeal] GetNetworks() for Banner: {output}");

        networksList = Appodeal.GetNetworks(AppodealAdType.Mrec);
        output = networksList == null ? String.Empty : String.Join(", ", (networksList.ToArray()));
        Debug.Log($"[APDUnity] [Appodeal] GetNetworks() for Mrec: {output}");
    }

    #endregion

    #region MrecAd Callbacks

    private void OnMrecLoaded(bool isPrecache)
    {
        Debug.Log($"[APDUnity] [Callback] OnMrecLoaded(bool isPrecache:{isPrecache})");
        Debug.Log($"[APDUnity] GetPredictedEcpm(): {Appodeal.GetPredictedEcpm(AppodealAdType.Mrec)}");
    }

    private void OnMrecFailedToLoad()
    {
        Debug.Log("[APDUnity] [Callback] OnMrecFailedToLoad()");
    }

    private void OnMrecShown()
    {
        Debug.Log("[APDUnity] [Callback] OnMrecShown()");
    }

    private void OnMrecShowFailed()
    {
        Debug.Log("[APDUnity] [Callback] OnMrecShowFailed()");
    }

    private void OnMrecClicked()
    {
        Debug.Log("[APDUnity] [Callback] OnMrecClicked()");
    }

    private void OnMrecExpired()
    {
        Debug.Log("[APDUnity] [Callback] OnMrecExpired()");
    }

    #endregion

    #region BannerAd Callbacks

    private void OnBannerLoaded(object sender, BannerLoadedEventArgs e)
    {
        Debug.Log($"[APDUnity] [Callback] OnBannerLoaded(int height:{e.Height}, bool precache:{e.IsPrecache})");
        Debug.Log($"[APDUnity] GetPredictedEcpm(): {Appodeal.GetPredictedEcpm(AppodealAdType.Banner)}");
    }

    private void OnBannerFailedToLoad(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnBannerFailedToLoad()");
    }

    private void OnBannerShown(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnBannerShown()");
    }

    private void OnBannerShowFailed(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnBannerShowFailed()");
    }

    private void OnBannerClicked(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnBannerClicked()");
    }

    private void OnBannerExpired(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnBannerExpired()");
    }

    #endregion

    #region InterstitialAd Callbacks

    private void OnInterstitialLoaded(object sender, AdLoadedEventArgs e)
    {
        Debug.Log($"[APDUnity] [Callback] OnInterstitialLoaded(bool isPrecache:{e.IsPrecache})");
        Debug.Log($"[APDUnity] GetPredictedEcpm(): {Appodeal.GetPredictedEcpm(AppodealAdType.Interstitial)}");
    }

    private void OnInterstitialFailedToLoad(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnInterstitialFailedToLoad()");
    }

    private void OnInterstitialShowFailed(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnInterstitialShowFailed()");
    }

    private void OnInterstitialShown(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnInterstitialShown()");
    }

    private void OnInterstitialClosed(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnInterstitialClosed()");
    }

    private void OnInterstitialClicked(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnInterstitialClicked()");
    }

    private void OnInterstitialExpired(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnInterstitialExpired()");
    }

    #endregion

    #region RewardedVideoAd Callbacks

    private void OnRewardedVideoLoaded(object sender, AdLoadedEventArgs e)
    {
        RewardedAdLoadedEvent.Invoke();
        Debug.Log($"[APDUnity] [Callback] OnRewardedVideoLoaded(bool isPrecache:{e.IsPrecache})");
        Debug.Log($"[APDUnity] GetPredictedEcpm(): {Appodeal.GetPredictedEcpm(AppodealAdType.RewardedVideo)}");
    }

    private void OnRewardedVideoFailedToLoad(object sender, EventArgs e)
    {
        RewardedAdLoadedWithErrorEvent.Invoke();
        Debug.Log("[APDUnity] [Callback] OnRewardedVideoFailedToLoad()");
    }

    private void OnRewardedVideoShowFailed(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnRewardedVideoShowFailed()");
    }

    private void OnRewardedVideoShown(object sender, EventArgs e)
    {
        RewardedAdLoadedWithErrorEvent.Invoke();
        Debug.Log("[APDUnity] [Callback] OnRewardedVideoShown()");
    }

    private void OnRewardedVideoClosed(object sender, RewardedVideoClosedEventArgs e)
    {
        OnAdClosedEvent.Invoke();
        Debug.Log($"[APDUnity] [Callback] OnRewardedVideoClosed(bool finished:{e.Finished})");
    }

    private void OnRewardedVideoFinished(object sender, RewardedVideoFinishedEventArgs e)
    {
        OnUserEarnedRewardEvent.Invoke();
        Debug.Log($"[APDUnity] [Callback] OnRewardedVideoFinished(double amount:{e.Amount}, string name:{e.Currency})");
    }

    private void OnRewardedVideoExpired(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnRewardedVideoExpired()");
    }

    private void OnRewardedVideoClicked(object sender, EventArgs e)
    {
        Debug.Log("[APDUnity] [Callback] OnRewardedVideoClicked()");
    }

    #endregion
}

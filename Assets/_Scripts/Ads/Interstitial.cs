using Firebase.Analytics;
using UnityEngine;

public class Interstitial : MonoBehaviour
{

    #region EDITOR FIELDS

    [SerializeField, Range(1, 900)] private int delayBetweenAds = 180;
    [SerializeField] private GoogleMobileAds.Sample.InterstitialAdController _adController;

    #endregion

    #region PRIVATE FIELDS

    private static float lastAdTime = Mathf.NegativeInfinity;

    #endregion

    private void OnEnable()
    {       
        PlayerPrefs.SetInt("HowManyGamesPlayed", PlayerPrefs.GetInt("HowManyGamesPlayed") + 1);

        var _playCount = PlayerPrefs.GetInt("HowManyGamesPlayed");

        FirebaseAnalytics.LogEvent(name: "games_count");
    }

    void Start()
    {
        LoadInterstitialAd();
    }

    public void LoadInterstitialAd()
    {
        if(PlayerPrefs.GetInt("HowManyGamesPlayed") < 3)
        {
            return;
        }

        if (PlayerPrefs.GetInt("adsRemoved") == 0)
        {
            if ((Time.time - lastAdTime) > (float)delayBetweenAds)
            {
                _adController.LoadAd();

                lastAdTime = Time.time;
                Debug.Log("Show Interstitial With Delay Between Ads " + lastAdTime);
            }
        }
    }
    public void ShowInterstitialAd()
    {
        _adController.ShowAd();

        FirebaseAnalytics.LogEvent(name: "interstitial_ad_showed");
    }
}

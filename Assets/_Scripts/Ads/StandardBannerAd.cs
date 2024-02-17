using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StandardBannerAd : MonoBehaviour
{
    [SerializeField] private GoogleMobileAds.Sample.StandardBannerViewController _adController;

    private void OnEnable()
    {
        if (PlayerPrefs.GetInt("adsRemoved") == 0)
        {
            _adController.LoadAd();
        }
    }

    private void OnDisable()
    {
        _adController.DestroyAd();
    }
}

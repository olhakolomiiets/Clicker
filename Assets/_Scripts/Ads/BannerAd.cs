using GoogleMobileAds.Sample;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BannerAd : MonoBehaviour
{
    [SerializeField] private GoogleMobileAds.Sample.BannerViewController _adController;

    private void OnEnable()
    {
        _adController = FindAnyObjectByType<BannerViewController>();

        if (PlayerPrefs.GetInt("adsRemoved") == 0 && _adController != null)
        {
            _adController.LoadAd();
        }
    }

    private void OnDisable()
    {
        _adController.DestroyAd();
    }
}

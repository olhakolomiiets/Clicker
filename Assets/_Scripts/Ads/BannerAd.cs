using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BannerAd : MonoBehaviour
{
    [SerializeField] private GoogleMobileAds.Sample.BannerViewController _adController;

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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Google.Play.Review;
using Firebase.Analytics;

public class GoogleReviewManager : MonoBehaviour
{
    private ReviewManager _reviewManager;
    private PlayReviewInfo _playReviewInfo;

    private void OnEnable()
    {
        FirebaseAnalytics.LogEvent(name: "open_reviewWindow");
    }

    IEnumerator RequestReviews()
    {
        _reviewManager = new ReviewManager();

        var requestFlowOperation = _reviewManager.RequestReviewFlow();
        yield return requestFlowOperation;

        if (requestFlowOperation.Error != ReviewErrorCode.NoError)
        {
            yield break;
        }

        _playReviewInfo = requestFlowOperation.GetResult();

        var launchFlowOperation = _reviewManager.LaunchReviewFlow(_playReviewInfo);
        yield return launchFlowOperation;
        _playReviewInfo = null;
        if (launchFlowOperation.Error != ReviewErrorCode.NoError)
        {          
            yield break;
        }
    }

    public void GetReviewOnGooglePlay()
    {
        StartCoroutine(RequestReviews());     
        FirebaseAnalytics.LogEvent(name: "open_googleReview");
    }
}

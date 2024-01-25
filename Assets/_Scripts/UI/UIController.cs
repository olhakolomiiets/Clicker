using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private RectTransform _controlButton;

    [SerializeField] private RectTransform _upgradeStorePanel;
    [SerializeField] private float _panelTopPosY, _panelMiddlePosY;
    [SerializeField] private float _tweenDuration;

    private bool isDisplayed;

    private void Start()
    {
       
    }

    public void ToggleUpgradeStorePanel()
    {
        if(isDisplayed)
        {
            _upgradeStorePanel.DOAnchorPosY(_panelMiddlePosY, _tweenDuration);
            _controlButton.DORotate(new Vector3(0, 0, 0), _tweenDuration);
            isDisplayed = false;
        }
        else
        {
            _upgradeStorePanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _controlButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
        }


    }


}

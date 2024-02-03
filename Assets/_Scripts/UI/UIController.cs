using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [Header("Shop Panel")]
    [SerializeField] private RectTransform _toggleButton;
    [SerializeField] private RectTransform _shopPanel;
    [SerializeField] private float _panelTopPosY, _panelMiddlePosY;
    [SerializeField] private float _tweenDuration;

    [Header("Creation")]
    [SerializeField] private RectTransform _creationItemsParent;
    [SerializeField] private Button _creationButton;

    [Header("Upgrade")]
    [SerializeField] private RectTransform _upgradeItemsParent;
    [SerializeField] private Button _upgradeButton;

    [Header("Shop")]
    [SerializeField] private RectTransform _shopItemsParent;
    [SerializeField] private Button _shopButton;

    private bool isDisplayed;

    [SerializeField] private GameObject _planet;

    private DragRotateGPT _planetRotator;

    private void Start()
    {
        _planetRotator = _planet.GetComponent<DragRotateGPT>();
    }

    public void ToggleUpgradeStorePanel()
    {
        if(isDisplayed)
        {
            _shopPanel.DOAnchorPosY(_panelMiddlePosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 0), _tweenDuration);
            isDisplayed = false;
            SetStartPos();
            _planetRotator.enabled = true;
        }
        else
        {
            _creationItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);
            _creationButton.Select();

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;

            _planetRotator.enabled = false;
        }
    }

    public void ShopsToggle(int index)
    {
        switch (index)
        {
            case 0:
                _creationItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);

                _upgradeItemsParent.DOAnchorPos(new Vector3(0, -1110, 0), 0.25f);
                _shopItemsParent.DOAnchorPos(new Vector3(1090, -350, 0), 0.25f);

                break;

            case 1:
                _upgradeItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);

                _creationItemsParent.DOAnchorPos(new Vector3(-1090, -350, 0), 0.25f);
                _shopItemsParent.DOAnchorPos(new Vector3(1090, -350, 0), 0.25f);

                break;

            case 2:
                _shopItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);

                _creationItemsParent.DOAnchorPos(new Vector3(-1090, -350, 0), 0.25f);
                _upgradeItemsParent.DOAnchorPos(new Vector3(0, -1110, 0), 0.25f);

                break;
        }
    }

    private void SetStartPos()
    {
        _creationItemsParent.DOAnchorPos(new Vector3(-1090, -350, 0), 0.25f);
        _upgradeItemsParent.DOAnchorPos(new Vector3(1090, -350, 0), 0.25f);
        _shopItemsParent.DOAnchorPos(new Vector3(1090, -350, 0), 0.25f);
    }

}

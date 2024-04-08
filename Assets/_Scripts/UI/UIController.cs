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
    private ObjectPlaceRotator _objectPlaceRotator; 

    private void Start()
    {
        _planetRotator = _planet.GetComponent<DragRotateGPT>();
        _objectPlaceRotator = _planet.GetComponent<ObjectPlaceRotator>();
    }

    public void ToggleUpgradeStorePanel()
    {
        if(isDisplayed)
        {
            _shopPanel.DOAnchorPosY(_panelMiddlePosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 0), _tweenDuration);
            isDisplayed = false;
            SetStartPos();
            if (!_objectPlaceRotator.isRotating)
            {
                _planetRotator.enabled = true;
            }          
        }
        else
        {
            _creationItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);
            _creationButton.Select();

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
            ColorToggle(0);
            _planetRotator.enabled = false;
        }
    }

    public void ShopsToggle(int index)
    {
        Vector3 creationPos = new Vector3(0, -350, 0);
        Vector3 upgradePos = new Vector3(0, -1110, 0);
        Vector3 shopPos = new Vector3(1090, -350, 0);

        _creationItemsParent.DOAnchorPos(index == 0 ? creationPos : new Vector3(-1090, -350, 0), 0.25f);
        _upgradeItemsParent.DOAnchorPos(index == 1 ? creationPos : upgradePos, 0.25f);
        _shopItemsParent.DOAnchorPos(index == 2 ? creationPos : shopPos, 0.25f);

        ColorToggle(index);
    }


    private void ColorToggle(int i)
    {
        ColorBlock creationColors = _creationButton.colors;
        ColorBlock upgradeColors = _upgradeButton.colors;
        ColorBlock shopColors = _shopButton.colors;

        creationColors.normalColor = (i == 0) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;
        upgradeColors.normalColor = (i == 1) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;
        shopColors.normalColor = (i == 2) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;

        _creationButton.colors = creationColors;
        _upgradeButton.colors = upgradeColors;
        _shopButton.colors = shopColors;
    }


    private void SetStartPos()
    {
        _creationItemsParent.DOAnchorPos(new Vector3(-1090, -350, 0), 0.25f);
        _upgradeItemsParent.DOAnchorPos(new Vector3(1090, -350, 0), 0.25f);
        _shopItemsParent.DOAnchorPos(new Vector3(1090, -350, 0), 0.25f);
    }

}

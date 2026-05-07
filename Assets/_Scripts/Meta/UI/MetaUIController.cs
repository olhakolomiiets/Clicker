using DG.Tweening;
using Exoa.Cameras;
using System;
using System.Collections;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class MetaUIController : MonoBehaviour
{
    [SerializeField] private GameObject _backgroundImage;

    [Header("Shop Panel")]
    [SerializeField] private RectTransform _toggleButton;
    [SerializeField] private RectTransform _shopPanel;
    [SerializeField] private float _panelTopPosY, _panelMiddlePosY;
    [SerializeField] private float _tweenDuration;

    [Header("Creation")]
    [SerializeField] private RectTransform _variantItemsParent;
    [SerializeField] private Button _variantButton;

    [Header("Upgrade")]
    [SerializeField] private RectTransform _upgradeItemsParent;
    [SerializeField] private Button _upgradeButton;

    [Header("Shop")]
    [SerializeField] private RectTransform _shopItemsParent;
    [SerializeField] private Button _shopButton;

    // [Header("Planet Levels")]
    // [SerializeField] private RectTransform _menuArrow;
    // [SerializeField] private GameObject _bg;
    // [SerializeField] private RectTransform _levelsPanel;
    // [SerializeField] private float _levelsPanelTopPosX, _levelsPanelMiddlePosX;

    [HideInInspector] public bool isDisplayed;
    // [HideInInspector] public bool isLevelsDisplayed;

    [SerializeField] private GameObject _planet;

    [Header("Planet Zoom")]
    [SerializeField] private DragRotateGPT _planetRotator;
    [SerializeField] private float zoomDuration;

    private MetaObjectPlaceRotator _objectPlaceRotator;

    public event Action OnTutorialStepCompleted, OnTutorialNonCompleted, OnStorePanelDisplayed;
    public event Action<int> OnShowUpgradeOrShopTutorial, OnLoadTutorialStep;
    public event Action<int, int> OnLoadTutorialNextStep;

    private void Start()
    {
        _planetRotator = _planet.GetComponent<DragRotateGPT>();
        _objectPlaceRotator = _planet.GetComponent<MetaObjectPlaceRotator>();
    }

    public void OpenShop()
    {
        if (isDisplayed)
        {
            ShopsToggle(2);
        }
        else
        {
            _planetRotator.SaveAndZoomToMax(zoomDuration);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
            _planetRotator.enabled = false;
            ShopsToggle(2);
        }            
    }
    public void ToggleUpgradeStorePanel()
    {
        if (isDisplayed)
        {
            _planetRotator.ZoomBack(zoomDuration);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(1f));
            }

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
            _planetRotator.SaveAndZoomToMax(zoomDuration);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _variantItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);
            _variantButton.Select();

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
            ColorToggle(0);
            _planetRotator.enabled = false;
        }
    }

    // public void ToggleLevelsPanel()
    // {
    //     if (isLevelsDisplayed)
    //     {
    //         _bg.SetActive(false);

    //         if (_backgroundImage != null)
    //         {
    //             StartCoroutine(SmoothScaleBackground(1f));
    //         }

    //         _levelsPanel.DOAnchorPosX(_levelsPanelMiddlePosX, _tweenDuration);
    //         _menuArrow.DORotate(new Vector3(0, 0, 0), _tweenDuration);
    //         isLevelsDisplayed = false;

    //         if (!_objectPlaceRotator.isRotating)
    //         {
    //             _planetRotator.enabled = true;
    //         }
    //     }
    //     else
    //     {
    //         _bg.SetActive(true);

    //         if (_backgroundImage != null)
    //         {
    //             StartCoroutine(SmoothScaleBackground(0.93f));
    //         }

    //         _levelsPanel.DOAnchorPosX(_levelsPanelTopPosX, _tweenDuration);

    //         _menuArrow.DORotate(new Vector3(0, 0, 180), _tweenDuration);
    //         isLevelsDisplayed = true;
    //         _planetRotator.enabled = false;
    //     }
    // }

    IEnumerator SmoothScaleBackground(float scale)
    {
        Vector3 initialScale = _backgroundImage.transform.localScale;
        Vector3 targetScale = new Vector3(scale, scale, scale);

        float elapsedTime = 0f;

        while (elapsedTime < 0.8f)
        {
            elapsedTime += Time.deltaTime;
            _backgroundImage.transform.localScale = Vector3.Lerp(initialScale, targetScale, elapsedTime / 0.8f);
            yield return null;
        }
        _backgroundImage.transform.localScale = targetScale;
    }

    public void ShopsToggle(int index)
    {
        Vector3 visiblePos = new Vector3(0, -350, 0);
        Vector3 hidePos = new Vector3(0, -1130, 0);

        _variantItemsParent.DOAnchorPos(index == 0 ? visiblePos : hidePos, 0.25f);
        _upgradeItemsParent.DOAnchorPos(index == 1 ? visiblePos : hidePos, 0.25f);
        _shopItemsParent.DOAnchorPos(index == 2 ? visiblePos : hidePos, 0.25f);

        ColorToggle(index);
    }

    private void ColorToggle(int i)
    {
        ColorBlock variantColors = _variantButton.colors;
        ColorBlock upgradeColors = _upgradeButton.colors;
        ColorBlock shopColors = _shopButton.colors;

        variantColors.normalColor = (i == 0) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;
        upgradeColors.normalColor = (i == 1) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;
        shopColors.normalColor = (i == 2) ? new Color(0.12f, 0.45f, 1f, 0.5f) : Color.white;

        _variantButton.colors = variantColors;
        _upgradeButton.colors = upgradeColors;
        _shopButton.colors = shopColors;
    }
    private void SetStartPos()
    {
        _variantItemsParent.DOAnchorPos(new Vector3(0, -1130, 0), 0.25f);
        _upgradeItemsParent.DOAnchorPos(new Vector3(0, -1130, 0), 0.25f);
        _shopItemsParent.DOAnchorPos(new Vector3(0, -1130, 0), 0.25f);
    }
}

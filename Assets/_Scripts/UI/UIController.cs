using CBS.Scriptable;
using CBS.UI;
using DG.Tweening;
using Exoa.Cameras;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private CameraPerspective _camera;
    [SerializeField] private Vector3 _cameraPos;
    [SerializeField] private float _cameraDistance;
    [SerializeField] private GameObject _backgroundImage;

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

    [Header("Planet Levels")]
    [SerializeField] private RectTransform _menuButton;
    [SerializeField] private RectTransform _levelsPanel;
    [SerializeField] private float _levelsPanelTopPosX, _levelsPanelMiddlePosX;

    [HideInInspector] public bool isDisplayed;
    [HideInInspector] public bool isLevelsDisplayed;

    [SerializeField] private GameObject _planet;

    private DragRotateGPT _planetRotator;
    private ObjectPlaceRotator _objectPlaceRotator;
    private Vector3 _startCameraPos;
    private float _startCameraDistance;

    private void Start()
    {
        _planetRotator = _planet.GetComponent<DragRotateGPT>();
        _objectPlaceRotator = _planet.GetComponent<ObjectPlaceRotator>();
    }

    public void ToggleUpgradeStorePanel()
    {
        if (isDisplayed)
        {
            _camera.MoveCameraTo(_startCameraDistance);
            _camera.MoveCameraToInstant(_startCameraPos);

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
            _startCameraDistance = _camera.FinalDistance;
            _startCameraPos = _camera.gameObject.transform.position;

            _camera.MoveCameraTo(_cameraDistance);
            _camera.MoveCameraToInstant(_cameraPos);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _creationItemsParent.DOAnchorPos(new Vector3(0, -350, 0), 0.25f);
            _creationButton.Select();

            _shopPanel.DOAnchorPosY(_panelTopPosY, _tweenDuration);
            _toggleButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isDisplayed = true;
            ColorToggle(0);
            _planetRotator.enabled = false;
        }
    }

    public void ToggleLevelsPanel()
    {
        if (isLevelsDisplayed)
        {
            _camera.MoveCameraTo(_startCameraDistance);
            _camera.MoveCameraToInstant(_startCameraPos);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(1f));
            }

            _levelsPanel.DOAnchorPosX(_levelsPanelMiddlePosX, _tweenDuration);
            _menuButton.gameObject.SetActive(true);
            //_menuButton.DORotate(new Vector3(0, 0, 0), _tweenDuration);
            isLevelsDisplayed = false;

            if (!_objectPlaceRotator.isRotating)
            {
                _planetRotator.enabled = true;
            }
        }
        else
        {
            _startCameraDistance = _camera.FinalDistance;
            _startCameraPos = _camera.gameObject.transform.position;

            _camera.MoveCameraTo(_cameraDistance);
            _camera.MoveCameraToInstant(_cameraPos);

            if (_backgroundImage != null)
            {
                StartCoroutine(SmoothScaleBackground(0.93f));
            }

            _levelsPanel.DOAnchorPosX(_levelsPanelTopPosX, _tweenDuration);
            _menuButton.gameObject.SetActive(false);

            //_menuButton.DORotate(new Vector3(0, 0, 180), _tweenDuration);
            isLevelsDisplayed = true;
            _planetRotator.enabled = false;
        }
    }

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

    public void ShowLeaderboards()
    {
        var prefabs = CBSScriptable.Get<LeaderboardPrefabs>();
        var leaderboardsPrefab = prefabs.LeaderboardsWindow;
        UIView.ShowWindow(leaderboardsPrefab);
    }
}

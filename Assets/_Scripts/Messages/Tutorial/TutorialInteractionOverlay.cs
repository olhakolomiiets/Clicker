using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PlanetBuilder.Messages;

namespace PlanetBuilder.Messages.Tutorial
{
    public class TutorialInteractionOverlay : MonoBehaviour, IPointerClickHandler
    {
        private const int DefaultSortingOrder = 32760;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _overlayRoot;
        [SerializeField] private Image _raycastShield;
        [SerializeField] private Image _topBlocker;
        [SerializeField] private Image _bottomBlocker;
        [SerializeField] private Image _leftBlocker;
        [SerializeField] private Image _rightBlocker;
        [SerializeField, Range(0f, 1f)] private float _overlayAlpha = 0.75f;
        [SerializeField] private Vector2 _cutoutPadding = new(12f, 12f);
        [SerializeField] private int _sortingOrder = DefaultSortingOrder;

        private readonly Vector3[] _targetWorldCorners = new Vector3[4];

        private GameObject _targetUI;
        private RectTransform _targetRectTransform;
        private Canvas _overlayCanvas;
        private Canvas _targetCanvas;
        private GraphicRaycaster _graphicRaycaster;
        private bool _isVisible;
        private bool _cutoutEnabled = true;
        private bool _fullDimLogWritten;

        public bool IsBlockingRaycasts => _canvasGroup != null && _canvasGroup.blocksRaycasts;
        public GameObject CurrentTargetUI => _targetUI;

        private void Awake()
        {
            EnsureReferences();
            Hide();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }

        private void LateUpdate()
        {
            if (_isVisible)
                RefreshLayout();
        }

        public bool Show(GameObject targetUI)
        {
            return Show(targetUI, true);
        }

        public bool Show(GameObject targetUI, bool allowCutout)
        {
            Debug.Log(
                $"[MetaTutorialTrace] TutorialInteractionOverlay.Show ENTER target={GetTransformPath(targetUI != null ? targetUI.transform : null)} active={(targetUI != null && targetUI.activeInHierarchy)} allowCutout={allowCutout}",
                this);
            Hide();

            if (!EnsureReferences() || !ValidateInfrastructure())
            {
                Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay.Show STOP refs/infrastructure", this);
                return false;
            }

            if (targetUI != null && !TrySetTarget(targetUI))
            {
                Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay.Show STOP TrySetTarget", this);
                return false;
            }

            _canvasGroup.alpha = _overlayAlpha;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
            _cutoutEnabled = allowCutout;
            _fullDimLogWritten = false;
            _isVisible = true;

            RefreshLayout();
            Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay.Show OK", this);
            return true;
        }

        public void Hide()
        {
            MessageTutorialTrace.LogHide(
                "TutorialInteractionOverlay.Hide",
                gameObject,
                _targetUI != null
                    ? $"target={MessageTutorialTrace.GetTransformPath(_targetUI.transform)}"
                    : "target=null");

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            _targetUI = null;
            _targetRectTransform = null;
            _targetCanvas = null;
            _cutoutEnabled = true;
            _fullDimLogWritten = false;
            _isVisible = false;
        }

        public void ClearTarget()
        {
            Hide();
            Debug.Log("[MetaTutorialTrace] Step5 interaction overlay target cleared", this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isVisible || _targetUI == null || _targetRectTransform == null)
                return;

            GameObject activeTarget = _targetUI;
            RectTransform activeTargetRect = _targetRectTransform;
            Camera targetCamera = GetCanvasCamera(_targetCanvas);
            bool isInsideTarget = RectTransformUtility.RectangleContainsScreenPoint(
                activeTargetRect,
                eventData.position,
                targetCamera);

            Debug.Log(
                $"[MetaTutorialTrace] TutorialInteractionOverlay click pointer position={eventData.position} active target name={activeTarget.name} active target rect={GetScreenRectDescription(activeTargetRect, targetCamera)} is pointer inside target={isInsideTarget}",
                this);

            if (!isInsideTarget)
            {
                Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay click blocked outside target", this);
                return;
            }

            if (!_cutoutEnabled)
                Debug.Log("[MetaTutorialTrace] Step5 box click received through invisible proxy", this);

            Button button = FindTargetButton(activeTarget);
            Debug.Log(
                $"[MetaTutorialTrace] TutorialInteractionOverlay button {(button != null ? "found" : "not found")} target={GetTransformPath(activeTarget.transform)} button={GetTransformPath(button != null ? button.transform : null)}",
                this);

            if (button != null)
            {
                Debug.Log(
                    $"[MetaTutorialTrace] TutorialInteractionOverlay button interactable {button.interactable} active={button.gameObject.activeInHierarchy}",
                    this);

                if (button.interactable && button.gameObject.activeInHierarchy)
                {
                    button.onClick.Invoke();
                    eventData.Use();
                    Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay button.onClick invoked", this);
                    Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay TargetUIClicked sent via Button.onClick", this);
                    return;
                }

                Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay click blocked: button not interactable or inactive", this);
                return;
            }

            bool executed = ExecuteEvents.Execute(activeTarget, eventData, ExecuteEvents.pointerClickHandler);
            eventData.Use();
            Debug.Log(
                $"[MetaTutorialTrace] Step5 click pass-through result: target={GetTransformPath(activeTarget.transform)} executed={executed}",
                this);

            if (executed)
                Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay TargetUIClicked sent via ExecuteEvents", this);
        }

        private static Button FindTargetButton(GameObject targetUI)
        {
            if (targetUI == null)
                return null;

            Button button = targetUI.GetComponent<Button>();

            if (button != null)
                return button;

            button = targetUI.GetComponentInParent<Button>();

            if (button != null)
                return button;

            return targetUI.GetComponentInChildren<Button>(true);
        }

        private bool EnsureReferences()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_overlayRoot == null)
                _overlayRoot = transform as RectTransform;

            if (_overlayCanvas == null && _overlayRoot != null)
                _overlayCanvas = _overlayRoot.GetComponentInParent<Canvas>();

            if (_graphicRaycaster == null && _overlayCanvas != null)
                _graphicRaycaster = _overlayCanvas.GetComponent<GraphicRaycaster>();

            bool isConfigured = _canvasGroup != null &&
                                _overlayRoot != null &&
                                _raycastShield != null &&
                                _topBlocker != null &&
                                _bottomBlocker != null &&
                                _leftBlocker != null &&
                                _rightBlocker != null;

            if (!isConfigured)
            {
                Debug.LogWarning(
                    "TutorialInteractionOverlay requires CanvasGroup, overlay root, raycast shield and four blocker images.",
                    this);
                return false;
            }

            _raycastShield.raycastTarget = true;
            _topBlocker.raycastTarget = false;
            _bottomBlocker.raycastTarget = false;
            _leftBlocker.raycastTarget = false;
            _rightBlocker.raycastTarget = false;
            return true;
        }

        private bool ValidateInfrastructure()
        {
            if (_overlayCanvas == null)
            {
                Debug.LogWarning("TutorialInteractionOverlay requires a Canvas.", this);
                return false;
            }

            if (!_overlayCanvas.isActiveAndEnabled)
            {
                Debug.LogWarning("TutorialInteractionOverlay Canvas must be active and enabled.", this);
                return false;
            }

            if (_graphicRaycaster == null)
            {
                Debug.LogWarning("TutorialInteractionOverlay Canvas requires a GraphicRaycaster.", this);
                return false;
            }

            if (!_graphicRaycaster.isActiveAndEnabled)
            {
                Debug.LogWarning("TutorialInteractionOverlay GraphicRaycaster must be active and enabled.", this);
                return false;
            }

            if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled)
            {
                Debug.LogWarning("TutorialInteractionOverlay requires an active and enabled EventSystem.", this);
                return false;
            }

            if (!TryValidateCanvasCamera(_overlayCanvas, "overlay"))
                return false;

            _overlayCanvas.overrideSorting = true;
            _overlayCanvas.sortingOrder = _sortingOrder;
            return ValidateSortingOrder();
        }

        private bool TrySetTarget(GameObject targetUI)
        {
            _targetRectTransform = targetUI.GetComponent<RectTransform>();

            if (_targetRectTransform == null)
            {
                Debug.LogWarning("Tutorial TargetUI requires a RectTransform.", this);
                return false;
            }

            _targetCanvas = _targetRectTransform.GetComponentInParent<Canvas>();

            if (_targetCanvas == null)
            {
                Debug.LogWarning("Tutorial TargetUI requires a parent Canvas.", this);
                return false;
            }

            if (!_targetCanvas.isActiveAndEnabled)
            {
                Debug.LogWarning("Tutorial TargetUI Canvas must be active and enabled.", this);
                return false;
            }

            GraphicRaycaster targetRaycaster = _targetCanvas.GetComponent<GraphicRaycaster>();

            if (targetRaycaster == null)
            {
                Debug.LogWarning("Tutorial TargetUI Canvas requires a GraphicRaycaster.", this);
                return false;
            }

            if (!targetRaycaster.isActiveAndEnabled)
            {
                Debug.LogWarning("Tutorial TargetUI GraphicRaycaster must be active and enabled.", this);
                return false;
            }

            if (!TryValidateCanvasCamera(_targetCanvas, "TargetUI"))
                return false;

            _targetUI = targetUI;
            return true;
        }

        private bool ValidateSortingOrder()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            int overlayLayerValue = SortingLayer.GetLayerValueFromID(_overlayCanvas.sortingLayerID);

            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];

                if (canvas == null || canvas == _overlayCanvas || !canvas.isActiveAndEnabled)
                    continue;

                Canvas effectiveCanvas = canvas.overrideSorting ? canvas : canvas.rootCanvas;

                if (effectiveCanvas == null || effectiveCanvas == _overlayCanvas)
                    continue;

                int layerValue = SortingLayer.GetLayerValueFromID(effectiveCanvas.sortingLayerID);
                bool isHigherLayer = layerValue > overlayLayerValue;
                bool isSameOrHigherOrder = layerValue == overlayLayerValue &&
                                           effectiveCanvas.sortingOrder >= _overlayCanvas.sortingOrder;

                if (!isHigherLayer && !isSameOrHigherOrder)
                    continue;

                Debug.LogWarning(
                    $"Tutorial overlay sorting order must be above Canvas '{effectiveCanvas.name}'.",
                    effectiveCanvas);
            }

            return true;
        }

        private static bool TryValidateCanvasCamera(Canvas canvas, string canvasName)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.worldCamera != null)
                return true;

            Debug.LogWarning($"Tutorial {canvasName} Canvas requires a world camera.", canvas);
            return false;
        }

        private void RefreshLayout()
        {
            if (_overlayRoot == null)
                return;

            Rect overlayRect = _overlayRoot.rect;

            SetBlockerRect(_raycastShield.rectTransform, overlayRect);

            if (_targetRectTransform == null || !_cutoutEnabled)
            {
                SetFullDimRects(overlayRect);

                if (_targetRectTransform != null && !_fullDimLogWritten)
                {
                    Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay cutout disabled for target", this);
                    Debug.Log("[MetaTutorialTrace] TutorialInteractionOverlay full dim mode active", this);
                    _fullDimLogWritten = true;
                }

                return;
            }

            Rect cutoutRect = GetCutoutRect(overlayRect);

            SetBlockerRect(
                _topBlocker.rectTransform,
                Rect.MinMaxRect(overlayRect.xMin, cutoutRect.yMax, overlayRect.xMax, overlayRect.yMax));
            SetBlockerRect(
                _bottomBlocker.rectTransform,
                Rect.MinMaxRect(overlayRect.xMin, overlayRect.yMin, overlayRect.xMax, cutoutRect.yMin));
            SetBlockerRect(
                _leftBlocker.rectTransform,
                Rect.MinMaxRect(overlayRect.xMin, cutoutRect.yMin, cutoutRect.xMin, cutoutRect.yMax));
            SetBlockerRect(
                _rightBlocker.rectTransform,
                Rect.MinMaxRect(cutoutRect.xMax, cutoutRect.yMin, overlayRect.xMax, cutoutRect.yMax));
        }

        private void SetFullDimRects(Rect overlayRect)
        {
            SetBlockerRect(_topBlocker.rectTransform, overlayRect);
            SetBlockerRect(_bottomBlocker.rectTransform, Rect.zero);
            SetBlockerRect(_leftBlocker.rectTransform, Rect.zero);
            SetBlockerRect(_rightBlocker.rectTransform, Rect.zero);
        }

        private Rect GetCutoutRect(Rect overlayRect)
        {
            _targetRectTransform.GetWorldCorners(_targetWorldCorners);

            Camera targetCamera = GetCanvasCamera(_targetCanvas);
            Camera overlayCamera = GetCanvasCamera(_overlayCanvas);
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            int convertedPointCount = 0;

            for (int i = 0; i < _targetWorldCorners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, _targetWorldCorners[i]);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _overlayRoot,
                        screenPoint,
                        overlayCamera,
                        out Vector2 localPoint))
                {
                    continue;
                }

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
                convertedPointCount++;
            }

            if (convertedPointCount == 0)
                return Rect.MinMaxRect(overlayRect.xMin, overlayRect.yMin, overlayRect.xMin, overlayRect.yMin);

            float paddingX = Mathf.Max(0f, _cutoutPadding.x);
            float paddingY = Mathf.Max(0f, _cutoutPadding.y);

            return Rect.MinMaxRect(
                Mathf.Clamp(min.x - paddingX, overlayRect.xMin, overlayRect.xMax),
                Mathf.Clamp(min.y - paddingY, overlayRect.yMin, overlayRect.yMax),
                Mathf.Clamp(max.x + paddingX, overlayRect.xMin, overlayRect.xMax),
                Mathf.Clamp(max.y + paddingY, overlayRect.yMin, overlayRect.yMax));
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        private static void SetBlockerRect(RectTransform blocker, Rect rect)
        {
            blocker.anchorMin = new Vector2(0.5f, 0.5f);
            blocker.anchorMax = new Vector2(0.5f, 0.5f);
            blocker.pivot = new Vector2(0.5f, 0.5f);
            blocker.anchoredPosition = rect.center;
            blocker.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
        }

        private static string GetScreenRectDescription(RectTransform rectTransform, Camera camera)
        {
            if (rectTransform == null)
                return "null";

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);

            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            return $"min={min} max={max} size={max - min}";
        }

        private static string GetTransformPath(Transform target)
        {
            if (target == null)
                return "null";

            string path = target.name;
            Transform current = target.parent;

            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}

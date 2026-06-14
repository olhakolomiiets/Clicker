using UnityEngine;
using UnityEngine.UI;
using PlanetBuilder.Messages;

namespace PlanetBuilder.Messages.Tutorial
{
    public class TutorialHighlightSystem : MonoBehaviour
    {
        [SerializeField] private RectTransform _overlayRoot;
        [SerializeField] private Image _highlightImage;
        [SerializeField] private RectTransform _arrowRoot;
        [SerializeField] private Image[] _arrowImages;

        private readonly Vector3[] _targetWorldCorners = new Vector3[4];

        private TutorialStepData _step;
        private RectTransform _targetRectTransform;
        private Canvas _overlayCanvas;
        private Canvas _targetCanvas;
        private bool _isVisible;
        private float _pulseStartedAt;
        private bool _hasLoggedCurrentStep;

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
            if (!_isVisible)
                return;

            RefreshLayout();
            RefreshPulse();
        }

        public bool Show(GameObject targetUI, TutorialStepData step)
        {
            Debug.Log(
                $"[MetaTutorialTrace] TutorialHighlightSystem.Show ENTER step={step?.StepId ?? "null"} target={GetTransformPath(targetUI != null ? targetUI.transform : null)} active={(targetUI != null && targetUI.activeInHierarchy)}",
                this);
            Hide();

            if (step == null)
            {
                Debug.LogWarning("TutorialHighlightSystem requires TutorialStepData.", this);
                Debug.Log("[MetaTutorialTrace] TutorialHighlightSystem.Show STOP null step", this);
                return false;
            }

            if (!step.EnableHighlight && !step.ShowArrow)
            {
                Debug.Log("[MetaTutorialTrace] TutorialHighlightSystem.Show OK no highlight/arrow requested", this);
                return true;
            }

            if (!EnsureReferences())
            {
                Debug.Log("[MetaTutorialTrace] TutorialHighlightSystem.Show STOP EnsureReferences", this);
                return false;
            }

            if (targetUI == null)
            {
                Debug.LogWarning($"Tutorial step '{step.StepId}' requires TargetUI for its highlight.", this);
                Debug.Log("[MetaTutorialTrace] TutorialHighlightSystem.Show STOP null target", this);
                return false;
            }

            if (!targetUI.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"[MetaTutorialTrace] TutorialHighlightSystem.Show STOP inactive target step={step.StepId} target={GetTransformPath(targetUI.transform)} activeSelf={targetUI.activeSelf} activeInHierarchy={targetUI.activeInHierarchy}",
                    this);
                return false;
            }

            _targetRectTransform = targetUI.GetComponent<RectTransform>();
            _targetCanvas = _targetRectTransform != null
                ? _targetRectTransform.GetComponentInParent<Canvas>()
                : null;

            if (_targetRectTransform == null || _targetCanvas == null)
            {
                Debug.LogWarning($"Tutorial step '{step.StepId}' highlight requires TargetUI inside a Canvas.", this);
                Debug.Log("[MetaTutorialTrace] TutorialHighlightSystem.Show STOP rect/canvas", this);
                return false;
            }

            if (!TryValidateCanvasCamera(_overlayCanvas, "overlay") ||
                !TryValidateCanvasCamera(_targetCanvas, "TargetUI"))
            {
                Debug.Log("[MetaTutorialTrace] TutorialHighlightSystem.Show STOP canvas camera", this);
                return false;
            }

            _step = step;
            _pulseStartedAt = Time.unscaledTime;
            _hasLoggedCurrentStep = false;
            _highlightImage.gameObject.SetActive(step.EnableHighlight);
            _highlightImage.color = step.HighlightColor;
            _arrowRoot.gameObject.SetActive(step.ShowArrow);
            SetArrowColor(step.ArrowColor);
            BringHighlightToFront();
            _isVisible = true;
            RefreshLayout();
            RefreshPulse();
            Debug.Log($"[MetaTutorialTrace] TutorialHighlightSystem.Show OK step={step.StepId}", this);
            return true;
        }

        public void Hide()
        {
            if (_highlightImage != null)
            {
                MessageTutorialTrace.LogHide(
                    "TutorialHighlightSystem.Hide.HighlightImage",
                    _highlightImage.gameObject,
                    _step != null
                        ? $"step={_step.StepId} target={MessageTutorialTrace.GetTransformPath(_targetRectTransform)}"
                        : "step=null");
                _highlightImage.gameObject.SetActive(false);
                _highlightImage.rectTransform.localScale = Vector3.one;
            }

            if (_arrowRoot != null)
            {
                MessageTutorialTrace.LogHide(
                    "TutorialHighlightSystem.Hide.ArrowRoot",
                    _arrowRoot.gameObject,
                    _step != null
                        ? $"step={_step.StepId} target={MessageTutorialTrace.GetTransformPath(_targetRectTransform)}"
                        : "step=null");
                _arrowRoot.gameObject.SetActive(false);
            }

            _step = null;
            _targetRectTransform = null;
            _targetCanvas = null;
            _isVisible = false;
            _hasLoggedCurrentStep = false;
        }

        private bool EnsureReferences()
        {
            if (_overlayRoot == null)
                _overlayRoot = transform as RectTransform;

            if (_overlayCanvas == null && _overlayRoot != null)
                _overlayCanvas = _overlayRoot.GetComponentInParent<Canvas>();

            bool isConfigured = _overlayRoot != null &&
                                _overlayCanvas != null &&
                                _highlightImage != null &&
                                _arrowRoot != null &&
                                _arrowImages != null &&
                                _arrowImages.Length > 0;

            if (!isConfigured)
            {
                Debug.LogWarning(
                    "TutorialHighlightSystem requires overlay root, highlight image, arrow root and arrow images.",
                    this);
                return false;
            }

            _highlightImage.raycastTarget = false;

            for (int i = 0; i < _arrowImages.Length; i++)
            {
                if (_arrowImages[i] != null)
                    _arrowImages[i].raycastTarget = false;
            }

            return true;
        }

        private void RefreshLayout()
        {
            if (_step == null || _targetRectTransform == null || _overlayRoot == null)
                return;

            Rect overlayRect = _overlayRoot.rect;
            Rect targetRect = GetTargetRect(overlayRect);
            float paddingX = Mathf.Max(0f, _step.HighlightPadding.x);
            float paddingY = Mathf.Max(0f, _step.HighlightPadding.y);
            Rect highlightRect = Rect.MinMaxRect(
                Mathf.Clamp(targetRect.xMin - paddingX, overlayRect.xMin, overlayRect.xMax),
                Mathf.Clamp(targetRect.yMin - paddingY, overlayRect.yMin, overlayRect.yMax),
                Mathf.Clamp(targetRect.xMax + paddingX, overlayRect.xMin, overlayRect.xMax),
                Mathf.Clamp(targetRect.yMax + paddingY, overlayRect.yMin, overlayRect.yMax));

            SetRect(_highlightImage.rectTransform, highlightRect);

            if (_step.ShowArrow)
                PositionArrow(overlayRect, highlightRect);

            LogAddVariantObjectHighlight(targetRect, highlightRect);
        }

        private void RefreshPulse()
        {
            if (_step == null || !_step.EnableHighlight || !_step.EnablePulse)
            {
                if (_highlightImage != null)
                    _highlightImage.rectTransform.localScale = Vector3.one;

                return;
            }

            float speed = Mathf.Max(0f, _step.PulseSpeed);
            float amplitude = Mathf.Max(0f, _step.PulseScale);
            float pulse = (Mathf.Sin((Time.unscaledTime - _pulseStartedAt) * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale = 1f + pulse * amplitude;
            _highlightImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private Rect GetTargetRect(Rect overlayRect)
        {
            _targetRectTransform.GetWorldCorners(_targetWorldCorners);

            Camera targetCamera = GetCanvasCamera(_targetCanvas);
            Camera overlayCamera = GetCanvasCamera(_overlayCanvas);
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);

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
            }

            if (min.x == float.MaxValue)
                return Rect.zero;

            return Rect.MinMaxRect(
                Mathf.Clamp(min.x, overlayRect.xMin, overlayRect.xMax),
                Mathf.Clamp(min.y, overlayRect.yMin, overlayRect.yMax),
                Mathf.Clamp(max.x, overlayRect.xMin, overlayRect.xMax),
                Mathf.Clamp(max.y, overlayRect.yMin, overlayRect.yMax));
        }

        private void PositionArrow(Rect overlayRect, Rect targetRect)
        {
            float leftSpace = targetRect.xMin - overlayRect.xMin;
            float rightSpace = overlayRect.xMax - targetRect.xMax;
            float bottomSpace = targetRect.yMin - overlayRect.yMin;
            float topSpace = overlayRect.yMax - targetRect.yMax;
            float distance = Mathf.Max(0f, _step.ArrowDistance);
            Vector2 position;
            float rotation;

            if (rightSpace >= leftSpace && rightSpace >= topSpace && rightSpace >= bottomSpace)
            {
                position = new Vector2(targetRect.xMax + distance, targetRect.center.y);
                rotation = 180f;
            }
            else if (leftSpace >= topSpace && leftSpace >= bottomSpace)
            {
                position = new Vector2(targetRect.xMin - distance, targetRect.center.y);
                rotation = 0f;
            }
            else if (topSpace >= bottomSpace)
            {
                position = new Vector2(targetRect.center.x, targetRect.yMax + distance);
                rotation = -90f;
            }
            else
            {
                position = new Vector2(targetRect.center.x, targetRect.yMin - distance);
                rotation = 90f;
            }

            float margin = Mathf.Max(_arrowRoot.rect.width, _arrowRoot.rect.height) * 0.5f;
            position.x = Mathf.Clamp(position.x, overlayRect.xMin + margin, overlayRect.xMax - margin);
            position.y = Mathf.Clamp(position.y, overlayRect.yMin + margin, overlayRect.yMax - margin);
            _arrowRoot.anchoredPosition = position;
            _arrowRoot.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void SetArrowColor(Color color)
        {
            for (int i = 0; i < _arrowImages.Length; i++)
            {
                if (_arrowImages[i] != null)
                    _arrowImages[i].color = color;
            }
        }

        private void BringHighlightToFront()
        {
            if (_highlightImage != null)
                _highlightImage.rectTransform.SetAsLastSibling();

            if (_arrowRoot != null)
                _arrowRoot.SetAsLastSibling();
        }

        private void LogAddVariantObjectHighlight(Rect targetRect, Rect highlightRect)
        {
            if (_hasLoggedCurrentStep ||
                _step == null ||
                _step.StepId != "MetaPlanet.AddVariantObject")
            {
                return;
            }

            _hasLoggedCurrentStep = true;
            Debug.Log(
                "MetaPlanet.AddVariantObject\n" +
                $"Target: {GetTransformPath(_targetRectTransform)}\n" +
                $"Rect: {targetRect}\n" +
                $"Size: {highlightRect.size}\n" +
                $"Canvas: {(_overlayCanvas != null ? _overlayCanvas.name : "null")} sortingOrder={(_overlayCanvas != null ? _overlayCanvas.sortingOrder : 0)}\n" +
                $"Highlight Active: {(_highlightImage != null && _highlightImage.gameObject.activeInHierarchy)}\n" +
                $"Arrow Active: {(_arrowRoot != null && _arrowRoot.gameObject.activeInHierarchy)}",
                this);
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

        private static bool TryValidateCanvasCamera(Canvas canvas, string canvasName)
        {
            if (canvas != null &&
                (canvas.renderMode == RenderMode.ScreenSpaceOverlay || canvas.worldCamera != null))
            {
                return true;
            }

            Debug.LogWarning($"Tutorial highlight {canvasName} Canvas requires a world camera.", canvas);
            return false;
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }

        private static void SetRect(RectTransform rectTransform, Rect rect)
        {
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = rect.center;
            rectTransform.sizeDelta = new Vector2(Mathf.Max(0f, rect.width), Mathf.Max(0f, rect.height));
        }
    }
}

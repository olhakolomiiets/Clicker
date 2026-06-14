using UnityEngine;
using PlanetBuilder.Messages;

namespace PlanetBuilder.Messages.Tutorial
{
    public class WorldTutorialTargetProxy : MonoBehaviour
    {
        [SerializeField] private RectTransform _proxyRect;
        [SerializeField] private Transform _worldTarget;
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private Vector2 _minimumSize = new(48f, 48f);

        private readonly Vector3[] _worldCorners = new Vector3[8];
        private RectTransform _parentRect;
        private Canvas _canvas;
        private TutorialTargetClickRelay _clickRelay;
        private bool _trackingLogWritten;

        public RectTransform ProxyRect => _proxyRect;
        public GameObject ProxyGameObject => _proxyRect != null ? _proxyRect.gameObject : null;

        private void Awake()
        {
            EnsureReferences();
            ClearTarget();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public bool SetTarget(Transform worldTarget, Camera worldCamera = null)
        {
            EnsureReferences();
            _worldTarget = worldTarget;
            _worldCamera = worldCamera != null ? worldCamera : Camera.main;
            _trackingLogWritten = false;

            if (!Refresh())
            {
                ClearTarget();
                return false;
            }

            return true;
        }

        public void ClearTarget()
        {
            _worldTarget = null;
            _trackingLogWritten = false;

            if (_proxyRect != null)
            {
                MessageTutorialTrace.LogHide(
                    "WorldTutorialTargetProxy.ClearTarget",
                    _proxyRect.gameObject,
                    "clear target");
                _proxyRect.gameObject.SetActive(false);
            }
        }

        private bool Refresh()
        {
            if (!EnsureReferences() ||
                _worldTarget == null ||
                !_worldTarget.gameObject.activeInHierarchy)
            {
                if (_proxyRect != null)
                {
                    MessageTutorialTrace.LogHide(
                        "WorldTutorialTargetProxy.Refresh.InvalidTarget",
                        _proxyRect.gameObject,
                        $"worldTarget={MessageTutorialTrace.GetTransformPath(_worldTarget)}");
                    _proxyRect.gameObject.SetActive(false);
                }

                return false;
            }

            if (_worldCamera == null)
                _worldCamera = Camera.main;

            if (_worldCamera == null)
            {
                MessageTutorialTrace.LogHide(
                    "WorldTutorialTargetProxy.Refresh.NoCamera",
                    _proxyRect.gameObject,
                    "world camera missing");
                _proxyRect.gameObject.SetActive(false);
                return false;
            }

            if (!TryGetWorldBounds(_worldTarget, out Bounds bounds) ||
                !TryProjectBounds(bounds, out Rect rect))
            {
                MessageTutorialTrace.LogHide(
                    "WorldTutorialTargetProxy.Refresh.ProjectFailed",
                    _proxyRect.gameObject,
                    $"worldTarget={MessageTutorialTrace.GetTransformPath(_worldTarget)}");
                _proxyRect.gameObject.SetActive(false);
                return false;
            }

            _proxyRect.gameObject.SetActive(true);
            ApplyRect(rect);

            if (!_trackingLogWritten)
            {
                Debug.Log("[MetaTutorialTrace] Step5 proxy LateUpdate tracking active", this);
                _trackingLogWritten = true;
            }

            return true;
        }

        private bool EnsureReferences()
        {
            if (_proxyRect == null)
                _proxyRect = transform as RectTransform;

            if (_proxyRect == null)
                return false;

            if (_parentRect == null)
                _parentRect = _proxyRect.parent as RectTransform;

            if (_canvas == null)
                _canvas = _proxyRect.GetComponentInParent<Canvas>();

            if (_clickRelay == null)
            {
                _clickRelay = _proxyRect.GetComponent<TutorialTargetClickRelay>();

                if (_clickRelay == null)
                    _clickRelay = _proxyRect.gameObject.AddComponent<TutorialTargetClickRelay>();
            }

            return _parentRect != null && _canvas != null;
        }

        private static bool TryGetWorldBounds(Transform target, out Bounds bounds)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(false);
            bool hasBounds = false;
            bounds = default;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];

                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
                return true;

            Collider[] colliders = target.GetComponentsInChildren<Collider>(false);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider == null || !collider.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (hasBounds)
                return true;

            bounds = new Bounds(target.position, Vector3.one);
            return true;
        }

        private bool TryProjectBounds(Bounds bounds, out Rect rect)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            _worldCorners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            _worldCorners[1] = center + new Vector3(-extents.x, -extents.y, extents.z);
            _worldCorners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
            _worldCorners[3] = center + new Vector3(-extents.x, extents.y, extents.z);
            _worldCorners[4] = center + new Vector3(extents.x, -extents.y, -extents.z);
            _worldCorners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
            _worldCorners[6] = center + new Vector3(extents.x, extents.y, -extents.z);
            _worldCorners[7] = center + new Vector3(extents.x, extents.y, extents.z);

            Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            int projectedCount = 0;

            for (int i = 0; i < _worldCorners.Length; i++)
            {
                Vector3 screenPoint = _worldCamera.WorldToScreenPoint(_worldCorners[i]);

                if (screenPoint.z < 0f)
                    continue;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _parentRect,
                        screenPoint,
                        canvasCamera,
                        out Vector2 localPoint))
                {
                    continue;
                }

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
                projectedCount++;
            }

            if (projectedCount == 0)
            {
                rect = Rect.zero;
                return false;
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private void ApplyRect(Rect rect)
        {
            Vector2 size = rect.size;
            size.x = Mathf.Max(size.x, _minimumSize.x);
            size.y = Mathf.Max(size.y, _minimumSize.y);

            _proxyRect.anchorMin = new Vector2(0.5f, 0.5f);
            _proxyRect.anchorMax = new Vector2(0.5f, 0.5f);
            _proxyRect.pivot = new Vector2(0.5f, 0.5f);
            _proxyRect.anchoredPosition = rect.center;
            _proxyRect.sizeDelta = size;
        }
    }
}

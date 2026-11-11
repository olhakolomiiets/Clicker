using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlanetBuilder.Loading
{
    /// <summary>
    /// Persistent, time-driven planet evolution loading screen.
    /// The first complete evolution cycle always plays before the screen can close.
    /// The caller decides when loading is actually complete; no fake progress values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PlanetLoadingScreen : MonoBehaviour
    {
        [Header("Required UI references")]
        [SerializeField] private Canvas overlayCanvas;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GraphicRaycaster raycaster;
        [SerializeField] private Image planetImage;

        [Header("Six stages, in display order")]
        [SerializeField] private Sprite[] planetStages = new Sprite[6];
        [SerializeField] private Image[] stageMarkers = new Image[6];

        [Header("Timing (unscaled seconds)")]
        [Min(0.05f)] [SerializeField] private float stageInterval = 0.55f;
        [Min(0f)] [SerializeField] private float finalStageHold = 0.80f;
        [Min(0f)] [SerializeField] private float fadeOutSeconds = 0.15f;

        [Header("Stage indicator")]
        [SerializeField] private Color activeMarkerColor = new Color(0.30f, 0.91f, 1f, 1f);
        [SerializeField] private Color inactiveMarkerColor = new Color(0.40f, 0.54f, 0.70f, 0.45f);

        public static PlanetLoadingScreen Instance { get; private set; }
        public bool IsShowing => showing;
        public bool HasCompletedFirstCycle => completedFirstCycle;
        public bool IsWaitingForScene => showing && !loadingComplete;
        public float FullCycleDuration =>
            Mathf.Max(0, (planetStages == null ? 0 : planetStages.Length - 1))
            * Mathf.Max(0.05f, stageInterval) + Mathf.Max(0, finalStageHold);

        private bool showing;
        private bool hiding;
        private bool loadingComplete;
        private bool completedFirstCycle;
        private bool sceneTransitionRunning;
        private float cycleElapsed;
        private int displayedStage = -1;
        private Coroutine fadeRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null)
            {
                Debug.LogWarning("[PlanetLoadingScreen] Prefab should be a scene root. Detaching for DontDestroyOnLoad.", this);
                transform.SetParent(null, false);
            }

            DontDestroyOnLoad(gameObject);
            if (overlayCanvas == null) overlayCanvas = GetComponent<Canvas>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (raycaster == null) raycaster = GetComponent<GraphicRaycaster>();
            HideImmediately();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            stageInterval = Mathf.Max(0.05f, stageInterval);
            finalStageHold = Mathf.Max(0f, finalStageHold);
            fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
        }

        /// <summary>Show instantly and restart the six-stage evolution.</summary>
        public void Show()
        {
            if (Instance != this)
            {
                Debug.LogWarning("[PlanetLoadingScreen] Duplicate instance cannot show the loading screen.", this);
                return;
            }

            if (planetStages == null || planetStages.Length != 6 || planetImage == null)
            {
                Debug.LogError("[PlanetLoadingScreen] Assign exactly 6 Sprite stages and a Planet Image.", this);
                return;
            }

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            cycleElapsed = 0f;
            completedFirstCycle = false;
            loadingComplete = false;
            hiding = false;
            showing = true;
            displayedStage = -1;

            if (overlayCanvas != null) overlayCanvas.enabled = true;
            if (raycaster != null) raycaster.enabled = true;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = false;
            }

            DisplayStage(0);
        }

        /// <summary>
        /// Call only after the destination scene AND your own required setup are ready.
        /// If the first evolution has not finished, it will finish before closing.
        /// If it has already finished (e.g. loading is long), close immediately.
        /// </summary>
        public void NotifyLoadCompleted()
        {
            if (!showing || loadingComplete) return;
            loadingComplete = true;
            if (completedFirstCycle) StartHide();
        }

        /// <summary>Explicit cancellation; intentionally bypasses the minimum cycle.</summary>
        public void CancelAndHide()
        {
            HideImmediately();
        }

        public IEnumerator WaitUntilHidden()
        {
            while (showing) yield return null;
        }

        private void Update()
        {
            if (!showing || hiding) return;

            cycleElapsed += Time.unscaledDeltaTime;
            float interval = Mathf.Max(0.05f, stageInterval);
            float cycleDuration = FullCycleDuration;

            if (cycleDuration <= 0f)
            {
                completedFirstCycle = true;
                if (loadingComplete) StartHide();
                return;
            }

            // Keep the final stage visible for finalStageHold seconds.
            if (cycleElapsed >= cycleDuration)
            {
                completedFirstCycle = true;
                if (loadingComplete)
                {
                    StartHide();
                    return;
                }

                cycleElapsed %= cycleDuration;
                displayedStage = -1; // loop back to stage 1 if loading continues
            }

            int nextStage = Mathf.Clamp(Mathf.FloorToInt(cycleElapsed / interval), 0, planetStages.Length - 1);
            DisplayStage(nextStage);
        }

        private void DisplayStage(int index)
        {
            if (index == displayedStage) return;
            displayedStage = index;

            Sprite sprite = planetStages[index];
            if (sprite == null)
                Debug.LogWarning($"[PlanetLoadingScreen] Missing stage Sprite #{index + 1}.", this);
            planetImage.sprite = sprite;
            planetImage.preserveAspect = true;

            if (stageMarkers == null) return;
            for (int i = 0; i < stageMarkers.Length; i++)
            {
                if (stageMarkers[i] != null)
                    stageMarkers[i].color = i <= index ? activeMarkerColor : inactiveMarkerColor;
            }
        }

        private void StartHide()
        {
            if (!showing || hiding) return;
            hiding = true;
            if (fadeOutSeconds <= 0f || canvasGroup == null)
                HideImmediately();
            else
                fadeRoutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            float elapsed = 0f;
            float startingAlpha = canvasGroup.alpha;
            while (elapsed < fadeOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startingAlpha, 0f, Mathf.Clamp01(elapsed / fadeOutSeconds));
                yield return null;
            }

            fadeRoutine = null;
            HideImmediately();
        }

        private void HideImmediately()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            showing = false;
            hiding = false;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            if (raycaster != null) raycaster.enabled = false;
            if (overlayCanvas != null) overlayCanvas.enabled = false;
        }

        /// <summary>
        /// Convenience wrapper for a regular scene in the Scene List / Build Profiles.
        /// This coroutine lives on the persistent loading screen, not on the old scene.
        /// </summary>
        public void LoadBuiltInScene(string sceneName)
        {
            LoadBuiltInScene(sceneName, waitForFirstCycle: true);
        }

        public void LoadBuiltInScene(string sceneName, bool waitForFirstCycle)
        {
            if (sceneTransitionRunning)
            {
                Debug.LogWarning("[PlanetLoadingScreen] A scene transition is already in progress.", this);
                return;
            }
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("[PlanetLoadingScreen] Scene name is empty.", this);
                return;
            }
            StartCoroutine(LoadBuiltInSceneRoutine(sceneName, waitForFirstCycle));
        }

        private IEnumerator LoadBuiltInSceneRoutine(string sceneName, bool waitForFirstCycle)
        {
            sceneTransitionRunning = true;
            Show();
            if (!showing)
            {
                sceneTransitionRunning = false;
                yield break;
            }

            // Render the loading overlay at least once before scene activation work.
            yield return null;

            AsyncOperation loadOperation = null;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }

            if (loadOperation == null)
            {
                Debug.LogError($"[PlanetLoadingScreen] Cannot load scene '{sceneName}'. Check Build Profiles / Scene List.", this);
                CancelAndHide();
                sceneTransitionRunning = false;
                yield break;
            }

            // Leave scene activation enabled, so async queues aren't blocked at 0.9.
            while (!loadOperation.isDone) yield return null;
            yield return null; // let the freshly activated scene initialize one frame

            if (waitForFirstCycle)
            {
                NotifyLoadCompleted();
                yield return WaitUntilHidden();
            }
            else
            {
                HideImmediately();
            }

            sceneTransitionRunning = false;
        }
    }
}

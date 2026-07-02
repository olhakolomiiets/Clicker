using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Lean.Localization;
using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.Dialogs;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace PlanetBuilder.Messages.Tutorial
{
    public class MetaPlanetTutorialController : MonoBehaviour
    {
        private const string MetaTutorialCompletedKey = "MetaPlanetTutorialCompleted";
        private const string TutorialStageKey = "MetaPlanetTutorialStage";
        private const string TutorialProgressKey = "MetaPlanetTutorialCurrentStepId";
        private const string TutorialActionsCompletedKey = "MetaPlanetTutorialActionsCompleted";
        private const string StartingDiamondsGrantedKey = "MetaPlanetTutorialStartingDiamondsGranted";
        private const string TutorialVideoWatchedKey = "MetaPlanetTutorialVideoWatched";

        private const int StageFirstVisit = 0;
        private const int StageStartingBudget = 1;
        private const int StageOpenVariantTab = 2;
        private const int StageAddVariantObject = 3;
        private const int StageBuyVariantObject = 4;
        private const int StageOpenUpgradeTab = 5;
        private const int StageAddUpgradeObject = 6;
        private const int StageUpgradeObject = 7;
        private const int StageFirstOrder = 8;
        private const int StageTutorialVideo = 9;
        private const int StageExplainGoal = 10;
        private const int StageOpenFirstPlanet = 11;
        private const float Step5BoxTargetTimeout = 2f;
        private const float Step5ShopCloseSettleSeconds = 0.45f;
        private const float Step6ShopOpenSettleSeconds = 0.45f;
        private const string Step5OpenSourceBoxClick = "box click";
        private const string Step5OpenSourceAlreadyOpen = "already open";
        private const string Step3OpenVariantTabStepId = "MetaPlanet.OpenVariantTab";
        private const string Step4AddVariantObjectStepId = "MetaPlanet.AddVariantObject";
        private const string Step6ToggleShopStepId = "MetaPlanet.OpenUpgradeTab.ToggleShop";
        private const string Step6OpenUpgradeTabStepId = "MetaPlanet.OpenUpgradeTab";
        private const string Step3ToggleGlowName = "RuntimeStep3ToggleGlow";
        private const string Step4AddVariantGlowName = "RuntimeStep4AddVariantGlow";
        private const string Step5VariantBuyButtonGlowName = "RuntimeStep5VariantBuyButtonGlow";
        private const string Step6ToggleGlowName = "RuntimeStep6ToggleGlow";
        private const string Step6UpgradeTabGlowName = "RuntimeStep6UpgradeTabGlow";
        private const string Step7UpgradeObjectGlowName = "RuntimeStep7UpgradeObjectGlow";
        private const string Step8UpgradeButtonGlowName = "RuntimeStep8UpgradeButtonGlow";
        private const string Step5BoxContourGlowName = "RuntimeStep5BoxContourGlow";

        [Header("Systems")]
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private DialogManager _dialogManager;
        [SerializeField] private TutorialInteractionOverlay _interactionOverlay;
        [SerializeField] private TutorialHighlightSystem _tutorialHighlightSystem;
        [SerializeField] private MetaGameRules _metaGameRules;
        [SerializeField] private GameManager _gameManager;
        [SerializeField] private MetaUIController _metaUIController;
        [SerializeField] private MetaPlanetManager _metaPlanetManager;

        [Header("Meta Planet Targets")]
        [SerializeField] private MetaVariantsController _houseController;
        [SerializeField] private MetaUpgradeItemController _houseUpgradeController;
        [SerializeField] private UpgradePanelUI _upgradePanel;

        [Header("Tutorial UI")]
        [SerializeField] private MetaTutorialModalView _rewardView;
        [SerializeField] private MetaTutorialModalView _contractView;
        [SerializeField] private MetaTutorialVideoView _videoView;
        [SerializeField] private GameObject _diamondBalanceTarget;
        [SerializeField] private WorldTutorialTargetProxy _worldTargetProxy;
        [SerializeField] private Camera _worldTargetCamera;

        [Header("Characters")]
        [SerializeField] private string _contractorSpeakerId = "1";
        [SerializeField] private string _robotSpeakerId = "2";

        [Header("Rewards")]
        [Min(0f)]
        [SerializeField] private double _startingDiamonds = 10d;
        [Min(0f)]
        [SerializeField] private double _finalDiamondsReward;

        [Header("Scene Transition")]
        [SerializeField] private string _firstPlanetSceneName = "PlanetLevel1";
        [SerializeField] private int _firstPlanetSceneIndex = 0;

        [Header("Typing")]
        [Min(0f)]
        [SerializeField] private float _typingSpeed = 30f;

        private readonly Dictionary<string, Action> _dialogCallbacks = new();
        private readonly HashSet<string> _nonBlockingDialogIds = new();
        private Coroutine _initializationCoroutine;
        private Coroutine _dialogRetryCoroutine;
        private Coroutine _stepCompletedCoroutine;
        private Coroutine _step5BoxTargetCoroutine;
        private Coroutine _step5VariantPurchaseCoroutine;
        private Coroutine _step6ShopOpenCoroutine;
        private Coroutine _deferredStageAdvanceCoroutine;
        private Coroutine _step7AddUpgradeObjectCoroutine;
        private Sequence _step3TogglePulseSequence;
        private Sequence _step3ToggleGlowSequence;
        private Sequence _step4AddVariantPulseSequence;
        private Sequence _step4AddVariantGlowSequence;
        private Sequence _step5VariantBuyButtonPulseSequence;
        private Sequence _step5VariantBuyButtonGlowSequence;
        private Sequence _step6TogglePulseSequence;
        private Sequence _step6ToggleGlowSequence;
        private Sequence _step6UpgradeTabPulseSequence;
        private Sequence _step6UpgradeTabGlowSequence;
        private Sequence _step7UpgradeObjectPulseSequence;
        private Sequence _step7UpgradeObjectGlowSequence;
        private Sequence _step8UpgradeButtonPulseSequence;
        private Sequence _step8UpgradeButtonGlowSequence;
        private Sequence _step5BoxContourGlowSequence;
        private Action _tutorialStepCompleted;
        private RectTransform _step3ToggleTarget;
        private RectTransform _step4AddVariantTarget;
        private RectTransform _step5VariantBuyButtonTarget;
        private RectTransform _step6ToggleTarget;
        private RectTransform _step6UpgradeTabTarget;
        private RectTransform _step7UpgradeObjectTarget;
        private RectTransform _step8UpgradeButtonTarget;
        private GameObject _step3ToggleGlow;
        private GameObject _step4AddVariantGlow;
        private GameObject _step5VariantBuyButtonGlow;
        private GameObject _step6ToggleGlow;
        private GameObject _step6UpgradeTabGlow;
        private GameObject _step7UpgradeObjectGlow;
        private GameObject _step8UpgradeButtonGlow;
        private GameObject _step5BoxContourGlow;
        private readonly List<Material> _step5BoxContourGlowMaterials = new();
        private readonly List<float> _step5BoxContourGlowBaseAlphas = new();
        private readonly Vector3[] _step3ToggleWorldCorners = new Vector3[4];
        private readonly Vector3[] _step4AddVariantWorldCorners = new Vector3[4];
        private readonly Vector3[] _step5VariantBuyButtonWorldCorners = new Vector3[4];
        private readonly Vector3[] _step6UpgradeTabWorldCorners = new Vector3[4];
        private readonly Vector3[] _step7UpgradeObjectWorldCorners = new Vector3[4];
        private readonly Vector3[] _step8UpgradeButtonWorldCorners = new Vector3[4];
        private Vector3 _step3ToggleOriginalScale;
        private Vector3 _step4AddVariantOriginalScale;
        private Vector3 _step5VariantBuyButtonOriginalScale;
        private Vector3 _step6ToggleOriginalScale;
        private Vector3 _step6UpgradeTabOriginalScale;
        private Vector3 _step7UpgradeObjectOriginalScale;
        private Vector3 _step8UpgradeButtonOriginalScale;
        private bool _isSubscribed;
        private bool _isAdvancing;
        private bool _step5ShopCloseRequested;
        private bool _step5IntroDialogStarted;
        private MetaVariantItemController _targetHouseItem;
        private int _targetHouseVariantIndex = -1;
        private GameObject _step5BoxProxyTarget;

        public static bool ShouldStartTutorial()
        {
            return PlayerPrefs.GetInt(MetaTutorialCompletedKey, 0) != 1;
        }

        private static string T(string key)
        {
            return LeanLocalization.GetTranslationText(key, key);
        }

        private void Awake()
        {
            if (_tutorialManager != null)
                _tutorialManager.SetStartAutomatically(false);
        }

        private void OnEnable()
        {
            Subscribe();
            _initializationCoroutine = StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            Debug.LogWarning("[MetaTutorialTrace] OnDisable START", this);
            Debug.LogWarning(
                $"[MetaTutorialTrace] OnDisable DETAILS\n" +
                $"gameObject.name={gameObject.name}\n" +
                $"activeSelf={gameObject.activeSelf}\n" +
                $"activeInHierarchy={gameObject.activeInHierarchy}\n" +
                $"enabled={enabled}\n" +
                $"transform path={GetTransformPath(transform)}\n" +
                $"Environment.StackTrace:\n{Environment.StackTrace}",
                this);

            Debug.LogWarning(
                $"[MetaTutorialTrace] MetaPlanetTutorialController.OnDisable gameObject={gameObject.name} path={GetTransformPath(transform)} activeSelf={gameObject.activeSelf} activeInHierarchy={gameObject.activeInHierarchy} enabled={enabled}\nStackTrace:\n{new StackTrace(true)}",
                this);

            if (_initializationCoroutine != null)
            {
                StopCoroutine(_initializationCoroutine);
                _initializationCoroutine = null;
            }

            StopDialogRetry();
            StopStepCompletedCallback();
            StopStep5BoxTargetSearch();
            StopStep5VariantPurchaseCoroutine();
            StopStep6ShopOpenCoroutine();
            StopDeferredStageAdvanceCoroutine();
            StopStep7AddUpgradeObjectCoroutine();
            StopStep3TogglePolish(false);
            StopStep4AddVariantPolish(false);
            StopStep5VariantBuyButtonPolish(false);
            StopStep6TogglePolish(false);
            StopStep6UpgradeTabPolish(false);
            StopStep7UpgradeObjectPolish(false);
            StopStep8UpgradeButtonPolish(false);
            DisableStep5WorldProxy();
            HideBlocker();
            SetConversationViewsTopPlacement(false, null, "controller disabled");
            Unsubscribe();
        }

        private void LateUpdate()
        {
            RefreshOverlayGlowLayout(_step3ToggleTarget, _step3ToggleGlow, _step3ToggleWorldCorners);
            RefreshOverlayGlowLayout(_step5VariantBuyButtonTarget, _step5VariantBuyButtonGlow, _step5VariantBuyButtonWorldCorners);
            RefreshOverlayGlowLayout(_step7UpgradeObjectTarget, _step7UpgradeObjectGlow, _step7UpgradeObjectWorldCorners);
            RefreshOverlayGlowLayout(_step8UpgradeButtonTarget, _step8UpgradeButtonGlow, _step8UpgradeButtonWorldCorners);
        }

        private void OnValidate()
        {
            if (_tutorialManager == null)
                Debug.LogWarning("MetaPlanetTutorialController requires a TutorialManager.", this);

            if (_dialogManager == null)
                Debug.LogWarning("MetaPlanetTutorialController requires a DialogManager.", this);

            if (_interactionOverlay == null)
                Debug.LogWarning("MetaPlanetTutorialController requires a TutorialInteractionOverlay.", this);

            if (_metaGameRules == null)
                Debug.LogWarning("MetaPlanetTutorialController requires MetaGameRules.", this);

            if (_gameManager == null)
                Debug.LogWarning("MetaPlanetTutorialController requires GameManager.", this);
        }

        private IEnumerator InitializeWhenReady()
        {
            if (!ShouldStartTutorial())
            {
                _initializationCoroutine = null;
                yield break;
            }

            while (!HasRequiredSystems() || !HasValidTargetData())
                yield return null;

            _initializationCoroutine = null;
            Subscribe();
            ConfigureTyping();
            _houseController.SetMetaGameRules(_metaGameRules);
            _houseUpgradeController.SetMetaGameRules(_metaGameRules);
            RunSavedStage();
        }

        private void RunSavedStage()
        {
            if (_isAdvancing || !ShouldStartTutorial())
                return;

            _isAdvancing = true;

            int stage = PlayerPrefs.GetInt(TutorialStageKey, StageFirstVisit);

            switch (stage)
            {
                case StageFirstVisit:
                    StepFirstVisit();
                    break;
                case StageStartingBudget:
                    StepStartingBudget();
                    break;
                case StageOpenVariantTab:
                    StepOpenVariantTab();
                    break;
                case StageAddVariantObject:
                    StepAddVariantObject();
                    break;
                case StageBuyVariantObject:
                    StepBuyVariantObject();
                    break;
                case StageOpenUpgradeTab:
                    StepOpenUpgradeTab();
                    break;
                case StageAddUpgradeObject:
                    StepAddUpgradeObject();
                    break;
                case StageUpgradeObject:
                    StepUpgradeObject();
                    break;
                case StageFirstOrder:
                    StepFirstOrder();
                    break;
                case StageTutorialVideo:
                    StepTutorialVideo();
                    break;
                case StageExplainGoal:
                    StepExplainGoal();
                    break;
                case StageOpenFirstPlanet:
                    StepOpenFirstPlanet();
                    break;
                default:
                    CompleteTutorialAndOpenFirstPlanet();
                    break;
            }

            _isAdvancing = false;
        }

        private void AdvanceToStage(int stage)
        {
            Debug.Log($"[MetaTutorialTrace] AdvanceToStage stage={stage}", this);
            SaveStage(stage);
            RunSavedStage();
        }

        private void SaveStage(int stage)
        {
            PlayerPrefs.SetInt(TutorialStageKey, stage);
            PlayerPrefs.Save();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacement(stage),
                GetStagePositionStepId(stage),
                $"SaveStage stage={stage}");
        }

        private void StepFirstVisit()
        {
            SaveStage(StageFirstVisit);
            ShowBlockingDialog(
                "MetaPlanetTutorial.FirstVisit",
                new[]
                {
                    Line(T("meta_tutorial_step0_dialog_1") + "\n\n" + T("meta_tutorial_step0_dialog_2"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step0_dialog_3") + "\n\n" + T("meta_tutorial_step0_dialog_4"), CharacterMood.Confused)
                },
                () => AdvanceToStage(StageStartingBudget));
        }

        private void StepStartingBudget()
        {
            SaveStage(StageStartingBudget);

            if (PlayerPrefs.GetInt(StartingDiamondsGrantedKey, 0) == 1)
            {
                ShowStartingBudgetDialog();
                return;
            }

            if (_rewardView == null)
            {
                GrantStartingDiamondsWithBalanceAnimation(ShowStartingBudgetDialog);
                return;
            }

            _rewardView.Show(
                T("meta_tutorial_step1_reward_title"),
                T("meta_tutorial_step1_reward_description"),
                string.Format(T("meta_tutorial_step1_reward_amount"), _startingDiamonds),
                T("meta_tutorial_step1_reward_button"),
                () =>
                {
                    GrantStartingDiamondsWithBalanceAnimation(ShowStartingBudgetDialog);
                });
        }

        private void ShowStartingBudgetDialog()
        {
            ShowBlockingDialog(
                "MetaPlanetTutorial.StartingBudget",
                new[]
                {
                    Line(T("meta_tutorial_step1_dialog_1"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step1_dialog_2") + "\n\n" + T("meta_tutorial_step1_dialog_3"), CharacterMood.Excited)
                },
                () => AdvanceToStage(StageOpenVariantTab));
        }

        private void StepOpenVariantTab()
        {
            SaveStage(StageOpenVariantTab);
            ShowBlockingDialog(
                "MetaPlanetTutorial.OpenVariantTab.Dialog",
                new[]
                {
                    Line(T("meta_tutorial_step2_dialog_1") + "\n\n" + T("meta_tutorial_step2_dialog_2"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step2_dialog_3") + "\n\n" + T("meta_tutorial_step2_dialog_4"), CharacterMood.Confused)
                },
                StartOpenVariantTabStep);
        }

        private void StepAddVariantObject()
        {
            Debug.Log("[MetaTutorialTrace] StepAddVariantObject ENTER", this);
            SaveStage(StageAddVariantObject);
            StartAddVariantObjectStep();
        }
        private void StepBuyVariantObject()
        {
            Debug.Log("[MetaTutorialTrace] Step5 started", this);
            SaveStage(StageBuyVariantObject);
            _step5IntroDialogStarted = false;
            _step5ShopCloseRequested = CloseShopPanelForStep5();

            if (IsHousePurchased())
            {
                AdvanceToStage(StageOpenUpgradeTab);
                return;
            }

            if (IsHouseVariantPanelOpen())
            {
                Debug.Log("[MetaTutorialTrace] Step5 switching from box highlight to variant panel UI highlight", this);
                StartVariantPurchaseStepDeferred(Step5OpenSourceAlreadyOpen, false);
                return;
            }

            StopStep5BoxTargetSearch();
            _step5BoxTargetCoroutine = StartCoroutine(StartStep5BoxClickWhenTargetReady());
        }

        private IEnumerator StartStep5BoxClickWhenTargetReady()
        {
            if (_step5ShopCloseRequested)
            {
                Debug.Log("[MetaTutorialTrace] Step5 wait after ShopPanel close started", this);
                yield return null;
                yield return new WaitForSecondsRealtime(Step5ShopCloseSettleSeconds);
                Canvas.ForceUpdateCanvases();
                Debug.Log("[MetaTutorialTrace] Step5 wait after ShopPanel close completed", this);
            }

            Debug.Log("[MetaTutorialTrace] Step5 box target search started", this);

            float startedAt = Time.unscaledTime;
            Transform boxTarget = null;
            string failureReason = string.Empty;
            bool highlightShown = false;

            while (Time.unscaledTime - startedAt <= Step5BoxTargetTimeout)
            {
                if (TryGetStep5BoxTarget(out boxTarget, out failureReason))
                {
                    Debug.Log($"[MetaTutorialTrace] Step5 box target found: {GetTransformPath(boxTarget)}", this);

                    if (TryPrepareStep5BoxClickProxy(boxTarget))
                    {
                        _step5BoxProxyTarget = _worldTargetProxy != null ? _worldTargetProxy.ProxyGameObject : null;
                        Debug.Log($"[MetaTutorialTrace] Step5 box target screen rect before proxy settle: {GetScreenRectDescription(_step5BoxProxyTarget)}", this);
                        yield return null;
                        Debug.Log($"[MetaTutorialTrace] Step5 box target screen rect after 1 frame: {GetScreenRectDescription(_step5BoxProxyTarget)}", this);
                        StartStep5BoxContourGlow(boxTarget);
                        highlightShown = true;
                        break;
                    }

                    _step5BoxProxyTarget = null;
                    Debug.LogWarning("[MetaTutorialTrace] Step5 box target found but invisible click proxy could not be prepared; retrying.", this);
                }

                yield return null;
            }

            _step5BoxTargetCoroutine = null;

            if (!highlightShown)
            {
                if (boxTarget == null)
                    Debug.LogWarning($"[MetaTutorialTrace] Step5 box target not found: {failureReason}", this);
                else
                    Debug.LogWarning("[MetaTutorialTrace] Step5 box target found but active click proxy could not be prepared.", this);

                _step5BoxProxyTarget = null;
            }

            if (_step5BoxProxyTarget != null && _step5BoxProxyTarget.activeInHierarchy)
            {
                Debug.Log("[MetaTutorialTrace] Step5 waiting for box click", this);
                StartStep5BoxClickStep(_step5BoxProxyTarget);
                yield break;
            }

            Debug.LogWarning("[MetaTutorialTrace] Step5 box target unavailable; PanelVariant will not be opened without box click.", this);

        }

        private void StartStep5BoxClickStep(GameObject boxProxyTarget)
        {
            const string stepId = "MetaPlanet.BuyVariantObject.Box";

            Debug.Log(
                $"[MetaTutorialTrace] StartClickStep stepId={stepId} target={GetTransformPath(boxProxyTarget != null ? boxProxyTarget.transform : null)} active={(boxProxyTarget != null && boxProxyTarget.activeInHierarchy)} customBoxPolish=true",
                this);

            _tutorialStepCompleted = HandleStep5BoxClicked;
            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(stepId),
                stepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            TutorialStepData boxStepData = new()
            {
                StepId = stepId,
                MessageText = T("meta_tutorial_step5_box_hint"),
                TargetUI = boxProxyTarget,
                CompletionCondition = TutorialCompletionCondition.TargetUIClicked,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                EnableHighlight = false,
                EnablePulse = false,
                DisableInteractionCutout = true,
                ShowArrow = false
            };

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    boxStepData
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            Debug.Log(
                $"[MetaTutorialTrace] Step5 box config stepId={boxStepData.StepId} target={GetTransformPath(boxProxyTarget != null ? boxProxyTarget.transform : null)} targetName={(boxProxyTarget != null ? boxProxyTarget.name : "null")} EnableHighlight={boxStepData.EnableHighlight} ShowArrow={boxStepData.ShowArrow} CutoutEnabled={!boxStepData.DisableInteractionCutout} noCutout={boxStepData.DisableInteractionCutout} activeOverlayMode={(boxStepData.DisableInteractionCutout ? "FullDimNoCutout" : "Cutout")}",
                this);
            Debug.Log("[MetaTutorialTrace] Step5 standard UI highlight disabled for box", this);
            Debug.Log("[MetaTutorialTrace] Step5 box no-cutout overlay enabled", this);
            _tutorialManager.RestartTutorial();
        }

        private void HandleStep5BoxClicked()
        {
            Debug.Log("[MetaTutorialTrace] Step5 box clicked", this);
            Debug.Log("[MetaTutorialTrace] Step5 switching from box highlight to variant panel UI highlight", this);
            Debug.Log("[MetaTutorialTrace] Step5 box click-step completed", this);
            ClearStep5BoxHighlightAndOverlay();
            StartVariantPurchaseStepDeferred(Step5OpenSourceBoxClick, true);
        }

        private void StartVariantPurchaseStep()
        {
            Debug.LogWarning("[MetaTutorialTrace] Step5 old StartVariantPurchaseStep entry ignored before box click", this);
        }

        private void ShowStep5IntroDialogAfterPanelOpened(string requestedBy)
        {
            if (_step5IntroDialogStarted)
                return;

            _step5IntroDialogStarted = true;
            Debug.Log($"[MetaTutorialTrace] Step5 intro dialog requested by: {requestedBy}", this);
            Debug.Log("[MetaTutorialTrace] Step5 intro dialog started after PanelVariant opened", this);
            ShowNonBlockingDialog(
                "MetaPlanetTutorial.BuyVariantObject.Dialog",
                new[]
                {
                    Line(T("meta_tutorial_step5_dialog_1"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step5_dialog_2") + "\n\n" + T("meta_tutorial_step5_dialog_3"), CharacterMood.Happy)
                },
                null);
        }
        private void StartVariantPurchaseStepDeferred(string source, bool allowOpenPanel)
        {
            StopStep5VariantPurchaseCoroutine();
            _step5VariantPurchaseCoroutine = StartCoroutine(StartVariantPurchaseStepRoutine(source, allowOpenPanel));
        }

        private IEnumerator StartVariantPurchaseStepRoutine(string source, bool allowOpenPanel)
        {
            ClearStep5BoxHighlightAndOverlay();
            yield return null;

            if (_tutorialManager != null && _tutorialManager.IsRunning)
                Debug.Log("[MetaTutorialTrace] Step5 waiting for TutorialManager.IsRunning false before buy button step", this);

            while (_tutorialManager != null && _tutorialManager.IsRunning)
                yield return null;

            if (source == Step5OpenSourceBoxClick)
                Debug.Log("[MetaTutorialTrace] Step5 opening PanelVariant from box click", this);

            Debug.Log("[MetaTutorialTrace] Step5 opening PanelVariant after box clear", this);

            if (!TryPrepareHousePurchaseTarget(source, allowOpenPanel))
            {
                Debug.LogWarning("Meta Planet tutorial could not prepare the first available variant.", this);
                _step5VariantPurchaseCoroutine = null;
                yield break;
            }

            yield return null;
            Canvas.ForceUpdateCanvases();

            if (!TryFindPreparedHousePurchaseButton(out GameObject buyButton))
            {
                Debug.LogWarning("Meta Planet tutorial could not find the prepared variant buy button.", this);
                _step5VariantPurchaseCoroutine = null;
                yield break;
            }

            LogStep5BuyButtonState(buyButton);

            StartStep5VariantBuyButtonStep(buyButton);
            Debug.Log("[MetaTutorialTrace] Step5 UI click step started", this);
            LogStep5OverlayStateAfterPanelOpened();
            Debug.Log($"[MetaTutorialTrace] Step5 buy button target assigned to overlay: {GetTransformPath(buyButton.transform)}", this);
            ShowStep5IntroDialogAfterPanelOpened(nameof(StartVariantPurchaseStepRoutine));
            Debug.Log("[MetaTutorialTrace] Step5 waiting for variant purchase", this);
            _step5VariantPurchaseCoroutine = null;
        }

        private void StepOpenUpgradeTab()
        {
            Debug.Log("[MetaTutorialTrace] Step6 started", this);
            SaveStage(StageOpenUpgradeTab);

            if (_metaUIController != null && _metaUIController.IsUpgradeTabOpen)
            {
                CompleteStep6OpenUpgradeTab();
                return;
            }

            if (_metaUIController != null && !_metaUIController.isDisplayed)
            {
                Debug.Log("[MetaTutorialTrace] Step6 ShopPanel is closed", this);
                GameObject toggleTarget = GetToggleButtonTarget();

                if (toggleTarget == null)
                {
                    Debug.LogWarning("[MetaTutorialTrace] Step6 ToggleButton target not found", this);
                    StartStep6UpgradeTabAfterShopOpen();
                    return;
                }

                Debug.Log("[MetaTutorialTrace] Step6 ToggleButton target found", this);
                StartStep6ToggleShopStep(toggleTarget);
                return;
            }

            StartStep6UpgradeTabHighlight();
        }

        private void HandleStep6ToggleButtonClicked()
        {
            Debug.Log("[MetaTutorialTrace] Step6 ToggleButton clicked", this);
            Debug.Log("[MetaTutorialTrace] Step6 target clicked", this);
            StopStep6TogglePolish();

            if (_metaUIController != null && !_metaUIController.isDisplayed)
                _metaUIController.ToggleUpgradeStorePanel();

            StartStep6UpgradeTabAfterShopOpen();
        }

        private void StartStep6UpgradeTabAfterShopOpen()
        {
            StopStep6ShopOpenCoroutine();
            _step6ShopOpenCoroutine = StartCoroutine(StartStep6UpgradeTabAfterShopOpenRoutine());
        }

        private IEnumerator StartStep6UpgradeTabAfterShopOpenRoutine()
        {
            Debug.Log("[MetaTutorialTrace] Step6 waiting for ShopPanel open animation", this);
            yield return null;
            Canvas.ForceUpdateCanvases();

            float elapsed = 0f;

            while (elapsed < Step6ShopOpenSettleSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Canvas.ForceUpdateCanvases();

            if (_metaUIController != null && !_metaUIController.isDisplayed)
            {
                _metaUIController.ToggleUpgradeStorePanel();
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            Debug.Log("[MetaTutorialTrace] Step6 ShopPanel opened", this);
            _step6ShopOpenCoroutine = null;
            StartStep6UpgradeTabHighlight();
        }

        private void StartStep6UpgradeTabHighlight()
        {
            if (_metaUIController != null && _metaUIController.IsUpgradeTabOpen)
            {
                CompleteStep6OpenUpgradeTab();
                return;
            }

            GameObject upgradeTabTarget = GetUpgradeTabTarget();

            if (upgradeTabTarget == null)
            {
                Debug.LogWarning("[MetaTutorialTrace] Step6 Upgrade tab target not found", this);
                CompleteStep6OpenUpgradeTab();
                return;
            }

            Debug.Log("[MetaTutorialTrace] Step6 Upgrade tab target found", this);
            StartStep6UpgradeTabStep(upgradeTabTarget);
            ShowStep6Dialog();
        }

        private void ShowStep6Dialog()
        {
            SetConversationViewsTopPlacement(
                _metaUIController != null && _metaUIController.isDisplayed,
                "MetaPlanet.OpenUpgradeTab",
                "Step6 dialog");
            Debug.Log("[MetaTutorialTrace] Step6 dialog started", this);
            ShowNonBlockingDialog(
                "MetaPlanetTutorial.OpenUpgradeTab.Dialog",
                new[]
                {
                    Line(T("meta_tutorial_step6_dialog_1"), CharacterMood.Excited),
                    Line(T("meta_tutorial_step6_dialog_2") + "\n\n" + T("meta_tutorial_step6_dialog_3"), CharacterMood.Thinking)
                },
                () => Debug.Log("[MetaTutorialTrace] Step6 dialog completed", this));
        }

        private void StartStep6ToggleShopStep(GameObject target)
        {
            StartStep6RuntimePolishStep(
                Step6ToggleShopStepId,
                T("meta_tutorial_step6_open_shop_hint"),
                target,
                HandleStep6ToggleButtonClicked,
                true);
        }

        private void StartStep6UpgradeTabStep(GameObject target)
        {
            StartStep6RuntimePolishStep(
                Step6OpenUpgradeTabStepId,
                T("meta_tutorial_step6_open_upgrade_hint"),
                target,
                HandleStep6UpgradeTabClicked,
                false);
        }

        private void StartStep6RuntimePolishStep(
            string stepId,
            string hint,
            GameObject target,
            Action completed,
            bool isToggleTarget)
        {
            Debug.Log(
                $"[MetaTutorialTrace] StartClickStep stepId={stepId} target={GetTransformPath(target != null ? target.transform : null)} active={(target != null && target.activeInHierarchy)} step6CustomPolish=true",
                this);

            if (target == null || !target.activeInHierarchy || target.GetComponent<RectTransform>() == null)
            {
                StartClickStep(stepId, hint, target, completed);
                return;
            }

            if (isToggleTarget)
                StartStep6TogglePolish(target);
            else
                StartStep6UpgradeTabPolish(target);

            _tutorialStepCompleted = completed;
            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(stepId),
                stepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            TutorialStepData stepData = new()
            {
                StepId = stepId,
                MessageText = hint,
                TargetUI = target,
                CompletionCondition = TutorialCompletionCondition.TargetUIClicked,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                EnableHighlight = false,
                EnablePulse = false,
                DisableInteractionCutout = true,
                ShowArrow = false
            };

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    stepData
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            Debug.Log("[MetaTutorialTrace] Step6 standard highlight disabled", this);
            Debug.Log("[MetaTutorialTrace] Step6 cutout disabled", this);
            _tutorialManager.RestartTutorial();
        }

        private void HandleStep6UpgradeTabClicked()
        {
            Debug.Log("[MetaTutorialTrace] Step6 Upgrade tab clicked", this);
            Debug.Log("[MetaTutorialTrace] Step6 target clicked", this);
            StopStep6UpgradeTabPolish();
            CompleteStep6OpenUpgradeTab();
        }

        private void CompleteStep6OpenUpgradeTab()
        {
            StopStep6TogglePolish();
            StopStep6UpgradeTabPolish();
            Debug.Log("[MetaTutorialTrace] Step6 completed", this);
            StartDeferredAdvanceToStage(StageAddUpgradeObject);
        }

        private IEnumerator StartStep7AddUpgradeObjectRoutine()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (_metaUIController != null)
            {
                if (!_metaUIController.isDisplayed)
                    _metaUIController.ToggleUpgradeStorePanel();

                if (!_metaUIController.IsUpgradeTabOpen)
                    _metaUIController.ShopsToggle(1);
            }

            yield return null;
            Canvas.ForceUpdateCanvases();

            GameObject upgradeObjectTarget = _metaPlanetManager != null
                ? _metaPlanetManager.FirstUpgradeObjectButton
                : null;

            if (upgradeObjectTarget == null)
            {
                Debug.LogWarning("[MetaTutorialTrace] Step7 first Upgrade object target not found", this);
                _step7AddUpgradeObjectCoroutine = null;
                StartDeferredAdvanceToStage(StageUpgradeObject);
                yield break;
            }

            Debug.Log("[MetaTutorialTrace] Step7 first Upgrade object target found", this);
            StartStep7AddUpgradeObjectStep(upgradeObjectTarget);
            ShowStep7IntroDialog();
            _step7AddUpgradeObjectCoroutine = null;
        }

        private void ShowStep7IntroDialog()
        {
            SetConversationViewsTopPlacement(
                _metaUIController != null && _metaUIController.isDisplayed,
                "MetaPlanet.AddUpgradeObject",
                "Step7 intro dialog");
            Debug.Log("[MetaTutorialTrace] Step7 dialog started", this);
            ShowNonBlockingDialog(
                "MetaPlanetTutorial.AddUpgradeObject.Dialog",
                new[]
                {
                    Line(T("meta_tutorial_step7_intro_1"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step7_intro_2"), CharacterMood.Happy)
                },
                () => Debug.Log("[MetaTutorialTrace] Step7 dialog completed", this));
        }

        private void CompleteStep7AddUpgradeObject()
        {
            StopStep7UpgradeObjectPolish();
            ShowToast(T("meta_tutorial_step7_toast_added"));
            SetConversationViewsTopPlacement(
                _metaUIController != null && _metaUIController.isDisplayed,
                "MetaPlanet.AddUpgradeObject",
                "Step7 outro dialog");
            ShowBlockingDialog(
                "MetaPlanetTutorial.AddUpgradeObject.Done",
                new[]
                {
                    Line(T("meta_tutorial_step7_done_1") + "\n\n" + T("meta_tutorial_step7_done_2"), CharacterMood.Happy)
                },
                () => StartDeferredAdvanceToStage(StageUpgradeObject));
        }

        private void StepAddUpgradeObject()
        {
            SaveStage(StageAddUpgradeObject);
            StopStep7AddUpgradeObjectCoroutine();
            _step7AddUpgradeObjectCoroutine = StartCoroutine(StartStep7AddUpgradeObjectRoutine());
        }

        private void StepUpgradeObject()
        {
            SaveStage(StageUpgradeObject);

            if (IsUpgradePurchasedOrMaxed())
            {
                AdvanceToStage(StageFirstOrder);
                return;
            }

            OpenUpgradePanel();
            Canvas.ForceUpdateCanvases();
            StartStep8UpgradeButtonStep(
                _upgradePanel != null && _upgradePanel.UpgradeButton != null ? _upgradePanel.UpgradeButton.gameObject : null);
        }

        private void StartStep7AddUpgradeObjectStep(GameObject target)
        {
            const string stepId = "MetaPlanet.AddUpgradeObject";

            if (target == null || !target.activeInHierarchy || target.GetComponent<RectTransform>() == null)
            {
                StartClickStep(
                    stepId,
                    T("meta_tutorial_step7_add_upgrade_hint"),
                    target,
                    CompleteStep7AddUpgradeObject);
                return;
            }

            StartStep7UpgradeObjectPolish(target);
            _tutorialStepCompleted = CompleteStep7AddUpgradeObject;
            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(stepId),
                stepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            TutorialStepData stepData = new()
            {
                StepId = stepId,
                MessageText = T("meta_tutorial_step7_add_upgrade_hint"),
                TargetUI = target,
                CompletionCondition = TutorialCompletionCondition.TargetUIClicked,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                EnableHighlight = false,
                EnablePulse = false,
                DisableInteractionCutout = true,
                ShowArrow = false
            };

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    stepData
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            Debug.Log("[MetaTutorialTrace] Step7 standard highlight disabled", this);
            Debug.Log("[MetaTutorialTrace] Step7 cutout disabled", this);
            _tutorialManager.RestartTutorial();
        }

        private void StartStep8UpgradeButtonStep(GameObject target)
        {
            const string stepId = "MetaPlanet.UpgradeObject";

            if (target == null || !target.activeInHierarchy || target.GetComponent<RectTransform>() == null)
            {
                StartManualStep(
                    stepId,
                    T("meta_tutorial_step8_upgrade_hint"),
                    target,
                    CharacterMood.Excited);
                return;
            }

            StartStep8UpgradeButtonPolish(target);
            _tutorialStepCompleted = null;
            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(stepId),
                stepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            TutorialStepData stepData = new()
            {
                StepId = stepId,
                MessageText = T("meta_tutorial_step8_upgrade_hint"),
                TargetUI = target,
                CompletionCondition = TutorialCompletionCondition.Manual,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = CharacterMood.Excited,
                EnableHighlight = false,
                EnablePulse = false,
                DisableInteractionCutout = true,
                ShowArrow = false
            };

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    stepData
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            Debug.Log("[MetaTutorialTrace] Step8 standard highlight disabled", this);
            Debug.Log("[MetaTutorialTrace] Step8 cutout disabled", this);
            _tutorialManager.RestartTutorial();
        }

        private void StepFirstOrder()
        {
            SaveStage(StageFirstOrder);
            ShowBlockingDialog(
                "MetaPlanetTutorial.FirstOrder",
                new[]
                {
                    Line(T("meta_tutorial_step9_dialog_1"), CharacterMood.Thinking, _robotSpeakerId),
                    Line(T("meta_tutorial_step9_dialog_2") + "\n\n" + T("meta_tutorial_step9_dialog_3"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step9_dialog_4") + "\n\n" + T("meta_tutorial_step9_dialog_5"), CharacterMood.Thinking, _robotSpeakerId),
                    Line(T("meta_tutorial_step9_dialog_6") + "\n\n" + T("meta_tutorial_step9_dialog_7"), CharacterMood.Excited),
                    Line(T("meta_tutorial_step9_dialog_8"), CharacterMood.Angry, _robotSpeakerId),
                    Line(T("meta_tutorial_step9_dialog_9"), CharacterMood.Confused)
                },
                () =>
                {
                    if (_contractView != null)
                    {
                        _contractView.Show(
                            T("meta_tutorial_step9_contract_title"),
                            T("meta_tutorial_step9_contract_description"),
                            string.Empty,
                            T("meta_tutorial_step9_contract_button"),
                            () => AdvanceToStage(StageTutorialVideo));
                    }
                    else
                    {
                        AdvanceToStage(StageTutorialVideo);
                    }
                });
        }

        private void StepTutorialVideo()
        {
            SaveStage(StageTutorialVideo);
            bool canSkip = PlayerPrefs.GetInt(TutorialVideoWatchedKey, 0) == 1;

            if (_videoView == null)
            {
                MarkVideoWatched();
                AdvanceToStage(StageExplainGoal);
                return;
            }

            _videoView.Show(canSkip, () =>
            {
                MarkVideoWatched();
                AdvanceToStage(StageExplainGoal);
            });
        }

        private void StepExplainGoal()
        {
            SaveStage(StageExplainGoal);
            ShowBlockingDialog(
                "MetaPlanetTutorial.ExplainGameGoal",
                new[]
                {
                    Line(T("meta_tutorial_step10_dialog_1"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step10_dialog_2"), CharacterMood.Excited),
                    Line(T("meta_tutorial_step10_dialog_3"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step10_dialog_4"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step10_dialog_5"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step10_dialog_6"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step10_dialog_7"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step10_dialog_8"), CharacterMood.Confused),
                    Line(T("meta_tutorial_step10_dialog_9"), CharacterMood.Shocked),
                    Line(T("meta_tutorial_step10_dialog_10"), CharacterMood.Excited),
                    Line(T("meta_tutorial_step10_dialog_11"), CharacterMood.Happy)
                },
                () => AdvanceToStage(StageOpenFirstPlanet));
        }

        private void StepOpenFirstPlanet()
        {
            SaveStage(StageOpenFirstPlanet);

            Action showDialog = () => ShowBlockingDialog(
                "MetaPlanetTutorial.OpenFirstPlanet.Dialog",
                new[]
                {
                    Line(T("meta_tutorial_step11_dialog_1"), CharacterMood.Thinking),
                    Line(T("meta_tutorial_step11_dialog_2"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step11_dialog_3"), CharacterMood.Excited),
                    Line(T("meta_tutorial_step11_dialog_4"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step11_dialog_5"), CharacterMood.Confused)
                },
                CompleteTutorialAndOpenFirstPlanet);

            if (_contractView != null)
            {
                _contractView.Show(
                    T("meta_tutorial_step11_contract_title"),
                    T("meta_tutorial_step11_contract_description"),
                    string.Empty,
                    T("meta_tutorial_step11_contract_button"),
                    showDialog);
            }
            else
            {
                showDialog();
            }
        }
        private void StartClickStep(string stepId, string hint, GameObject target, Action completed)
        {
            Debug.Log(
                $"[MetaTutorialTrace] StartClickStep stepId={stepId} target={GetTransformPath(target != null ? target.transform : null)} active={(target != null && target.activeInHierarchy)}",
                this);
            _tutorialStepCompleted = completed;
            StartStep(stepId, hint, target, TutorialCompletionCondition.TargetUIClicked, CharacterMood.Thinking);
        }

        private void StartAddVariantObjectStep()
        {
            GameObject target = GetFirstVariantObjectTarget();

            if (target == null || !target.activeInHierarchy || target.GetComponent<RectTransform>() == null)
            {
                Debug.Log("[MetaTutorialTrace] Step4 fallback to standard highlight", this);
                StartClickStep(
                    Step4AddVariantObjectStepId,
                    T("meta_tutorial_step3_add_variant_hint"),
                    target,
                    CompleteAddVariantObjectStep);
                return;
            }

            StartStep4AddVariantPolish(target);

            _tutorialStepCompleted = () =>
            {
                Debug.Log("[MetaTutorialTrace] Step4 add variant clicked", this);
                StopStep4AddVariantPolish();
                CompleteAddVariantObjectStep();
            };

            StartAddVariantObjectTutorialStep(target);
        }

        private void StartAddVariantObjectTutorialStep(GameObject target)
        {
            Debug.Log(
                $"[MetaTutorialTrace] StartClickStep stepId={Step4AddVariantObjectStepId} target={GetTransformPath(target != null ? target.transform : null)} active={(target != null && target.activeInHierarchy)} customPolish=true",
                this);

            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(Step4AddVariantObjectStepId),
                Step4AddVariantObjectStepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    new()
                    {
                        StepId = Step4AddVariantObjectStepId,
                        MessageText = T("meta_tutorial_step3_add_variant_hint"),
                        TargetUI = target,
                        CompletionCondition = TutorialCompletionCondition.TargetUIClicked,
                        SpeakerId = _contractorSpeakerId,
                        CharacterMood = CharacterMood.Thinking,
                        EnableHighlight = false,
                        EnablePulse = false,
                        DisableInteractionCutout = true,
                        ShowArrow = false
                    }
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{Step4AddVariantObjectStepId}");

            Debug.Log("[MetaTutorialTrace] Step4 standard UI highlight disabled", this);
            Debug.Log("[MetaTutorialTrace] Step4 overlay cutout kept for visible target", this);
            _tutorialManager.RestartTutorial();
        }

        private void CompleteAddVariantObjectStep()
        {
            ShowToast(T("meta_tutorial_step3_toast_added"));
            ShowBlockingDialog(
                "MetaPlanetTutorial.AddVariantObject.Done",
                new[]
                {
                    Line(T("meta_tutorial_step3_done_1") + "\n\n" + T("meta_tutorial_step3_done_2"), CharacterMood.Happy)
                },
                () => AdvanceToStage(StageBuyVariantObject));
        }

        private void StartOpenVariantTabStep()
        {
            GameObject target = GetVariantTabTarget();

            if (target == null || !target.activeInHierarchy || target.GetComponent<RectTransform>() == null)
            {
                Debug.Log("[MetaTutorialTrace] Step3 fallback to standard highlight", this);
                StartClickStep(
                    Step3OpenVariantTabStepId,
                    T("meta_tutorial_step2_open_variant_hint"),
                    target,
                    () => AdvanceToStage(StageAddVariantObject));
                return;
            }

            StartStep3TogglePolish(target);

            _tutorialStepCompleted = () =>
            {
                Debug.Log("[MetaTutorialTrace] Step3 toggle clicked", this);
                StopStep3TogglePolish();
                AdvanceToStage(StageAddVariantObject);
            };

            StartOpenVariantTabTutorialStep(target);
        }

        private void StartOpenVariantTabTutorialStep(GameObject target)
        {
            Debug.Log(
                $"[MetaTutorialTrace] StartClickStep stepId={Step3OpenVariantTabStepId} target={GetTransformPath(target != null ? target.transform : null)} active={(target != null && target.activeInHierarchy)} customPolish=true",
                this);

            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(Step3OpenVariantTabStepId),
                Step3OpenVariantTabStepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            TutorialStepData stepData = new()
            {
                StepId = Step3OpenVariantTabStepId,
                MessageText = T("meta_tutorial_step2_open_variant_hint"),
                TargetUI = target,
                CompletionCondition = TutorialCompletionCondition.TargetUIClicked,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                EnableHighlight = false,
                EnablePulse = false,
                DisableInteractionCutout = true,
                ShowArrow = false
            };

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    stepData
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{Step3OpenVariantTabStepId}");

            Debug.Log(
                $"[MetaTutorialTrace] Step3 config target={GetTransformPath(target != null ? target.transform : null)} targetName={(target != null ? target.name : "null")} Step3 EnableHighlight={stepData.EnableHighlight} Step3 ShowArrow={stepData.ShowArrow} Step3 CutoutEnabled={!stepData.DisableInteractionCutout} Step3 noCutout={stepData.DisableInteractionCutout} Step3 overlay mode={(stepData.DisableInteractionCutout ? "FullDimNoCutout" : "Cutout")}",
                this);
            _tutorialManager.RestartTutorial();
        }

        private void StartManualStep(string stepId, string hint, GameObject target, CharacterMood mood)
        {
            _tutorialStepCompleted = null;
            StartStep(stepId, hint, target, TutorialCompletionCondition.Manual, mood);
        }

        private void StartStep5VariantBuyButtonStep(GameObject target)
        {
            const string stepId = "MetaPlanet.BuyVariantObject";

            Debug.Log(
                $"[MetaTutorialTrace] StartManualStep stepId={stepId} target={GetTransformPath(target != null ? target.transform : null)} active={(target != null && target.activeInHierarchy)} step5BuyButtonPolish=true",
                this);

            StartStep5VariantBuyButtonPolish(target);
            _tutorialStepCompleted = null;
            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(stepId),
                stepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            TutorialStepData stepData = new()
            {
                StepId = stepId,
                MessageText = T("meta_tutorial_step5_buy_variant_hint"),
                TargetUI = target,
                CompletionCondition = TutorialCompletionCondition.Manual,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                EnableHighlight = false,
                EnablePulse = false,
                DisableInteractionCutout = true,
                ShowArrow = false
            };

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    stepData
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            Debug.Log("[MetaTutorialTrace] Step5 buy button standard highlight disabled", this);
            Debug.Log("[MetaTutorialTrace] Step5 buy button arrow disabled", this);
            Debug.Log("[MetaTutorialTrace] Step5 buy button cutout disabled", this);
            _tutorialManager.RestartTutorial();
        }

        private void StartStep(
            string stepId,
            string hint,
            GameObject target,
            TutorialCompletionCondition condition,
            CharacterMood mood)
        {
            Debug.Log(
                $"[MetaTutorialTrace] Controller.StartStep stepId={stepId} targetNull={target == null} tutorialManagerNull={_tutorialManager == null}",
                this);
            HideBlocker();
            SetConversationViewsTopPlacement(
                ShouldUseTopConversationPlacementForStep(stepId),
                stepId,
                "StartStep");

            if (_tutorialManager == null)
                return;

            _tutorialManager.Configure(
                new List<TutorialStepData>
                {
                    new()
                    {
                        StepId = stepId,
                        MessageText = hint,
                        TargetUI = target,
                        CompletionCondition = condition,
                        SpeakerId = _contractorSpeakerId,
                        CharacterMood = mood,
                        EnableHighlight = target != null,
                        EnablePulse = target != null,
                        ShowArrow = target != null
                    }
                },
                TutorialProgressKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            _tutorialManager.RestartTutorial();
        }

        private void ShowBlockingDialog(string dialogId, DialogLine[] lines, Action completed)
        {
            ShowBlocker();
            _dialogCallbacks[dialogId] = completed;
            RequestDialog(new DialogData
            {
                DialogId = dialogId,
                ListDialogLine = new List<DialogLine>(lines)
            });
        }

        private void ShowNonBlockingDialog(string dialogId, DialogLine[] lines, Action completed)
        {
            _nonBlockingDialogIds.Add(dialogId);
            _dialogCallbacks[dialogId] = completed;
            RequestDialog(new DialogData
            {
                DialogId = dialogId,
                ListDialogLine = new List<DialogLine>(lines)
            });
        }

        private DialogLine Line(string text, CharacterMood mood, string speakerId = null)
        {
            return new DialogLine
            {
                Text = text,
                SpeakerId = string.IsNullOrEmpty(speakerId) ? _contractorSpeakerId : speakerId,
                CharacterMood = mood
            };
        }

        private void RequestDialog(DialogData dialog)
        {
            StopDialogRetry();
            _dialogRetryCoroutine = StartCoroutine(StartDialogWhenAvailable(dialog));
        }

        private IEnumerator StartDialogWhenAvailable(DialogData dialog)
        {
            while (ShouldStartTutorial())
            {
                MessageManager messageManager = MessageManager.Instance;
                bool dialogChannelFree = messageManager != null &&
                                         messageManager.GetCurrentMessage(MessageChannel.Dialog) == null;

                if (!_dialogManager.IsRunning &&
                    dialogChannelFree &&
                    _dialogManager.StartDialog(dialog))
                {
                    _dialogRetryCoroutine = null;
                    yield break;
                }

                yield return null;
            }

            _dialogRetryCoroutine = null;
        }

        private void StopDialogRetry()
        {
            if (_dialogRetryCoroutine == null)
                return;

            StopCoroutine(_dialogRetryCoroutine);
            _dialogRetryCoroutine = null;
        }

        private void HandleDialogCompleted(DialogData dialog)
        {
            if (dialog == null)
                return;

            bool isNonBlockingDialog = _nonBlockingDialogIds.Remove(dialog.DialogId);

            if (!isNonBlockingDialog)
                HideBlocker();

            if (!_dialogCallbacks.TryGetValue(dialog.DialogId, out Action callback))
                return;

            _dialogCallbacks.Remove(dialog.DialogId);
            callback?.Invoke();
        }

        private void HandleStepCompleted(TutorialStepData step)
        {
            Action completed = _tutorialStepCompleted;
            _tutorialStepCompleted = null;

            if (completed != null)
                _stepCompletedCoroutine = StartCoroutine(InvokeStepCompletedNextFrame(completed));
        }

        private IEnumerator InvokeStepCompletedNextFrame(Action completed)
        {
            yield return null;

            _stepCompletedCoroutine = null;
            completed?.Invoke();
        }

        private void StopStepCompletedCallback()
        {
            if (_stepCompletedCoroutine == null)
                return;

            StopCoroutine(_stepCompletedCoroutine);
            _stepCompletedCoroutine = null;
        }

        private void StartStep3TogglePolish(GameObject target)
        {
            StopStep3TogglePolish(false);

            _step3ToggleTarget = target.GetComponent<RectTransform>();

            if (_step3ToggleTarget == null)
                return;

            DestroyExistingStep3Glow(_step3ToggleTarget);
            DestroyExistingStep3OverlayGlow();

            _step3ToggleOriginalScale = _step3ToggleTarget.localScale;
            _step3ToggleGlow = CreateStep3ToggleGlow(_step3ToggleTarget);
            RefreshOverlayGlowLayout(_step3ToggleTarget, _step3ToggleGlow, _step3ToggleWorldCorners);

            _step3TogglePulseSequence = DOTween.Sequence()
                .Append(_step3ToggleTarget.DOScale(_step3ToggleOriginalScale * 1.06f, 0.55f).SetEase(Ease.InOutSine))
                .Append(_step3ToggleTarget.DOScale(_step3ToggleOriginalScale, 0.55f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .OnUpdate(() => RefreshOverlayGlowLayout(_step3ToggleTarget, _step3ToggleGlow, _step3ToggleWorldCorners));

            if (_step3ToggleGlow != null)
            {
                RectTransform glowRect = _step3ToggleGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step3ToggleGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.35f;

                if (glowRect != null)
                {
                    _step3ToggleGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.08f, 0.55f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.75f, 0.55f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.55f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.35f, 0.55f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1)
                        .OnUpdate(() => RefreshOverlayGlowLayout(_step3ToggleTarget, _step3ToggleGlow, _step3ToggleWorldCorners));
                }
            }

            Debug.Log("[MetaTutorialTrace] Step3 toggle polish started", this);
        }

        private void StopStep3TogglePolish(bool log = true)
        {
            bool hadPolish = _step3TogglePulseSequence != null ||
                             _step3ToggleGlowSequence != null ||
                             _step3ToggleGlow != null ||
                             _step3ToggleTarget != null;

            _step3TogglePulseSequence?.Kill(false);
            _step3TogglePulseSequence = null;

            _step3ToggleGlowSequence?.Kill(false);
            _step3ToggleGlowSequence = null;

            if (_step3ToggleTarget != null)
            {
                _step3ToggleTarget.localScale = _step3ToggleOriginalScale;
                DestroyExistingStep3Glow(_step3ToggleTarget);
            }

            if (_step3ToggleGlow != null)
                Destroy(_step3ToggleGlow);

            DestroyExistingStep3OverlayGlow();

            _step3ToggleTarget = null;
            _step3ToggleGlow = null;
            _step3ToggleOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step3 toggle polish stopped", this);
        }

        private void StartStep4AddVariantPolish(GameObject target)
        {
            StopStep4AddVariantPolish(false);

            _step4AddVariantTarget = target.GetComponent<RectTransform>();

            if (_step4AddVariantTarget == null)
                return;

            DestroyExistingRuntimeGlow(_step4AddVariantTarget, Step4AddVariantGlowName);
            DestroyExistingStep4OverlayGlow();

            _step4AddVariantOriginalScale = _step4AddVariantTarget.localScale;
            _step4AddVariantGlow = CreateStep4OverlayGlow(
                _step4AddVariantTarget,
                new Color(1f, 0.72f, 0.24f, 0.16f),
                new Color(1f, 0.78f, 0.32f, 0.36f),
                1.22f,
                1.1f);
            RefreshStep4AddVariantGlowLayout();

            _step4AddVariantPulseSequence = DOTween.Sequence()
                .Append(_step4AddVariantTarget.DOScale(_step4AddVariantOriginalScale * 1.08f, 0.6f).SetEase(Ease.InOutSine))
                .Append(_step4AddVariantTarget.DOScale(_step4AddVariantOriginalScale, 0.6f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .OnUpdate(RefreshStep4AddVariantGlowLayout);

            if (_step4AddVariantGlow != null)
            {
                RectTransform glowRect = _step4AddVariantGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step4AddVariantGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.42f;

                if (glowRect != null)
                {
                    _step4AddVariantGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.06f, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.78f, 0.6f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.42f, 0.6f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1)
                        .OnUpdate(RefreshStep4AddVariantGlowLayout);
                }
            }

            Debug.Log("[MetaTutorialTrace] Step4 add variant polish started", this);
        }

        private void StopStep4AddVariantPolish(bool log = true)
        {
            bool hadPolish = _step4AddVariantPulseSequence != null ||
                             _step4AddVariantGlowSequence != null ||
                             _step4AddVariantGlow != null ||
                             _step4AddVariantTarget != null;

            _step4AddVariantPulseSequence?.Kill(false);
            _step4AddVariantPulseSequence = null;

            _step4AddVariantGlowSequence?.Kill(false);
            _step4AddVariantGlowSequence = null;

            if (_step4AddVariantTarget != null)
            {
                _step4AddVariantTarget.localScale = _step4AddVariantOriginalScale;
                DestroyExistingRuntimeGlow(_step4AddVariantTarget, Step4AddVariantGlowName);
            }

            if (_step4AddVariantGlow != null)
                Destroy(_step4AddVariantGlow);

            DestroyExistingStep4OverlayGlow();

            _step4AddVariantTarget = null;
            _step4AddVariantGlow = null;
            _step4AddVariantOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step4 add variant polish stopped", this);
        }

        private void StartStep5VariantBuyButtonPolish(GameObject target)
        {
            StopStep5VariantBuyButtonPolish(false);

            _step5VariantBuyButtonTarget = target != null
                ? target.GetComponent<RectTransform>()
                : null;

            if (_step5VariantBuyButtonTarget == null)
                return;

            DestroyExistingStep5VariantBuyButtonOverlayGlow();

            _step5VariantBuyButtonOriginalScale = _step5VariantBuyButtonTarget.localScale;
            _step5VariantBuyButtonGlow = CreateOverlaySpriteGlow(
                _step5VariantBuyButtonTarget,
                Step5VariantBuyButtonGlowName,
                new Color(1f, 0.72f, 0.24f, 0.16f),
                new Color(1f, 0.78f, 0.32f, 0.36f),
                1.2f,
                1.08f,
                "Step5 variant buy button");
            RefreshOverlayGlowLayout(_step5VariantBuyButtonTarget, _step5VariantBuyButtonGlow, _step5VariantBuyButtonWorldCorners);

            _step5VariantBuyButtonPulseSequence = DOTween.Sequence()
                .Append(_step5VariantBuyButtonTarget.DOScale(_step5VariantBuyButtonOriginalScale * 1.08f, 0.6f).SetEase(Ease.InOutSine))
                .Append(_step5VariantBuyButtonTarget.DOScale(_step5VariantBuyButtonOriginalScale, 0.6f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .OnUpdate(() => RefreshOverlayGlowLayout(_step5VariantBuyButtonTarget, _step5VariantBuyButtonGlow, _step5VariantBuyButtonWorldCorners));

            if (_step5VariantBuyButtonGlow != null)
            {
                RectTransform glowRect = _step5VariantBuyButtonGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step5VariantBuyButtonGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.42f;

                if (glowRect != null)
                {
                    _step5VariantBuyButtonGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.06f, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.78f, 0.6f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.42f, 0.6f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1)
                        .OnUpdate(() => RefreshOverlayGlowLayout(_step5VariantBuyButtonTarget, _step5VariantBuyButtonGlow, _step5VariantBuyButtonWorldCorners));
                }
            }

            Debug.Log("[MetaTutorialTrace] Step5 variant buy button polish started", this);
        }

        private void StopStep5VariantBuyButtonPolish(bool log = true)
        {
            bool hadPolish = _step5VariantBuyButtonPulseSequence != null ||
                             _step5VariantBuyButtonGlowSequence != null ||
                             _step5VariantBuyButtonGlow != null ||
                             _step5VariantBuyButtonTarget != null;

            _step5VariantBuyButtonPulseSequence?.Kill(false);
            _step5VariantBuyButtonPulseSequence = null;

            _step5VariantBuyButtonGlowSequence?.Kill(false);
            _step5VariantBuyButtonGlowSequence = null;

            if (_step5VariantBuyButtonTarget != null)
                _step5VariantBuyButtonTarget.localScale = _step5VariantBuyButtonOriginalScale;

            if (_step5VariantBuyButtonGlow != null)
                Destroy(_step5VariantBuyButtonGlow);

            DestroyExistingStep5VariantBuyButtonOverlayGlow();

            _step5VariantBuyButtonTarget = null;
            _step5VariantBuyButtonGlow = null;
            _step5VariantBuyButtonOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step5 variant buy button polish stopped", this);
        }

        private void StartStep6TogglePolish(GameObject target)
        {
            StopStep6TogglePolish(false);

            _step6ToggleTarget = target.GetComponent<RectTransform>();

            if (_step6ToggleTarget == null)
                return;

            DestroyExistingRuntimeGlow(_step6ToggleTarget, Step6ToggleGlowName);

            _step6ToggleOriginalScale = _step6ToggleTarget.localScale;
            _step6ToggleGlow = CreateRuntimeSpriteGlow(
                _step6ToggleTarget,
                Step6ToggleGlowName,
                new Color(1f, 0.72f, 0.24f, 0.16f),
                new Color(1f, 0.78f, 0.32f, 0.36f),
                1.16f,
                1.08f,
                "Step6 toggle");

            _step6TogglePulseSequence = DOTween.Sequence()
                .Append(_step6ToggleTarget.DOScale(_step6ToggleOriginalScale * 1.06f, 0.55f).SetEase(Ease.InOutSine))
                .Append(_step6ToggleTarget.DOScale(_step6ToggleOriginalScale, 0.55f).SetEase(Ease.InOutSine))
                .SetLoops(-1);

            if (_step6ToggleGlow != null)
            {
                RectTransform glowRect = _step6ToggleGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step6ToggleGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.38f;

                if (glowRect != null)
                {
                    _step6ToggleGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.06f, 0.55f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.72f, 0.55f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.55f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.38f, 0.55f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1);
                }
            }

            Debug.Log("[MetaTutorialTrace] Step6 toggle polish started", this);
        }

        private void StopStep6TogglePolish(bool log = true)
        {
            bool hadPolish = _step6TogglePulseSequence != null ||
                             _step6ToggleGlowSequence != null ||
                             _step6ToggleGlow != null ||
                             _step6ToggleTarget != null;

            _step6TogglePulseSequence?.Kill(false);
            _step6TogglePulseSequence = null;

            _step6ToggleGlowSequence?.Kill(false);
            _step6ToggleGlowSequence = null;

            if (_step6ToggleTarget != null)
            {
                _step6ToggleTarget.localScale = _step6ToggleOriginalScale;
                DestroyExistingRuntimeGlow(_step6ToggleTarget, Step6ToggleGlowName);
            }

            if (_step6ToggleGlow != null)
                Destroy(_step6ToggleGlow);

            _step6ToggleTarget = null;
            _step6ToggleGlow = null;
            _step6ToggleOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step6 toggle polish stopped", this);
        }

        private void StartStep6UpgradeTabPolish(GameObject target)
        {
            StopStep6UpgradeTabPolish(false);

            _step6UpgradeTabTarget = target.GetComponent<RectTransform>();

            if (_step6UpgradeTabTarget == null)
                return;

            DestroyExistingStep6UpgradeTabOverlayGlow();

            _step6UpgradeTabOriginalScale = _step6UpgradeTabTarget.localScale;
            _step6UpgradeTabGlow = CreateOverlaySpriteGlow(
                _step6UpgradeTabTarget,
                Step6UpgradeTabGlowName,
                new Color(1f, 0.72f, 0.24f, 0.16f),
                new Color(1f, 0.78f, 0.32f, 0.36f),
                1.18f,
                1.08f,
                "Step6 upgrade tab");
            RefreshOverlayGlowLayout(_step6UpgradeTabTarget, _step6UpgradeTabGlow, _step6UpgradeTabWorldCorners);

            _step6UpgradeTabPulseSequence = DOTween.Sequence()
                .Append(_step6UpgradeTabTarget.DOScale(_step6UpgradeTabOriginalScale * 1.08f, 0.6f).SetEase(Ease.InOutSine))
                .Append(_step6UpgradeTabTarget.DOScale(_step6UpgradeTabOriginalScale, 0.6f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .OnUpdate(() => RefreshOverlayGlowLayout(_step6UpgradeTabTarget, _step6UpgradeTabGlow, _step6UpgradeTabWorldCorners));

            if (_step6UpgradeTabGlow != null)
            {
                RectTransform glowRect = _step6UpgradeTabGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step6UpgradeTabGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.42f;

                if (glowRect != null)
                {
                    _step6UpgradeTabGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.06f, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.78f, 0.6f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.42f, 0.6f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1)
                        .OnUpdate(() => RefreshOverlayGlowLayout(_step6UpgradeTabTarget, _step6UpgradeTabGlow, _step6UpgradeTabWorldCorners));
                }
            }

            Debug.Log("[MetaTutorialTrace] Step6 upgrade tab polish started", this);
        }

        private void StopStep6UpgradeTabPolish(bool log = true)
        {
            bool hadPolish = _step6UpgradeTabPulseSequence != null ||
                             _step6UpgradeTabGlowSequence != null ||
                             _step6UpgradeTabGlow != null ||
                             _step6UpgradeTabTarget != null;

            _step6UpgradeTabPulseSequence?.Kill(false);
            _step6UpgradeTabPulseSequence = null;

            _step6UpgradeTabGlowSequence?.Kill(false);
            _step6UpgradeTabGlowSequence = null;

            if (_step6UpgradeTabTarget != null)
                _step6UpgradeTabTarget.localScale = _step6UpgradeTabOriginalScale;

            if (_step6UpgradeTabGlow != null)
                Destroy(_step6UpgradeTabGlow);

            DestroyExistingStep6UpgradeTabOverlayGlow();

            _step6UpgradeTabTarget = null;
            _step6UpgradeTabGlow = null;
            _step6UpgradeTabOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step6 upgrade tab polish stopped", this);
        }

        private void StartStep7UpgradeObjectPolish(GameObject target)
        {
            StopStep7UpgradeObjectPolish(false);

            _step7UpgradeObjectTarget = target != null ? target.GetComponent<RectTransform>() : null;

            if (_step7UpgradeObjectTarget == null)
                return;

            DestroyExistingStep7UpgradeObjectOverlayGlow();

            _step7UpgradeObjectOriginalScale = _step7UpgradeObjectTarget.localScale;
            _step7UpgradeObjectGlow = CreateOverlaySpriteGlow(
                _step7UpgradeObjectTarget,
                Step7UpgradeObjectGlowName,
                new Color(1f, 0.72f, 0.24f, 0.18f),
                new Color(1f, 0.82f, 0.36f, 0.38f),
                1.22f,
                1.08f,
                "Step7 upgrade object");
            RefreshOverlayGlowLayout(_step7UpgradeObjectTarget, _step7UpgradeObjectGlow, _step7UpgradeObjectWorldCorners);

            _step7UpgradeObjectPulseSequence = DOTween.Sequence()
                .Append(_step7UpgradeObjectTarget.DOScale(_step7UpgradeObjectOriginalScale * 1.08f, 0.62f).SetEase(Ease.InOutSine))
                .Append(_step7UpgradeObjectTarget.DOScale(_step7UpgradeObjectOriginalScale, 0.62f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .OnUpdate(() => RefreshOverlayGlowLayout(_step7UpgradeObjectTarget, _step7UpgradeObjectGlow, _step7UpgradeObjectWorldCorners));

            if (_step7UpgradeObjectGlow != null)
            {
                RectTransform glowRect = _step7UpgradeObjectGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step7UpgradeObjectGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.48f;

                if (glowRect != null)
                {
                    _step7UpgradeObjectGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.06f, 0.62f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.78f, 0.62f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.62f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.48f, 0.62f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1)
                        .OnUpdate(() => RefreshOverlayGlowLayout(_step7UpgradeObjectTarget, _step7UpgradeObjectGlow, _step7UpgradeObjectWorldCorners));
                }
            }

            Debug.Log("[MetaTutorialTrace] Step7 upgrade object polish started", this);
        }

        private void StopStep7UpgradeObjectPolish(bool log = true)
        {
            bool hadPolish = _step7UpgradeObjectPulseSequence != null ||
                             _step7UpgradeObjectGlowSequence != null ||
                             _step7UpgradeObjectGlow != null ||
                             _step7UpgradeObjectTarget != null;

            _step7UpgradeObjectPulseSequence?.Kill(false);
            _step7UpgradeObjectPulseSequence = null;

            _step7UpgradeObjectGlowSequence?.Kill(false);
            _step7UpgradeObjectGlowSequence = null;

            if (_step7UpgradeObjectTarget != null)
                _step7UpgradeObjectTarget.localScale = _step7UpgradeObjectOriginalScale;

            if (_step7UpgradeObjectGlow != null)
                Destroy(_step7UpgradeObjectGlow);

            DestroyExistingStep7UpgradeObjectOverlayGlow();

            _step7UpgradeObjectTarget = null;
            _step7UpgradeObjectGlow = null;
            _step7UpgradeObjectOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step7 upgrade object polish stopped", this);
        }

        private void StartStep8UpgradeButtonPolish(GameObject target)
        {
            StopStep8UpgradeButtonPolish(false);

            _step8UpgradeButtonTarget = target != null ? target.GetComponent<RectTransform>() : null;

            if (_step8UpgradeButtonTarget == null)
                return;

            DestroyExistingStep8UpgradeButtonOverlayGlow();

            _step8UpgradeButtonOriginalScale = _step8UpgradeButtonTarget.localScale;
            _step8UpgradeButtonGlow = CreateOverlaySpriteGlow(
                _step8UpgradeButtonTarget,
                Step8UpgradeButtonGlowName,
                new Color(1f, 0.72f, 0.24f, 0.18f),
                new Color(1f, 0.82f, 0.36f, 0.38f),
                1.22f,
                1.08f,
                "Step8 upgrade button");
            RefreshOverlayGlowLayout(_step8UpgradeButtonTarget, _step8UpgradeButtonGlow, _step8UpgradeButtonWorldCorners);

            _step8UpgradeButtonPulseSequence = DOTween.Sequence()
                .Append(_step8UpgradeButtonTarget.DOScale(_step8UpgradeButtonOriginalScale * 1.08f, 0.62f).SetEase(Ease.InOutSine))
                .Append(_step8UpgradeButtonTarget.DOScale(_step8UpgradeButtonOriginalScale, 0.62f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .OnUpdate(() => RefreshOverlayGlowLayout(_step8UpgradeButtonTarget, _step8UpgradeButtonGlow, _step8UpgradeButtonWorldCorners));

            if (_step8UpgradeButtonGlow != null)
            {
                RectTransform glowRect = _step8UpgradeButtonGlow.transform as RectTransform;
                CanvasGroup canvasGroup = _step8UpgradeButtonGlow.GetComponent<CanvasGroup>();

                if (canvasGroup != null)
                    canvasGroup.alpha = 0.48f;

                if (glowRect != null)
                {
                    _step8UpgradeButtonGlowSequence = DOTween.Sequence()
                        .Join(glowRect.DOScale(Vector3.one * 1.06f, 0.62f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.78f, 0.62f).SetEase(Ease.InOutSine)
                            : null)
                        .Append(glowRect.DOScale(Vector3.one, 0.62f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup != null
                            ? canvasGroup.DOFade(0.48f, 0.62f).SetEase(Ease.InOutSine)
                            : null)
                        .SetLoops(-1)
                        .OnUpdate(() => RefreshOverlayGlowLayout(_step8UpgradeButtonTarget, _step8UpgradeButtonGlow, _step8UpgradeButtonWorldCorners));
                }
            }

            Debug.Log("[MetaTutorialTrace] Step8 upgrade button polish started", this);
        }

        private void StopStep8UpgradeButtonPolish(bool log = true)
        {
            bool hadPolish = _step8UpgradeButtonPulseSequence != null ||
                             _step8UpgradeButtonGlowSequence != null ||
                             _step8UpgradeButtonGlow != null ||
                             _step8UpgradeButtonTarget != null;

            _step8UpgradeButtonPulseSequence?.Kill(false);
            _step8UpgradeButtonPulseSequence = null;

            _step8UpgradeButtonGlowSequence?.Kill(false);
            _step8UpgradeButtonGlowSequence = null;

            if (_step8UpgradeButtonTarget != null)
                _step8UpgradeButtonTarget.localScale = _step8UpgradeButtonOriginalScale;

            if (_step8UpgradeButtonGlow != null)
                Destroy(_step8UpgradeButtonGlow);

            DestroyExistingStep8UpgradeButtonOverlayGlow();

            _step8UpgradeButtonTarget = null;
            _step8UpgradeButtonGlow = null;
            _step8UpgradeButtonOriginalScale = Vector3.one;

            if (log && hadPolish)
                Debug.Log("[MetaTutorialTrace] Step8 upgrade button polish stopped", this);
        }

        private GameObject CreateStep4OverlayGlow(
            RectTransform target,
            Color outerColor,
            Color innerColor,
            float outerScale,
            float innerScale)
        {
            return CreateOverlaySpriteGlow(
                target,
                Step4AddVariantGlowName,
                outerColor,
                innerColor,
                outerScale,
                innerScale,
                "Step4 add variant");
        }

        private GameObject CreateOverlaySpriteGlow(
            RectTransform target,
            string glowName,
            Color outerColor,
            Color innerColor,
            float outerScale,
            float innerScale,
            string logLabel)
        {
            Image sourceImage = GetStep3ToggleGlowSourceImage(target);
            RectTransform overlayRoot = _interactionOverlay != null
                ? _interactionOverlay.transform as RectTransform
                : null;

            if (sourceImage == null || sourceImage.sprite == null || overlayRoot == null)
                return null;

            GameObject root = new(glowName, typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            LayoutElement layoutElement = root.GetComponent<LayoutElement>();

            rootRect.SetParent(overlayRoot, false);
            rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;
            rootRect.SetAsLastSibling();

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }

            if (layoutElement != null)
                layoutElement.ignoreLayout = true;

            AddStep3SpriteGlow(rootRect, "OuterSpriteGlow", sourceImage, outerColor, outerScale);
            AddStep3SpriteGlow(rootRect, "InnerSpriteGlow", sourceImage, innerColor, innerScale);

            Debug.Log(
                $"[MetaTutorialTrace] {logLabel} overlay glow sprite={sourceImage.sprite.name} sourceImage={GetTransformPath(sourceImage.transform)} imageType={sourceImage.type}",
                this);

            return root;
        }

        private void DestroyExistingStep4OverlayGlow()
        {
            DestroyExistingOverlayGlow(Step4AddVariantGlowName);
        }

        private void DestroyExistingStep3OverlayGlow()
        {
            DestroyExistingOverlayGlow(Step3ToggleGlowName);
        }

        private void DestroyExistingStep5VariantBuyButtonOverlayGlow()
        {
            DestroyExistingOverlayGlow(Step5VariantBuyButtonGlowName);
        }

        private void DestroyExistingStep6UpgradeTabOverlayGlow()
        {
            DestroyExistingOverlayGlow(Step6UpgradeTabGlowName);
        }

        private void DestroyExistingStep7UpgradeObjectOverlayGlow()
        {
            DestroyExistingOverlayGlow(Step7UpgradeObjectGlowName);
        }

        private void DestroyExistingStep8UpgradeButtonOverlayGlow()
        {
            DestroyExistingOverlayGlow(Step8UpgradeButtonGlowName);
        }

        private void DestroyExistingOverlayGlow(string glowName)
        {
            RectTransform overlayRoot = _interactionOverlay != null
                ? _interactionOverlay.transform as RectTransform
                : null;

            if (overlayRoot == null)
                return;

            for (int i = overlayRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = overlayRoot.GetChild(i);

                if (child != null && child.name == glowName)
                    Destroy(child.gameObject);
            }
        }

        private void RefreshStep4AddVariantGlowLayout()
        {
            RefreshOverlayGlowLayout(_step4AddVariantTarget, _step4AddVariantGlow, _step4AddVariantWorldCorners);
        }

        private void RefreshOverlayGlowLayout(
            RectTransform target,
            GameObject glow,
            Vector3[] worldCorners)
        {
            if (target == null || glow == null || worldCorners == null || worldCorners.Length < 4)
                return;

            RectTransform glowRect = glow.transform as RectTransform;
            RectTransform overlayRoot = _interactionOverlay != null
                ? _interactionOverlay.transform as RectTransform
                : null;

            if (glowRect == null || overlayRoot == null)
                return;

            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            Canvas overlayCanvas = overlayRoot.GetComponentInParent<Canvas>();

            if (targetCanvas == null || overlayCanvas == null)
                return;

            Camera targetCamera = targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera
                : null;
            Camera overlayCamera = overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? overlayCanvas.worldCamera
                : null;

            target.GetWorldCorners(worldCorners);

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            int convertedPointCount = 0;

            for (int i = 0; i < 4; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, worldCorners[i]);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        overlayRoot,
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
                return;

            glowRect.anchoredPosition = (min + max) * 0.5f;
            glowRect.sizeDelta = max - min;
            glowRect.SetAsLastSibling();
        }

        private GameObject CreateStep3ToggleGlow(RectTransform target)
        {
            return CreateOverlaySpriteGlow(
                target,
                Step3ToggleGlowName,
                new Color(0.78f, 0.94f, 1f, 0.18f),
                new Color(0.78f, 0.94f, 1f, 0.42f),
                1.16f,
                1.08f,
                "Step3 toggle");
        }

        private GameObject CreateRuntimeSpriteGlow(
            RectTransform target,
            string glowName,
            Color outerColor,
            Color innerColor,
            float outerScale,
            float innerScale,
            string logLabel)
        {
            Image sourceImage = GetStep3ToggleGlowSourceImage(target);

            if (sourceImage == null || sourceImage.sprite == null)
                return null;

            GameObject root = new(glowName, typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rootRect = root.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();

            rootRect.SetParent(target.parent, false);
            rootRect.anchorMin = target.anchorMin;
            rootRect.anchorMax = target.anchorMax;
            rootRect.anchoredPosition = target.anchoredPosition;
            rootRect.sizeDelta = target.sizeDelta;
            rootRect.pivot = target.pivot;
            rootRect.localRotation = target.localRotation;
            rootRect.localScale = Vector3.one;
            rootRect.SetSiblingIndex(target.GetSiblingIndex());

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }

            AddStep3SpriteGlow(rootRect, "OuterSpriteGlow", sourceImage, outerColor, outerScale);
            AddStep3SpriteGlow(rootRect, "InnerSpriteGlow", sourceImage, innerColor, innerScale);

            Debug.Log(
                $"[MetaTutorialTrace] {logLabel} glow sprite={sourceImage.sprite.name} sourceImage={GetTransformPath(sourceImage.transform)} imageType={sourceImage.type}",
                this);

            return root;
        }

        private static Image GetStep3ToggleGlowSourceImage(RectTransform target)
        {
            if (target == null)
                return null;

            Button button = target.GetComponent<Button>();

            if (button != null && button.targetGraphic is Image targetGraphicImage && targetGraphicImage.sprite != null)
                return targetGraphicImage;

            Image image = target.GetComponent<Image>();

            if (image != null && image.sprite != null)
                return image;

            Image[] childImages = target.GetComponentsInChildren<Image>(true);

            for (int i = 0; i < childImages.Length; i++)
            {
                if (childImages[i] != null && childImages[i].sprite != null)
                    return childImages[i];
            }

            return null;
        }

        private static void AddStep3SpriteGlow(
            RectTransform parent,
            string name,
            Image sourceImage,
            Color color,
            float scale)
        {
            GameObject glow = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rectTransform = glow.GetComponent<RectTransform>();
            Image image = glow.GetComponent<Image>();

            rectTransform.SetParent(parent, false);
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one * scale;

            image.sprite = sourceImage.sprite;
            image.type = sourceImage.type;
            image.preserveAspect = sourceImage.preserveAspect;
            image.fillCenter = sourceImage.fillCenter;
            image.fillMethod = sourceImage.fillMethod;
            image.fillAmount = sourceImage.fillAmount;
            image.fillClockwise = sourceImage.fillClockwise;
            image.fillOrigin = sourceImage.fillOrigin;
            image.useSpriteMesh = sourceImage.useSpriteMesh;
            image.pixelsPerUnitMultiplier = sourceImage.pixelsPerUnitMultiplier;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void DestroyExistingStep3Glow(RectTransform target)
        {
            DestroyExistingRuntimeGlow(target, Step3ToggleGlowName);
        }

        private static void DestroyExistingRuntimeGlow(RectTransform target, string glowName)
        {
            if (target == null)
                return;

            Transform parent = target.parent;

            if (parent != null)
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform child = parent.GetChild(i);

                    if (child != null && child.name == glowName)
                        Destroy(child.gameObject);
                }
            }

            for (int i = target.childCount - 1; i >= 0; i--)
            {
                Transform child = target.GetChild(i);

                if (child != null && child.name == glowName)
                    Destroy(child.gameObject);
            }
        }

        private void StartStep5BoxContourGlow(Transform boxTarget)
        {
            StopStep5BoxContourGlow(false);

            if (boxTarget == null)
            {
                Debug.Log("[MetaTutorialTrace] Step5 box contour renderer not found", this);
                return;
            }

            MeshRenderer[] renderers = boxTarget.GetComponentsInChildren<MeshRenderer>(false);

            if (renderers == null || renderers.Length == 0)
            {
                Debug.Log("[MetaTutorialTrace] Step5 box contour renderer not found", this);
                return;
            }

            _step5BoxContourGlow = new GameObject(Step5BoxContourGlowName);
            _step5BoxContourGlow.transform.SetParent(boxTarget, false);
            _step5BoxContourGlow.transform.localPosition = Vector3.zero;
            _step5BoxContourGlow.transform.localRotation = Quaternion.identity;
            _step5BoxContourGlow.transform.localScale = Vector3.one;

            int createdCount = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer sourceRenderer = renderers[i];

                if (sourceRenderer == null || !sourceRenderer.enabled)
                    continue;

                MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();

                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                    continue;

                AddStep5BoxContourLayer(sourceRenderer, sourceFilter, "SoftOuterContour", 1.22f, 0.07f);
                AddStep5BoxContourLayer(sourceRenderer, sourceFilter, "OuterContour", 1.12f, 0.16f);
                AddStep5BoxContourLayer(sourceRenderer, sourceFilter, "InnerContour", 1.06f, 0.24f);
                createdCount++;
            }

            if (createdCount == 0)
            {
                StopStep5BoxContourGlow(false);
                Debug.Log("[MetaTutorialTrace] Step5 box contour renderer not found", this);
                return;
            }

            Debug.Log("[MetaTutorialTrace] Step5 box contour renderer found", this);

            float alphaMultiplier = 1f;
            _step5BoxContourGlowSequence = DOTween.Sequence()
                .Append(DOTween.To(
                    () => alphaMultiplier,
                    value =>
                    {
                        alphaMultiplier = value;
                        SetStep5BoxContourGlowAlphaMultiplier(alphaMultiplier);
                    },
                    0.62f,
                    0.9f).SetEase(Ease.InOutSine))
                .Append(DOTween.To(
                    () => alphaMultiplier,
                    value =>
                    {
                        alphaMultiplier = value;
                        SetStep5BoxContourGlowAlphaMultiplier(alphaMultiplier);
                    },
                    1f,
                    0.9f).SetEase(Ease.InOutSine))
                .SetLoops(-1);

            Debug.Log("[MetaTutorialTrace] Step5 box contour glow started", this);
            Debug.Log("[MetaTutorialTrace] Step5 box world glow started", this);
        }

        private void AddStep5BoxContourLayer(
            MeshRenderer sourceRenderer,
            MeshFilter sourceFilter,
            string layerName,
            float scale,
            float alpha)
        {
            GameObject layer = new(layerName, typeof(MeshFilter), typeof(MeshRenderer));
            Transform layerTransform = layer.transform;
            layerTransform.SetPositionAndRotation(sourceRenderer.transform.position, sourceRenderer.transform.rotation);
            layerTransform.localScale = sourceRenderer.transform.lossyScale;
            layerTransform.SetParent(_step5BoxContourGlow.transform, true);
            layerTransform.localScale *= scale;

            MeshFilter meshFilter = layer.GetComponent<MeshFilter>();
            meshFilter.sharedMesh = sourceFilter.sharedMesh;

            MeshRenderer meshRenderer = layer.GetComponent<MeshRenderer>();
            Material material = CreateStep5BoxContourMaterial(alpha);
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.allowOcclusionWhenDynamic = false;

            _step5BoxContourGlowMaterials.Add(material);
            _step5BoxContourGlowBaseAlphas.Add(alpha);
        }

        private Material CreateStep5BoxContourMaterial(float alpha)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new(shader)
            {
                name = "RuntimeStep5BoxContourGlowMaterial",
                renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent
            };

            Color color = new(1f, 0.72f, 0.24f, alpha);
            material.SetOverrideTag("RenderType", "Transparent");

            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);

            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            if (material.HasProperty("_Surface"))
                material.SetInt("_Surface", 1);

            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);

            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", 0);

            if (material.HasProperty("_Cull"))
                material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Front);

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            return material;
        }

        private void SetStep5BoxContourGlowAlphaMultiplier(float multiplier)
        {
            for (int i = 0; i < _step5BoxContourGlowMaterials.Count; i++)
            {
                Material material = _step5BoxContourGlowMaterials[i];

                if (material == null)
                    continue;

                Color color = material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : material.color;
                float baseAlpha = i < _step5BoxContourGlowBaseAlphas.Count
                    ? _step5BoxContourGlowBaseAlphas[i]
                    : 0.16f;
                color.a = baseAlpha * multiplier;

                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);

                if (material.HasProperty("_Color"))
                    material.SetColor("_Color", color);
            }
        }

        private void StopStep5BoxContourGlow(bool log = true)
        {
            bool hadGlow = _step5BoxContourGlowSequence != null ||
                           _step5BoxContourGlow != null ||
                           _step5BoxContourGlowMaterials.Count > 0;

            _step5BoxContourGlowSequence?.Kill(false);
            _step5BoxContourGlowSequence = null;

            if (_step5BoxContourGlow != null)
                Destroy(_step5BoxContourGlow);

            _step5BoxContourGlow = null;

            for (int i = 0; i < _step5BoxContourGlowMaterials.Count; i++)
            {
                if (_step5BoxContourGlowMaterials[i] != null)
                    Destroy(_step5BoxContourGlowMaterials[i]);
            }

            _step5BoxContourGlowMaterials.Clear();
            _step5BoxContourGlowBaseAlphas.Clear();

            if (log && hadGlow)
            {
                Debug.Log("[MetaTutorialTrace] Step5 box contour glow stopped", this);
                Debug.Log("[MetaTutorialTrace] Step5 box world glow stopped", this);
            }
        }

        private void HandleTutorialCompleted()
        {
        }

        private void HandleHouseVariantPurchased(MetaVariantItemController item, int variantIndex)
        {
            TutorialStepData currentStep = _tutorialManager != null ? _tutorialManager.CurrentStep : null;

            if (currentStep == null ||
                currentStep.StepId != "MetaPlanet.BuyVariantObject" ||
                item != _targetHouseItem ||
                variantIndex != _targetHouseVariantIndex ||
                !item.IsVariantBought(variantIndex))
            {
                return;
            }

            Debug.Log("[MetaTutorialTrace] Step5 variant purchased", this);
            Debug.Log("[MetaTutorialTrace] Step5 buy button clicked", this);
            StopStep5VariantBuyButtonPolish();
            _houseController.SaveToES3();
            _gameManager.SaveGeneralGameData();
            _tutorialManager.CompleteManualStep();
            ShowToast(T("meta_tutorial_step5_toast_built"));
            CloseVariantPanelIfOpen();
            ShowBlockingDialog(
                "MetaPlanetTutorial.BuyVariantObject.Done",
                new[]
                {
                    Line(T("meta_tutorial_step5_done_1") + "\n\n" + T("meta_tutorial_step5_done_2"), CharacterMood.Happy),
                    Line(T("meta_tutorial_step5_done_3"), CharacterMood.Confused)
                },
                () =>
                {
                    Debug.Log("[MetaTutorialTrace] Step5 completed", this);
                    AdvanceToStage(StageOpenUpgradeTab);
                });
        }

        private void HandleHouseUpgradePurchased()
        {
            TutorialStepData currentStep = _tutorialManager != null ? _tutorialManager.CurrentStep : null;

            if (currentStep == null || currentStep.StepId != "MetaPlanet.UpgradeObject")
                return;

            StopStep8UpgradeButtonPolish();
            _gameManager.SaveGeneralGameData();
            _tutorialManager.CompleteManualStep();
            ShowToast(T("meta_tutorial_step8_toast_upgraded"));
            CloseUpgradePanelIfOpen();
            ShowBlockingDialog(
                "MetaPlanetTutorial.UpgradeObject.Done",
                new[]
                {
                    Line(T("meta_tutorial_step8_done_1") + "\n\n" + T("meta_tutorial_step8_done_2"), CharacterMood.Excited),
                    Line(T("meta_tutorial_step8_done_3") + "\n\n" + T("meta_tutorial_step8_done_4"), CharacterMood.Thinking)
                },
                () => AdvanceToStage(StageFirstOrder));
        }
        private void HighlightDiamondBalance(Action completed)
        {
            if (_diamondBalanceTarget == null)
            {
                completed?.Invoke();
                return;
            }

            StartStep(
                "MetaPlanet.HighlightDiamondBalance",
                string.Empty,
                _diamondBalanceTarget,
                TutorialCompletionCondition.Manual,
                CharacterMood.Happy);

            StartCoroutine(CompleteBalanceHighlightNextFrame(completed));
        }

        private IEnumerator CompleteBalanceHighlightNextFrame(Action completed)
        {
            yield return new WaitForSecondsRealtime(0.8f);

            if (_tutorialManager != null &&
                _tutorialManager.CurrentStep != null &&
                _tutorialManager.CurrentStep.StepId == "MetaPlanet.HighlightDiamondBalance")
            {
                _tutorialManager.CompleteManualStep();
            }

            completed?.Invoke();
        }

        private void ShowToast(string text)
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null)
            {
                Debug.Log(text);
                return;
            }

            messageManager.ShowMessage(new MessageData
            {
                Id = $"MetaPlanetTutorial.Toast.{text}",
                Text = text,
                Type = MessageType.Toast,
                Channel = MessageChannel.Toast,
                Priority = 100,
                DisplayDuration = 1.5f,
                CanInterrupt = true,
                CanDisplayOverModal = true
            });
        }

        private void CloseVariantPanelIfOpen()
        {
            if (_houseController == null ||
                _houseController._variantButtonList == null ||
                _houseController._variantButtonList.Count == 0)
            {
                return;
            }

            _houseController.CloseVariantPanel();
            SetConversationViewsTopPlacement(false);
        }

        private void CloseUpgradePanelIfOpen()
        {
            if (_upgradePanel == null || !_upgradePanel.gameObject.activeInHierarchy)
                return;

            _upgradePanel.Close();
            SetConversationViewsTopPlacement(false);
        }

        private void MarkVideoWatched()
        {
            PlayerPrefs.SetInt(TutorialVideoWatchedKey, 1);
            PlayerPrefs.Save();
        }

        private void OpenUpgradePanel()
        {
            if (_upgradePanel != null && _houseUpgradeController != null)
                _upgradePanel.Open(_houseUpgradeController);
        }

        private GameObject GetVariantTabTarget()
        {
            return _metaUIController != null ? _metaUIController.ToggleButtonGameObject : null;
        }

        private GameObject GetToggleButtonTarget()
        {
            return _metaUIController != null ? _metaUIController.ToggleButtonGameObject : null;
        }

        private GameObject GetFirstVariantObjectTarget()
        {
            if (_metaUIController != null && !_metaUIController.isDisplayed)
                _metaUIController.ToggleUpgradeStorePanel();

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.SetShopOpen(false);

            SetConversationViewsTopPlacement(_metaUIController != null && _metaUIController.isDisplayed);
            Canvas.ForceUpdateCanvases();

            return _metaPlanetManager != null ? _metaPlanetManager.FirstVariantObjectButton : null;
        }

        private bool CloseShopPanelForStep5()
        {
            Debug.Log("[MetaTutorialTrace] Step5 close ShopPanel requested", this);
            Debug.Log("[MetaTutorialTrace] Step5 ShopPanel close requested before box target search", this);

            if (_metaUIController == null)
            {
                Debug.LogWarning("[MetaTutorialTrace] Step5 ShopPanel close failed: MetaUIController is missing", this);
                return false;
            }

            if (!_metaUIController.isDisplayed)
            {
                Debug.Log("[MetaTutorialTrace] Step5 ShopPanel already closed", this);
                return false;
            }

            _metaUIController.ToggleUpgradeStorePanel();
            SetConversationViewsTopPlacement(false);
            Debug.Log("[MetaTutorialTrace] Step5 ShopPanel closed", this);
            return true;
        }

        private bool IsHouseVariantPanelOpen()
        {
            return _houseController != null &&
                   _houseController._variantButtonList != null &&
                   _houseController._variantButtonList.Count > 0;
        }

        private void LogStep5OverlayStateAfterPanelOpened()
        {
            bool overlayBlocking = _interactionOverlay != null && _interactionOverlay.IsBlockingRaycasts;
            string targetPath = _interactionOverlay != null
                ? GetTransformPath(_interactionOverlay.CurrentTargetUI != null ? _interactionOverlay.CurrentTargetUI.transform : null)
                : "null";

            Debug.Log(
                $"[MetaTutorialTrace] Step5 overlay blocking state after panel opened: blocking={overlayBlocking} target={targetPath}",
                this);
        }

        private void LogStep5BuyButtonState(GameObject buyButton)
        {
            Button button = buyButton != null ? buyButton.GetComponent<Button>() : null;

            if (button == null && buyButton != null)
                button = buyButton.GetComponentInParent<Button>();

            if (button == null && buyButton != null)
                button = buyButton.GetComponentInChildren<Button>(true);

            bool active = buyButton != null && buyButton.activeInHierarchy;
            bool interactable = button != null && button.interactable;

            Debug.Log(
                $"[MetaTutorialTrace] Step5 buy button interactable state: target={GetTransformPath(buyButton != null ? buyButton.transform : null)} active={active} hasButton={button != null} button={GetTransformPath(button != null ? button.transform : null)} interactable={interactable}",
                this);
        }

        private GameObject GetUpgradeTabTarget()
        {
            return _metaUIController != null ? _metaUIController.UpgradeTabButtonGameObject : null;
        }

        private void GrantStartingDiamonds()
        {
            if (PlayerPrefs.GetInt(StartingDiamondsGrantedKey, 0) == 1)
                return;

            if (_startingDiamonds > 0d && _metaGameRules != null && _metaGameRules.IsPrepared)
            {
                _metaGameRules.GetPurchasedProduct(_startingDiamonds);

                if (_gameManager != null)
                    _gameManager.SaveGeneralGameData();
            }

            PlayerPrefs.SetInt(StartingDiamondsGrantedKey, 1);
            PlayerPrefs.Save();
        }

        private void GrantStartingDiamondsWithBalanceAnimation(Action completed)
        {
            double oldValue = _metaGameRules != null && _metaGameRules.IsPrepared
                ? _metaGameRules.CurrentDiamonds
                : 0d;

            GrantStartingDiamonds();

            double newValue = _metaGameRules != null && _metaGameRules.IsPrepared
                ? _metaGameRules.CurrentDiamonds
                : oldValue;

            ScorePanel diamondScorePanel = GetDiamondScorePanel();

            if (diamondScorePanel == null || Mathf.Approximately((float)oldValue, (float)newValue))
            {
                completed?.Invoke();
                return;
            }

            Tween animation = diamondScorePanel.AnimateDiamondsScore(oldValue, newValue, 1.2f);

            if (animation == null)
            {
                diamondScorePanel.SetDiamondsScore(newValue);
                completed?.Invoke();
                return;
            }

            StartCoroutine(CompleteAfterDiamondsAnimation(animation, completed));
        }

        private ScorePanel GetDiamondScorePanel()
        {
            if (_diamondBalanceTarget == null)
                return null;

            ScorePanel scorePanel = _diamondBalanceTarget.GetComponent<ScorePanel>();

            if (scorePanel != null)
                return scorePanel;

            scorePanel = _diamondBalanceTarget.GetComponentInChildren<ScorePanel>(true);

            if (scorePanel != null)
                return scorePanel;

            return _diamondBalanceTarget.GetComponentInParent<ScorePanel>();
        }

        private IEnumerator CompleteAfterDiamondsAnimation(Tween animation, Action completed)
        {
            while (animation != null && animation.IsActive() && !animation.IsComplete())
                yield return null;

            completed?.Invoke();
        }

        private void GrantFinalReward()
        {
            if (_finalDiamondsReward <= 0d || _metaGameRules == null || !_metaGameRules.IsPrepared)
                return;

            _metaGameRules.GetPurchasedProduct(_finalDiamondsReward);

            if (_gameManager != null)
                _gameManager.SaveGeneralGameData();
        }

        private void CompleteTutorialAndOpenFirstPlanet()
        {
            GrantFinalReward();
            HideBlocker();

            if (_tutorialManager != null)
                _tutorialManager.CancelTutorial();

            PlayerPrefs.SetInt(MetaTutorialCompletedKey, 1);
            PlayerPrefs.DeleteKey(TutorialProgressKey);
            PlayerPrefs.DeleteKey(TutorialStageKey);
            PlayerPrefs.Save();

            if (!string.IsNullOrEmpty(_firstPlanetSceneName) && Application.CanStreamedLevelBeLoaded(_firstPlanetSceneName))
            {
                SceneManager.LoadScene(_firstPlanetSceneName);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(_firstPlanetSceneIndex))
                SceneManager.LoadScene(_firstPlanetSceneIndex);
            else
                Debug.LogWarning("Meta Planet tutorial completed, but the first planet scene is not available in Build Settings.", this);
        }

        private void ShowBlocker()
        {
            if (_interactionOverlay != null)
                _interactionOverlay.Show(null);
        }

        private void HideBlocker()
        {
            if (_interactionOverlay != null)
                _interactionOverlay.Hide();
        }

        private void SetConversationViewsTopPlacement(bool useTopPlacement, string stepId = null, string reason = null)
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null)
                return;

            if (useTopPlacement && _metaUIController != null && _metaUIController.isDisplayed)
                messageManager.SetShopOpen(false);

            messageManager.SetConversationViewsTopPlacement(useTopPlacement, stepId, reason);
        }

        private bool ShouldUseTopConversationPlacement(int stage)
        {
            return _metaUIController != null &&
                   _metaUIController.isDisplayed &&
                   (stage == StageAddVariantObject ||
                    stage == StageBuyVariantObject ||
                    stage == StageOpenUpgradeTab ||
                    stage == StageAddUpgradeObject ||
                    stage == StageUpgradeObject);
        }

        private bool ShouldUseTopConversationPlacementForStep(string stepId)
        {
            if (_metaUIController == null || !_metaUIController.isDisplayed)
                return false;

            return stepId == "MetaPlanet.AddVariantObject" ||
                   stepId == "MetaPlanet.BuyVariantObject" ||
                   stepId == "MetaPlanet.OpenUpgradeTab" ||
                   stepId == "MetaPlanet.AddUpgradeObject" ||
                   stepId == "MetaPlanet.UpgradeObject";
        }

        private string GetStagePositionStepId(int stage)
        {
            switch (stage)
            {
                case StageAddVariantObject:
                    return "MetaPlanet.AddVariantObject";
                case StageBuyVariantObject:
                    return "MetaPlanet.BuyVariantObject";
                case StageOpenUpgradeTab:
                    return "MetaPlanet.OpenUpgradeTab";
                case StageAddUpgradeObject:
                    return "MetaPlanet.AddUpgradeObject";
                case StageUpgradeObject:
                    return "MetaPlanet.UpgradeObject";
                default:
                    return null;
            }
        }

        private bool TryGetStep5BoxTarget(out Transform boxTarget, out string failureReason)
        {
            boxTarget = null;
            failureReason = string.Empty;

            if (!TryGetHouseItem(out MetaVariantItemController houseItem))
            {
                failureReason = "house item not found";
                return false;
            }

            boxTarget = houseItem.CurrentVisualTarget;

            if (boxTarget == null)
            {
                failureReason = $"current visual target is null for '{houseItem.name}'";
                return false;
            }

            if (!boxTarget.gameObject.activeInHierarchy)
            {
                failureReason = $"current visual target is inactive: {GetTransformPath(boxTarget)}";
                boxTarget = null;
                return false;
            }

            return true;
        }

        private bool TryPrepareStep5BoxClickProxy(Transform boxTarget)
        {
            TutorialHighlightSystem highlightSystem = GetTutorialHighlightSystem();

            if (!EnsureWorldTargetProxy())
            {
                Debug.LogWarning("[MetaTutorialTrace] Step5 box target not found: world target proxy is missing", this);
                return false;
            }

            if (!_worldTargetProxy.SetTarget(boxTarget, _worldTargetCamera))
            {
                Debug.LogWarning("[MetaTutorialTrace] Step5 box target not found: world proxy could not project target", this);
                return false;
            }

            if (highlightSystem != null)
                highlightSystem.Hide();

            Debug.Log("[MetaTutorialTrace] Step5 standard UI highlight disabled for box", this);
            Debug.Log("[MetaTutorialTrace] Step5 invisible click proxy enabled", this);
            Debug.Log("[MetaTutorialTrace] Step5 world proxy enabled", this);
            GameObject proxyTarget = _worldTargetProxy.ProxyGameObject;

            if (proxyTarget == null || !proxyTarget.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"[MetaTutorialTrace] Step5 invalid world proxy target: target={GetTransformPath(proxyTarget != null ? proxyTarget.transform : null)} activeSelf={(proxyTarget != null && proxyTarget.activeSelf)} activeInHierarchy={(proxyTarget != null && proxyTarget.activeInHierarchy)}",
                    this);
                _worldTargetProxy.ClearTarget();
                return false;
            }

            return true;
        }

        private TutorialHighlightSystem GetTutorialHighlightSystem()
        {
            if (_tutorialHighlightSystem == null && _interactionOverlay != null)
                _tutorialHighlightSystem = _interactionOverlay.GetComponent<TutorialHighlightSystem>();

            return _tutorialHighlightSystem;
        }

        private bool EnsureWorldTargetProxy()
        {
            if (_worldTargetProxy != null)
            {
                Debug.Log("[MetaTutorialTrace] WorldTutorialTargetProxy already assigned", this);
                return true;
            }

            RectTransform proxyParent = GetWorldTargetProxyParent();

            if (proxyParent == null)
            {
                Debug.LogWarning("[MetaTutorialTrace] WorldTutorialTargetProxy creation failed: proxy parent not found", this);
                return false;
            }

            if (!proxyParent.gameObject.activeInHierarchy)
            {
                Debug.LogWarning(
                    $"[MetaTutorialTrace] WorldTutorialTargetProxy parent inactive before create: {GetTransformPath(proxyParent)} activeSelf={proxyParent.gameObject.activeSelf} activeInHierarchy={proxyParent.gameObject.activeInHierarchy}",
                    this);

                if (proxyParent.gameObject.activeSelf == false)
                    proxyParent.gameObject.SetActive(true);
            }

            _worldTargetProxy = proxyParent.GetComponentInChildren<WorldTutorialTargetProxy>(true);

            if (_worldTargetProxy != null)
            {
                Debug.Log("[MetaTutorialTrace] WorldTutorialTargetProxy found in scene", this);
                return true;
            }

            GameObject proxyObject = new("WorldTutorialTargetProxy", typeof(RectTransform));
            RectTransform proxyRect = proxyObject.GetComponent<RectTransform>();
            proxyRect.SetParent(proxyParent, false);
            proxyRect.anchorMin = Vector2.zero;
            proxyRect.anchorMax = Vector2.one;
            proxyRect.pivot = new Vector2(0.5f, 0.5f);
            proxyRect.anchoredPosition = Vector2.zero;
            proxyRect.sizeDelta = Vector2.zero;
            proxyRect.localScale = Vector3.one;
            MessageTutorialTrace.LogHide(
                "MetaPlanetTutorialController.EnsureWorldTargetProxy.CreateInactiveProxy",
                proxyObject,
                $"parent={MessageTutorialTrace.GetTransformPath(proxyParent)}");
            proxyObject.SetActive(false);

            _worldTargetProxy = proxyObject.AddComponent<WorldTutorialTargetProxy>();

            if (_worldTargetProxy == null)
            {
                Debug.LogWarning("[MetaTutorialTrace] WorldTutorialTargetProxy creation failed: component was not created", this);
                Destroy(proxyObject);
                return false;
            }

            Debug.Log("[MetaTutorialTrace] WorldTutorialTargetProxy auto-created", this);
            return true;
        }

        private RectTransform GetWorldTargetProxyParent()
        {
            if (_interactionOverlay != null)
            {
                RectTransform overlayRect = _interactionOverlay.transform as RectTransform;

                if (overlayRect != null)
                    return overlayRect;
            }

            TutorialHighlightSystem highlightSystem = GetTutorialHighlightSystem();

            if (highlightSystem != null)
            {
                RectTransform highlightRect = highlightSystem.transform as RectTransform;

                if (highlightRect != null)
                    return highlightRect;
            }

            Canvas canvas = _interactionOverlay != null
                ? _interactionOverlay.GetComponentInParent<Canvas>()
                : null;

            if (canvas == null && highlightSystem != null)
                canvas = highlightSystem.GetComponentInParent<Canvas>();

            return canvas != null ? canvas.transform as RectTransform : null;
        }

        private void ClearStep5BoxHighlightAndOverlay()
        {
            Debug.Log("[MetaTutorialTrace] Step5 clearing box highlight", this);
            DisableStep5WorldProxy();

            if (_interactionOverlay != null)
                _interactionOverlay.ClearTarget();
            else
                Debug.Log("[MetaTutorialTrace] Step5 interaction overlay target cleared: overlay missing", this);
        }

        private void DisableStep5WorldProxy()
        {
            StopStep5BoxContourGlow();

            TutorialHighlightSystem highlightSystem = GetTutorialHighlightSystem();

            if (highlightSystem != null)
                highlightSystem.Hide();

            if (_worldTargetProxy != null)
                _worldTargetProxy.ClearTarget();

            _step5BoxProxyTarget = null;
            Debug.Log("[MetaTutorialTrace] Step5 box highlight cleared", this);
            Debug.Log("[MetaTutorialTrace] Step5 box highlight disabled", this);
            Debug.Log("[MetaTutorialTrace] Step5 world proxy disabled", this);
        }

        private void StopStep5BoxTargetSearch()
        {
            if (_step5BoxTargetCoroutine == null)
                return;

            StopCoroutine(_step5BoxTargetCoroutine);
            _step5BoxTargetCoroutine = null;
        }

        private void StopStep5VariantPurchaseCoroutine()
        {
            if (_step5VariantPurchaseCoroutine == null)
                return;

            StopCoroutine(_step5VariantPurchaseCoroutine);
            _step5VariantPurchaseCoroutine = null;
        }

        private void StopStep6ShopOpenCoroutine()
        {
            if (_step6ShopOpenCoroutine == null)
                return;

            StopCoroutine(_step6ShopOpenCoroutine);
            _step6ShopOpenCoroutine = null;
        }

        private void StartDeferredAdvanceToStage(int stage)
        {
            StopDeferredStageAdvanceCoroutine();
            _deferredStageAdvanceCoroutine = StartCoroutine(DeferredAdvanceToStageRoutine(stage));
        }

        private IEnumerator DeferredAdvanceToStageRoutine(int stage)
        {
            yield return null;

            if (_tutorialManager != null && _tutorialManager.IsRunning)
                Debug.Log($"[MetaTutorialTrace] waiting for TutorialManager.IsRunning false before stage={stage}", this);

            while (_tutorialManager != null && _tutorialManager.IsRunning)
                yield return null;

            _deferredStageAdvanceCoroutine = null;
            AdvanceToStage(stage);
        }

        private void StopDeferredStageAdvanceCoroutine()
        {
            if (_deferredStageAdvanceCoroutine == null)
                return;

            StopCoroutine(_deferredStageAdvanceCoroutine);
            _deferredStageAdvanceCoroutine = null;
        }

        private void StopStep7AddUpgradeObjectCoroutine()
        {
            if (_step7AddUpgradeObjectCoroutine == null)
                return;

            StopCoroutine(_step7AddUpgradeObjectCoroutine);
            _step7AddUpgradeObjectCoroutine = null;
        }

        private static string GetScreenRectDescription(GameObject target)
        {
            if (target == null)
                return "null";

            RectTransform rectTransform = target.GetComponent<RectTransform>();

            if (rectTransform == null)
                return "no RectTransform";

            Canvas canvas = rectTransform.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

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

        private void ConfigureTyping()
        {
            _tutorialManager.SetTypingSettings(true, _typingSpeed, true);
            _dialogManager.SetTypingSettings(true, _typingSpeed, true);
        }

        private bool HasRequiredSystems()
        {
            return _tutorialManager != null &&
                   _dialogManager != null &&
                   _interactionOverlay != null &&
                   _metaGameRules != null &&
                   _gameManager != null &&
                   _metaGameRules.IsPrepared;
        }

        private bool HasValidTargetData()
        {
            return TryGetHouseItem(out MetaVariantItemController houseItem) &&
                   houseItem.EnsureVariantStateInitialized() &&
                   houseItem.Variants != null &&
                   houseItem.state != null &&
                   houseItem.state.Count == houseItem.Variants.Count &&
                   _houseUpgradeController != null &&
                   _houseUpgradeController.data != null &&
                   _houseUpgradeController.data.Upgrades != null &&
                   _houseUpgradeController.data.Upgrades.Count > 0;
        }

        private bool TryGetHouseItem(out MetaVariantItemController houseItem)
        {
            houseItem = null;

            if (_houseController == null ||
                _houseController.items == null ||
                _houseController.items.Count == 0)
            {
                return false;
            }

            houseItem = _houseController.items[0];
            return houseItem != null;
        }

        private bool IsHousePurchased()
        {
            if (!TryGetHouseItem(out MetaVariantItemController houseItem) || houseItem.state == null)
                return false;

            for (int i = 0; i < houseItem.state.Count; i++)
            {
                if (houseItem.state[i] != null && houseItem.state[i].isBought)
                    return true;
            }

            return false;
        }

        private bool IsUpgradePurchasedOrMaxed()
        {
            if (_houseUpgradeController == null)
                return false;

            return _houseUpgradeController.currentLevel > 0 ||
                   !_houseUpgradeController.TryGetNextUpgradeCost(out _);
        }

        private bool TryGetHousePurchaseData(
            out MetaVariantItemController houseItem,
            out int variantIndex)
        {
            houseItem = null;
            variantIndex = -1;

            if (!TryGetHouseItem(out houseItem) ||
                houseItem.Variants == null ||
                houseItem.state == null)
            {
                return false;
            }

            int count = Math.Min(houseItem.Variants.Count, houseItem.state.Count);

            for (int i = 0; i < count; i++)
            {
                if (houseItem.state[i] == null || houseItem.state[i].isBought)
                    continue;

                variantIndex = i;
                return true;
            }

            return false;
        }

        private bool TryPrepareHousePurchaseTarget(string source, bool allowOpenPanel)
        {
            if (_houseController == null || _houseController._variantButtonList == null)
                return false;

            if (!TryGetHousePurchaseData(out MetaVariantItemController houseItem, out int variantIndex))
                return false;

            if (IsHouseVariantPanelOpen() &&
                TryFindHousePurchaseButton(houseItem, variantIndex, out _))
            {
                Debug.Log("[MetaTutorialTrace] Step5 PanelVariant opened: already open", this);
                return true;
            }

            if (!allowOpenPanel)
            {
                Debug.LogWarning($"[MetaTutorialTrace] Step5 OpenVariantPanel blocked for source: {source}", this);
                return false;
            }

            if (IsHouseVariantPanelOpen())
                _houseController.CloseVariantPanel();

            Debug.Log($"[MetaTutorialTrace] Step5 OpenVariantPanel requested by: {source}", this);
            _houseController.OpenVariantPanel(houseItem);
            Debug.Log("[MetaTutorialTrace] Step5 PanelVariant opened", this);
            SetConversationViewsTopPlacement(_metaUIController != null && _metaUIController.isDisplayed);
            Canvas.ForceUpdateCanvases();

            return true;
        }

        private bool TryFindPreparedHousePurchaseButton(out GameObject houseBuyButton)
        {
            houseBuyButton = null;

            if (_targetHouseItem != null &&
                _targetHouseVariantIndex >= 0 &&
                TryFindHousePurchaseButton(_targetHouseItem, _targetHouseVariantIndex, out houseBuyButton))
            {
                return true;
            }

            return TryGetHousePurchaseData(out MetaVariantItemController houseItem, out int variantIndex) &&
                   TryFindHousePurchaseButton(houseItem, variantIndex, out houseBuyButton);
        }

        private bool TryFindHousePurchaseButton(
            MetaVariantItemController houseItem,
            int variantIndex,
            out GameObject houseBuyButton)
        {
            houseBuyButton = null;

            if (_houseController == null || _houseController._variantButtonList == null)
                return false;

            for (int i = 0; i < _houseController._variantButtonList.Count; i++)
            {
                VariantButtonUI button = _houseController._variantButtonList[i];

                if (button == null || button.Index != variantIndex)
                    continue;

                _targetHouseItem = houseItem;
                _targetHouseVariantIndex = variantIndex;
                houseBuyButton = button.BuyButtonGameObject;
                Debug.Log("[MetaTutorialTrace] Step5 house UI target found", this);
                return houseBuyButton != null;
            }

            return false;
        }

        private void Subscribe()
        {
            if (_isSubscribed ||
                _tutorialManager == null ||
                _dialogManager == null ||
                _houseUpgradeController == null ||
                !TryGetHouseItem(out MetaVariantItemController houseItem))
            {
                return;
            }

            _dialogManager.OnDialogCompleted += HandleDialogCompleted;
            _tutorialManager.OnStepCompleted += HandleStepCompleted;
            _tutorialManager.OnTutorialCompleted += HandleTutorialCompleted;
            _houseUpgradeController.OnUpgradePurchased += HandleHouseUpgradePurchased;
            houseItem.OnVariantPurchased += HandleHouseVariantPurchased;
            _isSubscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_isSubscribed)
                return;

            if (_dialogManager != null)
                _dialogManager.OnDialogCompleted -= HandleDialogCompleted;

            if (_tutorialManager != null)
            {
                _tutorialManager.OnStepCompleted -= HandleStepCompleted;
                _tutorialManager.OnTutorialCompleted -= HandleTutorialCompleted;
            }

            if (_houseUpgradeController != null)
                _houseUpgradeController.OnUpgradePurchased -= HandleHouseUpgradePurchased;

            if (TryGetHouseItem(out MetaVariantItemController houseItem))
                houseItem.OnVariantPurchased -= HandleHouseVariantPurchased;

            _isSubscribed = false;
        }
    }
}




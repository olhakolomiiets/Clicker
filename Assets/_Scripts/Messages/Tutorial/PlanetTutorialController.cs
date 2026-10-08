using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.Dialogs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetBuilder.Messages.Tutorial
{
    /// <summary>
    /// Planet 1 onboarding tutorial. Drives a six stage flow (first visit, open Creation tab,
    /// earn coins, buy another SmallTree, buy a BigTree, buy a SmallTree manager) through the
    /// existing message/tutorial infrastructure (<see cref="TutorialManager"/>,
    /// <see cref="DialogManager"/>, <see cref="TutorialInteractionOverlay"/>,
    /// <see cref="TutorialHighlightSystem"/>). Progress is persisted with its own PlayerPrefs
    /// keys, separate from the Meta Planet tutorial.
    ///
    /// Mirrors the structure of <see cref="MetaPlanetTutorialController"/> but relies on the
    /// standard highlight + arrow + interaction cutout instead of custom DOTween glow.
    /// </summary>
    public class PlanetTutorialController : MonoBehaviour
    {
        // ---- Persistence keys (separate from Meta Planet Tutorial) ----
        private const string CompletedKey = "PlanetTutorialCompleted";
        private const string CurrentStageKey = "PlanetTutorialCurrentStage";
        private const string CurrentStepIdKey = "PlanetTutorialCurrentStepId";
        private const string FirstVisitShownKey = "PlanetTutorialFirstVisitShown";
        private const string CoinsEarnedFromSmallTreeKey = "PlanetTutorialCoinsEarnedFromSmallTree";
        private const string SmallTreeBoughtKey = "PlanetTutorialSmallTreeBought";
        private const string BigTreeBoughtKey = "PlanetTutorialBigTreeBought";
        private const string SmallTreeManagerBoughtKey = "PlanetTutorialSmallTreeManagerBought";
        private const string EarlyManagerDialogShownKey = "PlanetTutorialEarlyManagerDialogShown";
        private const string TutorialActionsCompletedKey = "PlanetTutorialActionsCompleted";

        private const int SmallTreeIndex = 0;
        private const int BigTreeIndex = 1;
        private const float DialogShowTimeout = 1f;
        private const float ManagerTargetRetryInterval = 0.25f;
        private const float ManagerTargetRetryDuration = 10f;
        private const string RuntimeGlowName = "PlanetTutorialRuntimeGlow";
        private const string RuntimeGlowLayersName = "GlowLayers";

        // TutorialManager step ids (also used as current-step guards in the event handlers).
        private const string StepIdOpenCreation = "Planet.OpenCreationTab";
        private const string StepIdEarnCoins = "Planet.EarnCoinsSmallTree";
        private const string StepIdBuySmallTree = "Planet.BuySmallTree";
        private const string StepIdBuyBigTree = "Planet.BuyBigTree";
        private const string StepIdOpenManager = "Planet.OpenSmallTreeManager";
        private const string StepIdBuyManager = "Planet.BuySmallTreeManager";

        // Hardcoded RU strings. Localization is intentionally not introduced yet to match the
        // existing tutorials' content style and to avoid touching localization assets.
        private const string DialogWelcome = "Добро пожаловать на мою первую планету!";
        private const string DialogOpenCreation = "Сначала посмотрим на объекты.";
        private const string HintEarnCoins = "Для начала заработаем первые монеты.";
        private const string ToastEarnedFirst = "Ты заработал первые деньги.";
        private const string HintBuySmallTree = "Для начала разместим новый объект из этой категории.";
        private const string DialogSmallTreeBought = "Отлично. Теперь объект появился на станции.";
        private const string HintBuyBigTree = "Теперь купим еще один тип объектов.\nТак планета будет выглядеть гораздо лучше.";
        private const string DialogBigTreeBought = "Отлично. Теперь на планете есть деревья.";
        private const string HintBuyManager = "Теперь купим автоматизацию заработка.";
        private const string DialogManagerBought = "Отлично. Теперь планета будет развиваться гораздо быстрее.";
        private const string DialogManagerEarlyOpen = "Менеджер заставит эти объекты работать автоматически.";
        private const string DialogManagerNotEnoughMoney = "Но монет пока маловато. Придётся поработать активнее.";
        private const string HintEarnMore = "Заработай немного больше монет, чтобы продолжить.";

        public enum PlanetTutorialStage
        {
            FirstVisit = 0,
            OpenCreationTab = 1,
            EarnCoinsSmallTree = 2,
            BuySmallTree = 3,
            BuyBigTree = 4,
            BuySmallTreeManager = 5
        }

        [Header("Tutorial")]
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private DialogManager _dialogManager;
        [SerializeField] private TutorialInteractionOverlay _interactionOverlay;
        [SerializeField] private TutorialHighlightSystem _highlightSystem;
        [SerializeField] private bool _startAutomatically = true;

        [Header("Planet")]
        [SerializeField] private UIController _uiController;
        [SerializeField] private GameRules _gameRules;
        [SerializeField] private GameUI _gameUI;
        [SerializeField] private GameManager _gameManager; // reserved for future save hooks; not required.

        [Header("Characters")]
        [SerializeField] private string _contractorSpeakerId = "1";

        [Header("Typing")]
        [Min(0f)]
        [SerializeField] private float _typingSpeed = 30f;

        private PlanetTutorialStage _currentStage;
        private bool _isRunning;
        private bool _isSubscribed;
        private bool _ownsTutorialSession;

        private readonly Dictionary<string, Action> _dialogCallbacks = new();
        private readonly HashSet<string> _nonBlockingDialogIds = new();

        private Action _tutorialStepCompleted;
        private bool _isEarnPhaseActive;
        private double _earnTargetMoney;
        private bool _smallTreeFirstEarnShown;
        private bool _isWaitingForManagerMoney;
        private bool _isEarlyManagerDialogActive;

        private Coroutine _initializationCoroutine;
        private Coroutine _dialogRetryCoroutine;
        private Coroutine _stepCompletedCoroutine;
        private Coroutine _deferredStageAdvanceCoroutine;
        private Coroutine _managerTargetRetryCoroutine;
        private bool _isStartingManagerTutorialFlow;

        private readonly Vector3[] _targetWorldCorners = new Vector3[4];
        private RectTransform _polishTarget;
        private Vector3 _polishOriginalScale = Vector3.one;
        private GameObject _runtimeGlow;
        private Sequence _targetPulse;
        private Sequence _glowPulse;

        public static bool ShouldStartTutorial() => PlayerPrefs.GetInt(CompletedKey, 0) != 1;

        private void Awake()
        {
            if (_tutorialManager != null)
                _tutorialManager.SetStartAutomatically(false);
        }

        private void OnEnable()
        {
            // Subscribe() is deferred until references are resolved in InitializeWhenReady(),
            // because the Messages/Tutorial managers persist from PlanetMeta and are resolved
            // at runtime (they are not present in the PlanetLevel1 scene file).
            _initializationCoroutine = StartCoroutine(InitializeWhenReady());
        }

        private void OnDisable()
        {
            if (_initializationCoroutine != null)
            {
                StopCoroutine(_initializationCoroutine);
                _initializationCoroutine = null;
            }

            StopDialogRetry();
            StopStepCompletedCallback();
            StopDeferredStageAdvanceCoroutine();
            StopManagerTargetRetry();
            StopTargetPolish();

            if (_ownsTutorialSession)
            {
                HideBlocker();

                if (_tutorialManager != null)
                    _tutorialManager.CancelTutorial();

                _ownsTutorialSession = false;
            }

            _isRunning = false;
            _isEarnPhaseActive = false;
            _isWaitingForManagerMoney = false;
            _isEarlyManagerDialogActive = false;
            SetConversationViewsTopPlacement(false, null, "controller disabled");
            Unsubscribe();
        }

        private void OnValidate()
        {
            if (_tutorialManager == null)
                Debug.LogWarning("PlanetTutorialController requires a TutorialManager.", this);

            if (_dialogManager == null)
                Debug.LogWarning("PlanetTutorialController requires a DialogManager.", this);

            if (_interactionOverlay == null)
                Debug.LogWarning("PlanetTutorialController requires a TutorialInteractionOverlay.", this);

            if (_uiController == null)
                Debug.LogWarning("PlanetTutorialController requires a UIController.", this);

            if (_gameRules == null)
                Debug.LogWarning("PlanetTutorialController requires a GameRules.", this);

            if (_gameUI == null)
                Debug.LogWarning("PlanetTutorialController requires a GameUI.", this);
        }

        public void Initialize()
        {
            _currentStage = LoadStage();
        }

        private IEnumerator InitializeWhenReady()
        {
            if (IsCompleted())
            {
                _initializationCoroutine = null;
                yield break;
            }

            // Wait until references are wired and GameRules has prepared the active planet data
            // (Start() ordering between this controller and StandardPlanetMode is undefined).
            float waited = 0f;

            while (waited < 10f)
            {
                ResolveReferences();

                if (HasRequiredReferences() && (_gameRules == null || _gameRules.IsPrepared))
                    break;

                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            ResolveReferences();
            _initializationCoroutine = null;

            if (!HasRequiredReferences())
            {
                Debug.LogWarning("[PlanetTutorialTrace] run skipped: required references are missing", this);
                yield break;
            }

            if (_gameRules != null && !_gameRules.IsPrepared)
            {
                Debug.LogWarning("[PlanetTutorialTrace] run skipped: GameRules not prepared after wait", this);
                yield break;
            }

            Debug.Log(
                "[PlanetTutorialTrace] references resolved " +
                $"tutorialManager={GetManagerName(_tutorialManager)} " +
                $"dialogManager={GetComponentName(_dialogManager)} " +
                $"interactionOverlay={GetComponentName(_interactionOverlay)} " +
                $"uiController={GetComponentName(_uiController)} " +
                $"gameRules={GetComponentName(_gameRules)} " +
                $"gameUI={GetComponentName(_gameUI)}",
                this);

            Subscribe();
            ConfigureTyping();

            if (_startAutomatically)
                RunSavedStage();
        }

        public void RunSavedStage()
        {
            Initialize();

            if (IsCompleted())
            {
                Debug.Log("[PlanetTutorialTrace] run skipped: tutorial is completed", this);
                return;
            }

            if (!HasRequiredReferences())
            {
                Debug.LogWarning("[PlanetTutorialTrace] run skipped: required references are missing", this);
                return;
            }

            _isRunning = true;
            _ownsTutorialSession = true;
            _currentStage = LoadStage();
            Debug.Log($"[PlanetTutorialTrace] run stage={_currentStage}", this);
            StartStage(_currentStage);
        }

        public void AdvanceToStage(PlanetTutorialStage stage)
        {
            if (IsCompleted())
                return;

            _currentStage = stage;
            SaveStage(stage);
            Debug.Log($"[PlanetTutorialTrace] advancing to stage={stage}", this);
            StartStage(stage);
        }

        public void CompleteTutorial()
        {
            PlayerPrefs.SetInt(CompletedKey, 1);
            PlayerPrefs.DeleteKey(CurrentStepIdKey);
            PlayerPrefs.Save();

            _isRunning = false;
            _isEarnPhaseActive = false;
            _isWaitingForManagerMoney = false;
            _isEarlyManagerDialogActive = false;
            StopTargetPolish();

            if (_ownsTutorialSession && _tutorialManager != null)
                _tutorialManager.CancelTutorial();

            if (_ownsTutorialSession)
                HideBlocker();

            _ownsTutorialSession = false;
            SetConversationViewsTopPlacement(false, null, "tutorial completed");

            Debug.Log("[PlanetTutorialTrace] tutorial completed", this);
        }

        private void StartStage(PlanetTutorialStage stage)
        {
            StopTargetPolish();
            _isRunning = true;
            _ownsTutorialSession = true;
            SaveStage(stage);
            _currentStage = stage;

            switch (stage)
            {
                case PlanetTutorialStage.FirstVisit:
                    StepFirstVisit();
                    break;
                case PlanetTutorialStage.OpenCreationTab:
                    StepOpenCreationTab();
                    break;
                case PlanetTutorialStage.EarnCoinsSmallTree:
                    StepEarnCoinsSmallTree();
                    break;
                case PlanetTutorialStage.BuySmallTree:
                    StepBuySmallTree();
                    break;
                case PlanetTutorialStage.BuyBigTree:
                    StepBuyBigTree();
                    break;
                case PlanetTutorialStage.BuySmallTreeManager:
                    StepBuySmallTreeManager();
                    break;
                default:
                    Debug.LogWarning($"[PlanetTutorialTrace] unknown stage={stage}", this);
                    break;
            }
        }

        // ================================================================
        // STAGE 1: First visit
        // ================================================================
        private void StepFirstVisit()
        {
            if (PlayerPrefs.GetInt(FirstVisitShownKey, 0) == 1)
            {
                AdvanceToStage(PlanetTutorialStage.OpenCreationTab);
                return;
            }

            ShowBlockingDialog(
                "PlanetTutorial.FirstVisit",
                new[] { Line(DialogWelcome, CharacterMood.Happy) },
                () =>
                {
                    SaveFlag(FirstVisitShownKey);
                    AdvanceToStage(PlanetTutorialStage.OpenCreationTab);
                });
        }

        // ================================================================
        // STAGE 2: Open the Creation tab
        // ================================================================
        private void StepOpenCreationTab()
        {
            if (_uiController != null && _uiController.isDisplayed)
            {
                AdvanceToStage(PlanetTutorialStage.EarnCoinsSmallTree);
                return;
            }

            GameObject target = _uiController != null ? _uiController.ToggleButtonTarget : null;

            ShowBlockingDialog(
                "PlanetTutorial.OpenCreationTab",
                new[] { Line(DialogOpenCreation, CharacterMood.Thinking) },
                () =>
                {
                    if (target == null)
                    {
                        Debug.LogWarning("[PlanetTutorialTrace] OpenCreationTab: ToggleButton target missing; opening Creation tab programmatically", this);
                        EnsureShopOpenOnCreationTab();
                        AdvanceToStage(PlanetTutorialStage.EarnCoinsSmallTree);
                        return;
                    }

                    _tutorialStepCompleted = HandleCreationTabOpened;
                    StartHighlightStep(StepIdOpenCreation, DialogOpenCreation, target, CharacterMood.Thinking);
                });
        }

        private void HandleCreationTabOpened()
        {
            Debug.Log("[PlanetTutorialTrace] Creation tab opened", this);
            EnsureShopOpenOnCreationTab();
            AdvanceToStage(PlanetTutorialStage.EarnCoinsSmallTree);
        }

        // ================================================================
        // STAGE 3: Earn coins from the SmallTree Creation object
        // ================================================================
        private void StepEarnCoinsSmallTree()
        {
            EnsureShopOpenOnCreationTab();
            _smallTreeFirstEarnShown = PlayerPrefs.GetInt(CoinsEarnedFromSmallTreeKey, 0) == 1;

            double cost = GetSmallTreeUpgradeCost();

            if (cost > 0d && GetCurrentMoney() >= cost)
            {
                Debug.Log("[PlanetTutorialTrace] EarnCoinsSmallTree skipped: already enough money", this);
                AdvanceToStage(PlanetTutorialStage.BuySmallTree);
                return;
            }

            double target = cost > 0d ? cost : 1d;
            StartEarnPhase(target, HintEarnCoins, () => AdvanceToStage(PlanetTutorialStage.BuySmallTree));
        }

        // ================================================================
        // STAGE 4: Buy another SmallTree
        // ================================================================
        private void StepBuySmallTree()
        {
            EnsureShopOpenOnCreationTab();

            if (PlayerPrefs.GetInt(SmallTreeBoughtKey, 0) == 1 ||
                (_gameRules != null && _gameRules.GetItemCount(SmallTreeIndex) >= 2))
            {
                Debug.Log("[PlanetTutorialTrace] BuySmallTree skipped: another SmallTree already bought", this);
                AdvanceToStage(PlanetTutorialStage.BuyBigTree);
                return;
            }

            GameObject target = _gameUI != null ? _gameUI.GetCreationBuyButtonTarget(SmallTreeIndex) : null;

            if (!StartBuyStep(StepIdBuySmallTree, HintBuySmallTree, target, SmallTreeIndex))
                return;

            // Completed via OnItemUpgradedConfirmed event.
            _tutorialStepCompleted = null;
        }

        private void HandleSmallTreeBought()
        {
            Debug.Log("[PlanetTutorialTrace] another SmallTree bought", this);
            CompleteManualStep();

            ShowBlockingDialog(
                "PlanetTutorial.BuySmallTree.Done",
                new[] { Line(DialogSmallTreeBought, CharacterMood.Happy) },
                () => EarnUntilMoneyOrAdvance(GetBigTreeFirstCost(), PlanetTutorialStage.BuyBigTree));
        }

        // ================================================================
        // STAGE 5: Buy a BigTree (a new Creation type)
        // ================================================================
        private void StepBuyBigTree()
        {
            EnsureShopOpenOnCreationTab();

            if (PlayerPrefs.GetInt(BigTreeBoughtKey, 0) == 1 ||
                (_gameRules != null && _gameRules.GetItemCount(BigTreeIndex) >= 1))
            {
                Debug.Log("[PlanetTutorialTrace] BuyBigTree skipped: BigTree already owned", this);
                AdvanceToStage(PlanetTutorialStage.BuySmallTreeManager);
                return;
            }

            // A new (unowned) Creation type is acquired through the progress button's first click,
            // which routes to GameRules.PurchaseItemFirstTime -> OnItemFirstPurchaseConfirmed.
            GameObject target = _gameUI != null ? _gameUI.GetCreationProgressButtonTarget(BigTreeIndex) : null;

            if (!StartBuyStep(StepIdBuyBigTree, HintBuyBigTree, target, BigTreeIndex))
                return;

            // Completed via OnItemFirstPurchaseConfirmed event.
            _tutorialStepCompleted = null;
        }

        private void HandleBigTreeBought()
        {
            Debug.Log("[PlanetTutorialTrace] BigTree bought", this);
            CompleteManualStep();

            ShowBlockingDialog(
                "PlanetTutorial.BuyBigTree.Done",
                new[] { Line(DialogBigTreeBought, CharacterMood.Happy) },
                () => AdvanceToStage(PlanetTutorialStage.BuySmallTreeManager));
        }

        // ================================================================
        // STAGE 6: Buy the SmallTree automation manager
        // ================================================================
        private void StepBuySmallTreeManager()
        {
            if (PlayerPrefs.GetInt(SmallTreeManagerBoughtKey, 0) == 1 ||
                (_gameRules != null && _gameRules.IsManagerPurchased(SmallTreeIndex)))
            {
                _isWaitingForManagerMoney = false;
                EnsureShopOpenOnCreationTab();
                Debug.Log("[PlanetTutorialTrace] BuySmallTreeManager confirmed: showing final dialog", this);
                ShowManagerBoughtDialog();
                return;
            }

            double managerCost = GetSmallTreeManagerCost();

            if (managerCost > 0d && GetCurrentMoney() < managerCost)
            {
                StartPassiveManagerWait(managerCost);
                return;
            }

            StartManagerTutorialFlow();
        }

        private void StartPassiveManagerWait(double managerCost)
        {
            _isWaitingForManagerMoney = true;
            _tutorialStepCompleted = null;
            _isEarnPhaseActive = false;
            StopTargetPolish();

            if (_tutorialManager != null && _tutorialManager.IsRunning)
                _tutorialManager.CancelTutorial();

            HideBlocker();
            SetConversationViewsTopPlacement(false, null, "waiting for manager money");
            Debug.Log($"[PlanetTutorial] BuySmallTreeManager waiting for money: current={GetCurrentMoney()}, required={managerCost}", this);
        }

        private void StartManagerTutorialFlow()
        {
            if (_isStartingManagerTutorialFlow)
                return;

            _isStartingManagerTutorialFlow = true;
            StopManagerTargetRetry();
            _isWaitingForManagerMoney = false;
            _tutorialStepCompleted = null;

            try
            {
                GameObject buyTarget = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(SmallTreeIndex) : null;

                if (buyTarget != null && buyTarget.activeInHierarchy)
                {
                    ShowManagerBuyStep();
                    return;
                }

                EnsureShopOpenOnCreationTab();

                GameObject target = _gameUI != null ? _gameUI.GetManagerButtonTarget(SmallTreeIndex) : null;

                if (target == null || !target.activeInHierarchy)
                {
                    Debug.LogWarning("[PlanetTutorialTrace] BuySmallTreeManager: manager panel target missing/inactive; retrying", this);
                    StartManagerTargetRetry();
                    return;
                }

                _tutorialStepCompleted = ShowManagerBuyStep;
                StartHighlightStep(
                    StepIdOpenManager,
                    HintBuyManager,
                    target,
                    CharacterMood.Thinking,
                    TutorialCompletionCondition.TargetUIClicked);
            }
            finally
            {
                _isStartingManagerTutorialFlow = false;
            }
        }

        private void ShowManagerBuyStep()
        {
            GameObject target = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(SmallTreeIndex) : null;

            if (target == null || !target.activeInHierarchy)
            {
                Debug.LogWarning("[PlanetTutorialTrace] BuySmallTreeManager: manager buy target missing/inactive after opening panel; retrying", this);
                StartManagerTargetRetry();
                return;
            }

            _gameUI.SetManagerBuyButtonInteractable(SmallTreeIndex, true);

            _tutorialStepCompleted = null; // Completed via OnManagerPurchasedConfirmed event.
            StartHighlightStep(StepIdBuyManager, HintBuyManager, target, CharacterMood.Thinking);
        }

        private void StartManagerTargetRetry()
        {
            if (_managerTargetRetryCoroutine == null)
                _managerTargetRetryCoroutine = StartCoroutine(RetryManagerTargetRoutine());
        }

        private IEnumerator RetryManagerTargetRoutine()
        {
            float deadline = Time.unscaledTime + ManagerTargetRetryDuration;

            while (_isRunning &&
                   !_isWaitingForManagerMoney &&
                   _currentStage == PlanetTutorialStage.BuySmallTreeManager &&
                   Time.unscaledTime < deadline)
            {
                GameObject buyTarget = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(SmallTreeIndex) : null;

                if (buyTarget != null && buyTarget.activeInHierarchy)
                {
                    _managerTargetRetryCoroutine = null;
                    ShowManagerBuyStep();
                    yield break;
                }

                GameObject managerTarget = _gameUI != null ? _gameUI.GetManagerButtonTarget(SmallTreeIndex) : null;

                if (managerTarget != null && managerTarget.activeInHierarchy)
                {
                    _managerTargetRetryCoroutine = null;
                    _tutorialStepCompleted = ShowManagerBuyStep;
                    StartHighlightStep(
                        StepIdOpenManager,
                        HintBuyManager,
                        managerTarget,
                        CharacterMood.Thinking,
                        TutorialCompletionCondition.TargetUIClicked);
                    yield break;
                }

                yield return new WaitForSecondsRealtime(ManagerTargetRetryInterval);
            }

            _managerTargetRetryCoroutine = null;
            _tutorialStepCompleted = null;
            Debug.LogWarning("[PlanetTutorialTrace] BuySmallTreeManager: manager targets stayed unavailable; tutorial remains pending", this);
        }

        private void StopManagerTargetRetry()
        {
            if (_managerTargetRetryCoroutine == null)
                return;

            StopCoroutine(_managerTargetRetryCoroutine);
            _managerTargetRetryCoroutine = null;
        }

        private void HandleManagerBought()
        {
            Debug.Log("[PlanetTutorialTrace] SmallTree manager purchased", this);
            _isWaitingForManagerMoney = false;
            _tutorialStepCompleted = null;
            CompleteManualStep();

            ShowManagerBoughtDialog();
        }

        private void ShowManagerBoughtDialog()
        {
            ShowBlockingDialog(
                "PlanetTutorial.BuySmallTreeManager.Done",
                new[] { Line(DialogManagerBought, CharacterMood.Excited) },
                CompleteTutorial);
        }

        // ================================================================
        // Shared earn phase: highlight the SmallTree progress button and wait
        // until the player has earned at least <targetMoney> coins.
        // ================================================================
        private void StartEarnPhase(double targetMoney, string hint, Action onReached)
        {
            _earnTargetMoney = targetMoney;

            if (GetCurrentMoney() >= targetMoney)
            {
                onReached?.Invoke();
                return;
            }

            GameObject target = _gameUI != null ? _gameUI.GetCreationProgressButtonTarget(SmallTreeIndex) : null;
            GameObject visualTarget = _gameUI != null ? _gameUI.GetCreationItemVisualTarget(SmallTreeIndex) : null;

            if (target == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] earn phase: SmallTree progress button target missing; skipping earn phase", this);
                onReached?.Invoke();
                return;
            }

            if (_gameUI != null)
                _gameUI.SetCreationProgressButtonEnabled(SmallTreeIndex, true);

            _isEarnPhaseActive = true;
            _tutorialStepCompleted = onReached;
            StartHighlightStep(StepIdEarnCoins, hint, target, CharacterMood.Thinking, visualTarget: visualTarget);

            // Money may have already crossed the threshold between frames.
            if (_isEarnPhaseActive && GetCurrentMoney() >= _earnTargetMoney)
            {
                _isEarnPhaseActive = false;
                CompleteManualStep();
            }
        }

        private void EarnUntilMoneyOrAdvance(double targetMoney, PlanetTutorialStage next)
        {
            if (targetMoney <= 0d || GetCurrentMoney() >= targetMoney)
            {
                AdvanceToStage(next);
                return;
            }

            StartEarnPhase(targetMoney, HintEarnMore, () => AdvanceToStage(next));
        }

        // ================================================================
        // Buy step helper. Forces the target button interactable (tutorial
        // gates affordability before reaching a buy step, so this only
        // overcomes UI update timing and guarantees no click deadlock).
        // ================================================================
        private bool StartBuyStep(string stepId, string hint, GameObject target, int itemIndex)
        {
            if (target == null)
            {
                Debug.LogWarning($"[PlanetTutorialTrace] {stepId}: target missing; advancing to avoid deadlock", this);

                if (stepId == StepIdBuySmallTree)
                    AdvanceToStage(PlanetTutorialStage.BuyBigTree);
                else if (stepId == StepIdBuyBigTree)
                    AdvanceToStage(PlanetTutorialStage.BuySmallTreeManager);
                else
                    CompleteTutorial();

                return false;
            }

            if (_gameUI != null)
            {
                if (stepId == StepIdBuySmallTree)
                    _gameUI.SetCreationBuyButtonInteractable(itemIndex, true);
                else if (stepId == StepIdBuyBigTree)
                    _gameUI.SetCreationProgressButtonEnabled(itemIndex, true);
            }

            StartHighlightStep(stepId, hint, target, CharacterMood.Thinking);
            return true;
        }

        // ================================================================
        // Tutorial step plumbing
        // ================================================================
        private void StartHighlightStep(
            string stepId,
            string hint,
            GameObject target,
            CharacterMood mood,
            TutorialCompletionCondition completionCondition = TutorialCompletionCondition.Manual,
            GameObject visualTarget = null)
        {
            if (_tutorialManager == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] TutorialManager missing; cannot start step", this);
                return;
            }

            _ownsTutorialSession = true;
            HideBlocker();
            SetConversationViewsTopPlacement(ShouldUseTopPlacement(), stepId, "StartStep");

            GameObject polishTarget = visualTarget != null ? visualTarget : target;
            float pulseScale = visualTarget != null && visualTarget != target ? 1.05f : 1.08f;
            bool usesRuntimePolish = StartTargetPolish(polishTarget, pulseScale);

            TutorialStepData stepData = new()
            {
                StepId = stepId,
                MessageText = hint,
                TargetUI = target,
                CompletionCondition = completionCondition,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = mood,
                EnableHighlight = target != null && !usesRuntimePolish,
                EnablePulse = target != null && !usesRuntimePolish,
                ShowArrow = target != null && !usesRuntimePolish,
                DisableInteractionCutout = true
            };

            _tutorialManager.Configure(
                new List<TutorialStepData> { stepData },
                CurrentStepIdKey,
                $"{TutorialActionsCompletedKey}.{stepId}");

            _tutorialManager.RestartTutorial();
            Debug.Log($"[PlanetTutorialTrace] step started stepId={stepId} target={(target != null ? target.name : "null")}", this);
        }

        private void CompleteManualStep()
        {
            if (_tutorialManager != null && _tutorialManager.IsRunning)
                _tutorialManager.CompleteManualStep();
        }

        private void HandleStepCompleted(TutorialStepData step)
        {
            StopTargetPolish();
            Action completed = _tutorialStepCompleted;
            _tutorialStepCompleted = null;
            _stepCompletedCoroutine = StartCoroutine(InvokeStepCompletedNextFrame(completed));
        }

        private IEnumerator InvokeStepCompletedNextFrame(Action completed)
        {
            yield return null;
            _stepCompletedCoroutine = null;
            _isEarnPhaseActive = false;
            completed?.Invoke();
        }

        private void StopStepCompletedCallback()
        {
            if (_stepCompletedCoroutine == null)
                return;

            StopCoroutine(_stepCompletedCoroutine);
            _stepCompletedCoroutine = null;
        }

        private void HandleTutorialCompleted()
        {
            // TutorialManager finished its single configured step; this controller drives stage progression.
        }

        // ================================================================
        // Dialog plumbing
        // ================================================================
        private void ShowBlockingDialog(string dialogId, DialogLine[] lines, Action completed)
        {
            SetConversationViewsTopPlacement(ShouldUseTopPlacement(), dialogId, "ShowBlockingDialog");
            _dialogCallbacks[dialogId] = completed;
            RequestDialog(new DialogData { DialogId = dialogId, ListDialogLine = new List<DialogLine>(lines) });
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
            while (_isRunning)
            {
                MessageManager messageManager = MessageManager.Instance;
                bool dialogChannelFree = messageManager != null &&
                                         messageManager.GetCurrentMessage(MessageChannel.Dialog) == null;

                if (_dialogManager != null &&
                    !_dialogManager.IsRunning &&
                    dialogChannelFree &&
                    _dialogManager.StartDialog(dialog))
                {
                    string messageId = $"Dialog.{dialog.DialogId}.0";
                    float timeoutAt = Time.unscaledTime + DialogShowTimeout;

                    while (_isRunning &&
                           Time.unscaledTime < timeoutAt &&
                           messageManager.GetCurrentMessage(MessageChannel.Dialog)?.Id != messageId)
                    {
                        yield return null;
                    }

                    if (messageManager.GetCurrentMessage(MessageChannel.Dialog)?.Id == messageId)
                        ShowBlocker();
                    else
                        Debug.LogWarning($"[PlanetTutorialTrace] Blocking dialog '{dialog.DialogId}' did not become active; blocker kept hidden.", this);

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

            if (!_nonBlockingDialogIds.Remove(dialog.DialogId))
                HideBlocker();

            if (!_dialogCallbacks.TryGetValue(dialog.DialogId, out Action callback))
                return;

            _dialogCallbacks.Remove(dialog.DialogId);
            callback?.Invoke();
        }

        private void ShowToast(string text)
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null)
            {
                Debug.Log($"[PlanetTutorialTrace] {text}", this);
                return;
            }

            messageManager.ShowMessage(new MessageData
            {
                Id = "PlanetTutorial.Toast",
                Text = text,
                Type = MessageType.Toast,
                Channel = MessageChannel.Toast,
                Priority = 100,
                DisplayDuration = 1.5f,
                CanInterrupt = true,
                CanDisplayOverModal = true
            });
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

            if (useTopPlacement && _uiController != null && _uiController.isDisplayed)
                messageManager.SetShopOpen(false);

            messageManager.SetConversationViewsTopPlacement(useTopPlacement, stepId, reason);
        }

        private bool ShouldUseTopPlacement()
        {
            return _uiController != null && _uiController.isDisplayed;
        }

        private void EnsureShopOpenOnCreationTab()
        {
            if (_uiController == null)
                return;

            if (!_uiController.isDisplayed)
                _uiController.ToggleUpgradeStorePanel();

            _uiController.ShopsToggle(SmallTreeIndex);
        }

        // ================================================================
        // Gameplay event handlers
        // ================================================================
        private void HandleShopOpened()
        {
            TutorialStepData current = _tutorialManager != null ? _tutorialManager.CurrentStep : null;

            if (current == null || current.StepId != StepIdOpenCreation)
            {
                if (_isRunning &&
                    !_isWaitingForManagerMoney &&
                    !_isStartingManagerTutorialFlow &&
                    _managerTargetRetryCoroutine == null &&
                    _currentStage == PlanetTutorialStage.BuySmallTreeManager &&
                    (_tutorialManager == null || !_tutorialManager.IsRunning))
                {
                    StartManagerTutorialFlow();
                }

                return;
            }

            Debug.Log("[PlanetTutorialTrace] shop opened during OpenCreationTab step", this);
            CompleteManualStep();
        }

        private void HandleShopTabSelected(int index)
        {
            if (index == 0 &&
                _isRunning &&
                !_isWaitingForManagerMoney &&
                !_isStartingManagerTutorialFlow &&
                _managerTargetRetryCoroutine == null &&
                _currentStage == PlanetTutorialStage.BuySmallTreeManager &&
                (_tutorialManager == null || !_tutorialManager.IsRunning))
            {
                StartManagerTutorialFlow();
            }
        }

        private void HandleItemIncomeEarned(int index, double income)
        {
            if (income <= 0d)
                return;

            if (index == SmallTreeIndex && !_smallTreeFirstEarnShown)
            {
                _smallTreeFirstEarnShown = true;
                SaveFlag(CoinsEarnedFromSmallTreeKey);

                if (_gameUI != null)
                    _gameUI.PlayCoinsEarnedFeedback();

                ShowToast(ToastEarnedFirst);
                Debug.Log("[PlanetTutorialTrace] first coins earned from SmallTree", this);
            }

            if (index == SmallTreeIndex && _isEarnPhaseActive && GetCurrentMoney() >= _earnTargetMoney)
            {
                Debug.Log($"[PlanetTutorialTrace] earn target reached money={GetCurrentMoney()} target={_earnTargetMoney}", this);
                _isEarnPhaseActive = false;
                CompleteManualStep();
            }

            TryResumeManagerTutorial();
        }

        private void HandleGameDataUpdated()
        {
            TryResumeManagerTutorial();
        }

        private void TryResumeManagerTutorial()
        {
            if (!_isRunning ||
                !_isWaitingForManagerMoney ||
                _isEarlyManagerDialogActive ||
                _currentStage != PlanetTutorialStage.BuySmallTreeManager)
            {
                return;
            }

            double managerCost = GetSmallTreeManagerCost();

            if (managerCost > 0d && GetCurrentMoney() < managerCost)
                return;

            Debug.Log("[PlanetTutorial] Manager money requirement reached. Resuming tutorial.", this);
            StartManagerTutorialFlow();
        }

        private void HandleManagerPanelOpened(int index)
        {
            if (index == SmallTreeIndex &&
                _isRunning &&
                !_isWaitingForManagerMoney &&
                !_isStartingManagerTutorialFlow &&
                _managerTargetRetryCoroutine == null &&
                _currentStage == PlanetTutorialStage.BuySmallTreeManager &&
                (_tutorialManager == null || !_tutorialManager.IsRunning))
            {
                StartManagerTutorialFlow();
                return;
            }

            if (index != SmallTreeIndex ||
                !_isRunning ||
                !_isWaitingForManagerMoney ||
                _currentStage != PlanetTutorialStage.BuySmallTreeManager)
            {
                return;
            }

            double managerCost = GetSmallTreeManagerCost();

            if (managerCost <= 0d || GetCurrentMoney() >= managerCost)
            {
                TryResumeManagerTutorial();
                return;
            }

            if (_isEarlyManagerDialogActive || PlayerPrefs.GetInt(EarlyManagerDialogShownKey, 0) == 1)
                return;

            Debug.Log("[PlanetTutorial] Manager opened before enough money.", this);
            _isEarlyManagerDialogActive = true;
            ShowBlockingDialog(
                "PlanetTutorial.BuySmallTreeManager.Early",
                new[]
                {
                    Line(DialogManagerEarlyOpen, CharacterMood.Thinking),
                    Line(DialogManagerNotEnoughMoney, CharacterMood.Thinking)
                },
                () =>
                {
                    SaveFlag(EarlyManagerDialogShownKey);
                    _isEarlyManagerDialogActive = false;
                    TryResumeManagerTutorial();
                });
        }

        private void HandleItemFirstPurchaseConfirmed(int index)
        {
            if (index == BigTreeIndex)
                SaveFlag(BigTreeBoughtKey);

            TutorialStepData current = _tutorialManager != null ? _tutorialManager.CurrentStep : null;

            if (current != null && current.StepId == StepIdBuyBigTree && index == BigTreeIndex)
                HandleBigTreeBought();
        }

        private void HandleItemUpgradedConfirmed(int index)
        {
            if (index == SmallTreeIndex)
                SaveFlag(SmallTreeBoughtKey);

            TutorialStepData current = _tutorialManager != null ? _tutorialManager.CurrentStep : null;

            if (current != null && current.StepId == StepIdBuySmallTree && index == SmallTreeIndex)
                HandleSmallTreeBought();
        }

        private void HandleManagerPurchasedConfirmed(int index)
        {
            if (index != SmallTreeIndex)
                return;

            SaveFlag(SmallTreeManagerBoughtKey);

            if (_isRunning && _currentStage == PlanetTutorialStage.BuySmallTreeManager)
                HandleManagerBought();
        }

        // ================================================================
        // Subscribe / unsubscribe
        // ================================================================
        private void Subscribe()
        {
            if (_isSubscribed)
                return;

            if (_dialogManager != null)
                _dialogManager.OnDialogCompleted += HandleDialogCompleted;

            if (_tutorialManager != null)
            {
                _tutorialManager.OnStepCompleted += HandleStepCompleted;
                _tutorialManager.OnTutorialCompleted += HandleTutorialCompleted;
            }

            if (_uiController != null)
            {
                _uiController.OnShopOpened += HandleShopOpened;
                _uiController.OnShopTabSelected += HandleShopTabSelected;
            }

            if (_gameRules != null)
            {
                _gameRules.OnItemIncomeEarned += HandleItemIncomeEarned;
                _gameRules.OnDataUpdated += HandleGameDataUpdated;
                _gameRules.OnItemFirstPurchaseConfirmed += HandleItemFirstPurchaseConfirmed;
                _gameRules.OnItemUpgradedConfirmed += HandleItemUpgradedConfirmed;
                _gameRules.OnManagerPurchasedConfirmed += HandleManagerPurchasedConfirmed;
            }

            if (_gameUI != null)
                _gameUI.OnManagerPanelOpened += HandleManagerPanelOpened;

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

            if (_uiController != null)
            {
                _uiController.OnShopOpened -= HandleShopOpened;
                _uiController.OnShopTabSelected -= HandleShopTabSelected;
            }

            if (_gameRules != null)
            {
                _gameRules.OnItemIncomeEarned -= HandleItemIncomeEarned;
                _gameRules.OnDataUpdated -= HandleGameDataUpdated;
                _gameRules.OnItemFirstPurchaseConfirmed -= HandleItemFirstPurchaseConfirmed;
                _gameRules.OnItemUpgradedConfirmed -= HandleItemUpgradedConfirmed;
                _gameRules.OnManagerPurchasedConfirmed -= HandleManagerPurchasedConfirmed;
            }

            if (_gameUI != null)
                _gameUI.OnManagerPanelOpened -= HandleManagerPanelOpened;

            _isSubscribed = false;
        }

        private void ConfigureTyping()
        {
            if (_tutorialManager != null)
                _tutorialManager.SetTypingSettings(true, _typingSpeed, true);

            if (_dialogManager != null)
                _dialogManager.SetTypingSettings(true, _typingSpeed, true);
        }

        private bool StartTargetPolish(GameObject target, float pulseScale)
        {
            StopTargetPolish();

            _polishTarget = target != null ? target.GetComponent<RectTransform>() : null;

            if (_polishTarget == null)
                return false;

            _polishOriginalScale = _polishTarget.localScale;
            _runtimeGlow = CreateRuntimeGlow(_polishTarget);

            if (_runtimeGlow == null)
            {
                _polishTarget = null;
                _polishOriginalScale = Vector3.one;
                return false;
            }

            RefreshRuntimeGlowLayout();

            _targetPulse = DOTween.Sequence()
                .Append(_polishTarget.DOScale(_polishOriginalScale * pulseScale, 0.6f).SetEase(Ease.InOutSine))
                .Append(_polishTarget.DOScale(_polishOriginalScale, 0.6f).SetEase(Ease.InOutSine))
                .SetLoops(-1)
                .SetUpdate(true)
                .OnUpdate(RefreshRuntimeGlowLayout);

            if (_runtimeGlow != null)
            {
                RectTransform glowRect = _runtimeGlow.transform.Find(RuntimeGlowLayersName) as RectTransform;
                CanvasGroup canvasGroup = glowRect != null ? glowRect.GetComponent<CanvasGroup>() : null;

                if (glowRect != null && canvasGroup != null)
                {
                    canvasGroup.alpha = 0.42f;
                    _glowPulse = DOTween.Sequence()
                        .Append(glowRect.DOScale(Vector3.one * 1.06f, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup.DOFade(0.78f, 0.6f).SetEase(Ease.InOutSine))
                        .Append(glowRect.DOScale(Vector3.one, 0.6f).SetEase(Ease.InOutSine))
                        .Join(canvasGroup.DOFade(0.42f, 0.6f).SetEase(Ease.InOutSine))
                        .SetLoops(-1)
                        .SetUpdate(true)
                        .OnUpdate(RefreshRuntimeGlowLayout);
                }
            }

            return true;
        }

        private void StopTargetPolish()
        {
            _targetPulse?.Kill(false);
            _targetPulse = null;
            _glowPulse?.Kill(false);
            _glowPulse = null;

            if (_polishTarget != null)
                _polishTarget.localScale = _polishOriginalScale;

            if (_runtimeGlow != null)
                Destroy(_runtimeGlow);

            _polishTarget = null;
            _runtimeGlow = null;
            _polishOriginalScale = Vector3.one;
        }

        private GameObject CreateRuntimeGlow(RectTransform target)
        {
            RectTransform overlayRoot = _interactionOverlay != null
                ? _interactionOverlay.transform as RectTransform
                : null;
            Image sourceImage = GetTargetImage(target);

            if (overlayRoot == null || sourceImage == null || sourceImage.sprite == null)
                return null;

            Transform existingGlow = overlayRoot.Find(RuntimeGlowName);

            if (existingGlow != null)
                Destroy(existingGlow.gameObject);

            GameObject glow = new(RuntimeGlowName, typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
            RectTransform glowRect = glow.GetComponent<RectTransform>();
            CanvasGroup visualGroup = glow.GetComponent<CanvasGroup>();
            LayoutElement layoutElement = glow.GetComponent<LayoutElement>();

            glowRect.SetParent(overlayRoot, false);
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.localRotation = Quaternion.identity;
            glowRect.localScale = Vector3.one;
            glowRect.SetAsLastSibling();
            visualGroup.alpha = 1f;
            visualGroup.interactable = false;
            visualGroup.blocksRaycasts = false;
            visualGroup.ignoreParentGroups = true;
            layoutElement.ignoreLayout = true;

            GameObject glowLayers = new(RuntimeGlowLayersName, typeof(RectTransform), typeof(CanvasGroup));
            RectTransform glowLayersRect = glowLayers.GetComponent<RectTransform>();
            CanvasGroup glowGroup = glowLayers.GetComponent<CanvasGroup>();
            StretchToParent(glowLayersRect, glowRect);
            glowGroup.alpha = 0f;
            glowGroup.interactable = false;
            glowGroup.blocksRaycasts = false;

            AddGlowLayer(glowLayersRect, "OuterGlow", sourceImage, new Color(1f, 0.72f, 0.24f, 0.18f), 1.22f);
            AddGlowLayer(glowLayersRect, "InnerGlow", sourceImage, new Color(1f, 0.82f, 0.36f, 0.38f), 1.08f);
            AddTargetVisualCopies(glowRect, target);
            return glow;
        }

        private void RefreshRuntimeGlowLayout()
        {
            if (_polishTarget == null || _runtimeGlow == null || _interactionOverlay == null)
                return;

            RectTransform glowRect = _runtimeGlow.transform as RectTransform;
            RectTransform overlayRoot = _interactionOverlay.transform as RectTransform;
            Canvas targetCanvas = _polishTarget.GetComponentInParent<Canvas>();
            Canvas overlayCanvas = overlayRoot != null ? overlayRoot.GetComponentInParent<Canvas>() : null;

            if (glowRect == null || overlayRoot == null || targetCanvas == null || overlayCanvas == null)
                return;

            Camera targetCamera = targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? targetCanvas.worldCamera : null;
            Camera overlayCamera = overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? overlayCanvas.worldCamera : null;
            _polishTarget.GetWorldCorners(_targetWorldCorners);

            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);

            for (int i = 0; i < _targetWorldCorners.Length; i++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, _targetWorldCorners[i]);

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        overlayRoot,
                        screenPoint,
                        overlayCamera,
                        out Vector2 localPoint))
                {
                    return;
                }

                min = Vector2.Min(min, localPoint);
                max = Vector2.Max(max, localPoint);
            }

            glowRect.anchoredPosition = (min + max) * 0.5f;
            glowRect.sizeDelta = max - min;
            glowRect.SetAsLastSibling();
        }

        private static Image GetTargetImage(RectTransform target)
        {
            Button button = target != null ? target.GetComponent<Button>() : null;

            if (button != null && button.targetGraphic is Image buttonImage && buttonImage.sprite != null)
                return buttonImage;

            Image image = target != null ? target.GetComponent<Image>() : null;

            if (image != null && image.sprite != null)
                return image;

            Image[] childImages = target != null ? target.GetComponentsInChildren<Image>(true) : null;

            if (childImages == null)
                return null;

            for (int i = 0; i < childImages.Length; i++)
            {
                if (childImages[i] != null && childImages[i].sprite != null)
                    return childImages[i];
            }

            return null;
        }

        private static void AddGlowLayer(RectTransform parent, string name, Image source, Color color, float scale)
        {
            GameObject layer = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = layer.GetComponent<RectTransform>();
            Image image = layer.GetComponent<Image>();

            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one * scale;

            image.sprite = source.sprite;
            image.type = source.type;
            image.preserveAspect = source.preserveAspect;
            image.fillCenter = source.fillCenter;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void AddTargetVisualCopies(RectTransform parent, RectTransform target)
        {
            Graphic[] graphics = target.GetComponentsInChildren<Graphic>(false);

            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic source = graphics[i];

                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy)
                    continue;

                if (source is Image sourceImage)
                {
                    GameObject copyObject = new($"TargetCopy.{source.name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    RectTransform copyRect = copyObject.GetComponent<RectTransform>();
                    Image copy = copyObject.GetComponent<Image>();
                    MatchTargetGraphicRect(copyRect, parent, target, source.rectTransform);
                    CopyImagePresentation(sourceImage, copy);
                }
                else if (source is TMP_Text sourceText)
                {
                    GameObject copyObject = new($"TargetCopy.{source.name}", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    RectTransform copyRect = copyObject.GetComponent<RectTransform>();
                    TextMeshProUGUI copy = copyObject.GetComponent<TextMeshProUGUI>();
                    MatchTargetGraphicRect(copyRect, parent, target, source.rectTransform);
                    CopyTextPresentation(sourceText, copy);
                }
            }
        }

        private static void StretchToParent(RectTransform rect, RectTransform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void MatchTargetGraphicRect(
            RectTransform copy,
            RectTransform parent,
            RectTransform target,
            RectTransform source)
        {
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);
            Vector2 min = target.InverseTransformPoint(corners[0]);
            Vector2 max = min;

            for (int i = 1; i < corners.Length; i++)
            {
                Vector2 point = target.InverseTransformPoint(corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            Rect targetRect = target.rect;
            copy.SetParent(parent, false);
            copy.anchorMin = new Vector2(
                (min.x - targetRect.xMin) / targetRect.width,
                (min.y - targetRect.yMin) / targetRect.height);
            copy.anchorMax = new Vector2(
                (max.x - targetRect.xMin) / targetRect.width,
                (max.y - targetRect.yMin) / targetRect.height);
            copy.offsetMin = Vector2.zero;
            copy.offsetMax = Vector2.zero;
            copy.localScale = Vector3.one;
        }

        private static void CopyImagePresentation(Image source, Image copy)
        {
            copy.sprite = source.sprite;
            copy.type = source.type;
            copy.preserveAspect = source.preserveAspect;
            copy.fillCenter = source.fillCenter;
            copy.fillMethod = source.fillMethod;
            copy.fillAmount = source.fillAmount;
            copy.fillClockwise = source.fillClockwise;
            copy.fillOrigin = source.fillOrigin;
            copy.useSpriteMesh = source.useSpriteMesh;
            copy.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            copy.color = source.color;
            copy.raycastTarget = false;
        }

        private static void CopyTextPresentation(TMP_Text source, TextMeshProUGUI copy)
        {
            copy.text = source.text;
            copy.font = source.font;
            copy.fontSharedMaterial = source.fontSharedMaterial;
            copy.fontSize = source.fontSize;
            copy.fontStyle = source.fontStyle;
            copy.alignment = source.alignment;
            copy.enableAutoSizing = source.enableAutoSizing;
            copy.fontSizeMin = source.fontSizeMin;
            copy.fontSizeMax = source.fontSizeMax;
            copy.enableWordWrapping = source.enableWordWrapping;
            copy.overflowMode = source.overflowMode;
            copy.richText = source.richText;
            copy.margin = source.margin;
            copy.color = source.color;
            copy.raycastTarget = false;
        }

        private void StartDeferredAdvanceToStage(PlanetTutorialStage stage)
        {
            StopDeferredStageAdvanceCoroutine();
            _deferredStageAdvanceCoroutine = StartCoroutine(DeferredAdvanceToStageRoutine(stage));
        }

        private IEnumerator DeferredAdvanceToStageRoutine(PlanetTutorialStage stage)
        {
            yield return null;

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

        // ================================================================
        // Helpers
        // ================================================================
        /// <summary>
        /// Fills any unset serialized references at runtime. The Messages/Tutorial infrastructure
        /// (MessageManager, DialogManager, TutorialManager and the message views) lives on a
        /// DontDestroyOnLoad "MetaPlanetTutorial" GameObject in PlanetMeta and persists into
        /// PlanetLevel1 in the build flow (Meta -> PlanetLevel1 via SceneManager.LoadScene). It is
        /// therefore absent from the PlanetLevel1 scene file and must be located at runtime instead
        /// of through the Inspector. Planet-side objects (UIController, GameRules, GameUI) live in
        /// the PlanetLevel1 scene and are resolved by type as well, so no manual wiring is required.
        /// </summary>
        private void ResolveReferences()
        {
            MessageManager messageManager = MessageManager.Instance;

            if (_tutorialManager == null)
            {
                _tutorialManager = messageManager != null
                    ? messageManager.GetComponent<TutorialManager>()
                    : null;

                if (_tutorialManager == null)
                    _tutorialManager = FindFirstObjectByType<TutorialManager>();
            }

            if (_dialogManager == null && messageManager != null)
                _dialogManager = messageManager.GetComponent<DialogManager>();

            if (_interactionOverlay == null)
                _interactionOverlay = FindFirstObjectByType<TutorialInteractionOverlay>();

            if (_highlightSystem == null && _interactionOverlay != null)
                _highlightSystem = _interactionOverlay.GetComponent<TutorialHighlightSystem>();

            if (_uiController == null)
                _uiController = FindFirstObjectByType<UIController>();

            if (_gameRules == null)
                _gameRules = FindFirstObjectByType<GameRules>();

            if (_gameUI == null)
                _gameUI = FindFirstObjectByType<GameUI>();
        }

        private static string GetComponentName(Component component) =>
            component != null ? component.name : "null";

        private static string GetManagerName(TutorialManager manager) =>
            manager != null ? manager.name : "null";

        private bool HasRequiredReferences()
        {
            bool ok = true;

            if (_tutorialManager == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing TutorialManager", this);
                ok = false;
            }

            if (_dialogManager == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing DialogManager", this);
                ok = false;
            }

            if (_interactionOverlay == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing TutorialInteractionOverlay", this);
                ok = false;
            }

            if (_uiController == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing UIController", this);
                ok = false;
            }

            if (_gameRules == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing GameRules", this);
                ok = false;
            }

            if (_gameUI == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] missing GameUI", this);
                ok = false;
            }

            return ok;
        }

        private PlanetTutorialStage LoadStage()
        {
            int value = PlayerPrefs.GetInt(CurrentStageKey, (int)PlanetTutorialStage.FirstVisit);

            if (value < (int)PlanetTutorialStage.FirstVisit || value > (int)PlanetTutorialStage.BuySmallTreeManager)
            {
                Debug.LogWarning($"[PlanetTutorialTrace] saved stage out of range value={value}, fallback FirstVisit", this);
                return PlanetTutorialStage.FirstVisit;
            }

            return (PlanetTutorialStage)value;
        }

        private void SaveStage(PlanetTutorialStage stage)
        {
            PlayerPrefs.SetInt(CurrentStageKey, (int)stage);
            PlayerPrefs.SetString(CurrentStepIdKey, stage.ToString());
            PlayerPrefs.Save();
        }

        private static bool IsCompleted() => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

        private static void SaveFlag(string key)
        {
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        private double GetCurrentMoney() => _gameRules != null ? _gameRules.CurrentMoney : 0d;

        private double GetSmallTreeUpgradeCost() => _gameRules != null ? _gameRules.GetItemUpgradeCost(SmallTreeIndex) : 0d;

        // BigTree starts unowned, so its upgrade price at count 0 equals its first-purchase cost.
        private double GetBigTreeFirstCost() => _gameRules != null ? _gameRules.GetItemUpgradeCost(BigTreeIndex) : 0d;

        private double GetSmallTreeManagerCost() => _gameRules != null ? _gameRules.GetManagerPrice(SmallTreeIndex) : 0d;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.Dialogs;
using UnityEngine;

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
        private const string TutorialActionsCompletedKey = "PlanetTutorialActionsCompleted";

        private const int SmallTreeIndex = 0;
        private const int BigTreeIndex = 1;

        // TutorialManager step ids (also used as current-step guards in the event handlers).
        private const string StepIdOpenCreation = "Planet.OpenCreationTab";
        private const string StepIdEarnCoins = "Planet.EarnCoinsSmallTree";
        private const string StepIdBuySmallTree = "Planet.BuySmallTree";
        private const string StepIdBuyBigTree = "Planet.BuyBigTree";
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

        [Header("Legacy Tutorial Optional")]
        [Tooltip("Optional reference to the legacy global::TutorialManager. It is never disabled by this controller.")]
        [SerializeField] private global::TutorialManager _legacyTutorialManager;

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

        private Coroutine _initializationCoroutine;
        private Coroutine _dialogRetryCoroutine;
        private Coroutine _stepCompletedCoroutine;
        private Coroutine _deferredStageAdvanceCoroutine;

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

            if (_ownsTutorialSession)
            {
                HideBlocker();

                if (_tutorialManager != null)
                    _tutorialManager.CancelTutorial();

                _ownsTutorialSession = false;
            }

            _isRunning = false;
            _isEarnPhaseActive = false;
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

            if (_legacyTutorialManager != null)
                Debug.Log("[PlanetTutorialTrace] legacy TutorialManager reference is set; it is not disabled by this controller", this);

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

            if (_ownsTutorialSession && _tutorialManager != null)
                _tutorialManager.CancelTutorial();

            if (_ownsTutorialSession)
                HideBlocker();

            _ownsTutorialSession = false;

            Debug.Log("[PlanetTutorialTrace] tutorial completed", this);
        }

        private void StartStage(PlanetTutorialStage stage)
        {
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
                    StartManualHighlightStep(StepIdOpenCreation, DialogOpenCreation, target, CharacterMood.Thinking);
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
                () => EarnUntilMoneyOrAdvance(GetSmallTreeManagerCost(), PlanetTutorialStage.BuySmallTreeManager));
        }

        // ================================================================
        // STAGE 6: Buy the SmallTree automation manager
        // ================================================================
        private void StepBuySmallTreeManager()
        {
            EnsureShopOpenOnCreationTab();

            if (PlayerPrefs.GetInt(SmallTreeManagerBoughtKey, 0) == 1 ||
                (_gameRules != null && _gameRules.IsManagerPurchased(SmallTreeIndex)))
            {
                Debug.Log("[PlanetTutorialTrace] BuySmallTreeManager skipped: manager already purchased", this);
                CompleteTutorial();
                return;
            }

            double managerCost = GetSmallTreeManagerCost();

            if (managerCost > 0d && GetCurrentMoney() < managerCost)
            {
                StartEarnPhase(managerCost, HintEarnMore, () => AdvanceToStage(PlanetTutorialStage.BuySmallTreeManager));
                return;
            }

            GameObject target = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(SmallTreeIndex) : null;

            if (target == null || !target.activeInHierarchy)
            {
                Debug.LogWarning("[PlanetTutorialTrace] BuySmallTreeManager: manager buy target missing/inactive; completing tutorial to avoid deadlock", this);
                CompleteTutorial();
                return;
            }

            if (_gameUI != null)
                _gameUI.SetManagerBuyButtonInteractable(SmallTreeIndex, true);

            _tutorialStepCompleted = null; // Completed via OnManagerPurchasedConfirmed event.
            StartManualHighlightStep(StepIdBuyManager, HintBuyManager, target, CharacterMood.Thinking);
        }

        private void HandleManagerBought()
        {
            Debug.Log("[PlanetTutorialTrace] SmallTree manager purchased", this);
            CompleteManualStep();

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
            StartManualHighlightStep(StepIdEarnCoins, hint, target, CharacterMood.Thinking);

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

            StartManualHighlightStep(stepId, hint, target, CharacterMood.Thinking);
            return true;
        }

        // ================================================================
        // Tutorial step plumbing
        // ================================================================
        private void StartManualHighlightStep(string stepId, string hint, GameObject target, CharacterMood mood)
        {
            if (_tutorialManager == null)
            {
                Debug.LogWarning("[PlanetTutorialTrace] TutorialManager missing; cannot start step", this);
                return;
            }

            _ownsTutorialSession = true;
            HideBlocker();
            SetConversationViewsTopPlacement(ShouldUseTopPlacement(), stepId, "StartStep");

            TutorialStepData stepData = new()
            {
                StepId = stepId,
                MessageText = hint,
                TargetUI = target,
                CompletionCondition = TutorialCompletionCondition.Manual,
                SpeakerId = _contractorSpeakerId,
                CharacterMood = mood,
                EnableHighlight = target != null,
                EnablePulse = target != null,
                ShowArrow = target != null,
                DisableInteractionCutout = false
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
            ShowBlocker();
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
                return;

            Debug.Log("[PlanetTutorialTrace] shop opened during OpenCreationTab step", this);
            CompleteManualStep();
        }

        private void HandleShopTabSelected(int index)
        {
            // Informational only.
        }

        private void HandleItemIncomeEarned(int index, double income)
        {
            if (index != SmallTreeIndex || income <= 0d)
                return;

            if (!_smallTreeFirstEarnShown)
            {
                _smallTreeFirstEarnShown = true;
                SaveFlag(CoinsEarnedFromSmallTreeKey);

                if (_gameUI != null)
                    _gameUI.PlayCoinsEarnedFeedback();

                ShowToast(ToastEarnedFirst);
                Debug.Log("[PlanetTutorialTrace] first coins earned from SmallTree", this);
            }

            if (_isEarnPhaseActive && GetCurrentMoney() >= _earnTargetMoney)
            {
                Debug.Log($"[PlanetTutorialTrace] earn target reached money={GetCurrentMoney()} target={_earnTargetMoney}", this);
                _isEarnPhaseActive = false;
                CompleteManualStep();
            }
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
            if (index == SmallTreeIndex)
                SaveFlag(SmallTreeManagerBoughtKey);

            TutorialStepData current = _tutorialManager != null ? _tutorialManager.CurrentStep : null;

            if (current != null && current.StepId == StepIdBuyManager && index == SmallTreeIndex)
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
                _gameRules.OnItemFirstPurchaseConfirmed += HandleItemFirstPurchaseConfirmed;
                _gameRules.OnItemUpgradedConfirmed += HandleItemUpgradedConfirmed;
                _gameRules.OnManagerPurchasedConfirmed += HandleManagerPurchasedConfirmed;
            }

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
                _gameRules.OnItemFirstPurchaseConfirmed -= HandleItemFirstPurchaseConfirmed;
                _gameRules.OnItemUpgradedConfirmed -= HandleItemUpgradedConfirmed;
                _gameRules.OnManagerPurchasedConfirmed -= HandleManagerPurchasedConfirmed;
            }

            _isSubscribed = false;
        }

        private void ConfigureTyping()
        {
            if (_tutorialManager != null)
                _tutorialManager.SetTypingSettings(true, _typingSpeed, true);

            if (_dialogManager != null)
                _dialogManager.SetTypingSettings(true, _typingSpeed, true);
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

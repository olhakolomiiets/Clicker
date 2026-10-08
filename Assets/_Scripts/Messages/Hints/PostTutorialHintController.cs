using System;
using System.Collections.Generic;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.Dialogs;
using PlanetBuilder.Messages.Tutorial;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PlanetBuilder.Messages.Hints
{
    [DisallowMultipleComponent]
    public sealed class PostTutorialHintController : MonoBehaviour
    {
        private const string PlanetTutorialCompletedKey = "PlanetTutorialCompleted";
        private const string UpgradeMenuIntroShownKey = "PostTutorialHint.UpgradeMenuIntro.Shown";
        private const string ProfessorSpeakerId = "1";
        private const string UpgradeMenuIntroId = "PostTutorialHint.UpgradeMenuIntro";

        private const float BuyMoreObjectDelay = 60f;
        private const string UpgradeMenuIntroText =
            "Здесь находятся улучшения. Некоторые из них заметно ускоряют развитие планеты.";
        private const string UnlockAvailableText =
            "Отлично. Денег уже хватает, чтобы открыть новый объект.";
        private const string AutomationAvailableText =
            "Можно автоматизировать этот объект. Тогда он будет приносить доход без постоянных нажатий.";
        private const string BuyMoreObjectText =
            "У вас уже хватает средств. Купите ещё один объект, и доход будет расти быстрее.";

        private enum ReminderType
        {
            None,
            UnlockAvailable,
            AutomationAvailable,
            BuyMoreObject,
            UpgradeMenuIntro
        }

        private sealed class ReminderCandidate
        {
            public ReminderType Type;
            public int Index;
            public float EligibleElapsed;
            public bool TimerStarted;
            public bool Ready;
            public bool TriggeredByMenuOpen;
            public bool Shown;
            public bool DisplayConsumed;
        }

        private static PostTutorialHintController _instance;

        [Header("Post-Tutorial Hints")]
        [SerializeField, Min(0f)] private float _globalCooldown = 60f;
        [SerializeField, Min(0f)] private float _upgradeMenuIntroDelay = 60f;
        [SerializeField] private string _planetScenePrefix = "PlanetLevel";

        [Header("Existing Message Infrastructure")]
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private DialogManager _dialogManager;
        [SerializeField] private TutorialHighlightSystem _highlightSystem;

        private readonly List<ReminderCandidate> _candidates = new();
        private MessageManager _messageManager;
        private MessageDisplayValidator _displayValidator;
        private GameRules _gameRules;
        private GameUI _gameUI;
        private UIController _uiController;
        private ReminderCandidate _activeCandidate;
        private TutorialStepData _activeHighlightStep;
        private float _lastHintShownAt = float.NegativeInfinity;
        private string _lastBlockReason;
        private int _selectedShopTab = -1;
        private bool _isInitialized;
        private bool _didInitialGameplaySync;
        private bool _isApplicationPaused;
        private bool _hasApplicationFocus = true;
        private bool _isSceneTransitioning;
        private bool _isMessageCloseRequested;
        private bool _isHighlightVisible;
        private bool _planetTutorialCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureControllerExists();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureControllerExists();
        }

        private static void EnsureControllerExists()
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null ||
                messageManager.GetComponent<PostTutorialHintController>() != null)
            {
                return;
            }

            messageManager.gameObject.AddComponent<PostTutorialHintController>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            TryInitialize();
        }

        private void Start()
        {
            TryInitialize();
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            UnsubscribeFromUI();
            UnsubscribeFromGameplay();
            UnsubscribeFromMessageManager();
            HideHighlight();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            if (!_isInitialized || _isApplicationPaused || !_hasApplicationFocus)
                return;

            RefreshPlanetTutorialCompletion();

            if (!IsSuitablePlanetScene())
            {
                SetBlockReason("not in a Planet scene");
                return;
            }

            ResolveSceneReferencesIfNeeded();

            if (!_didInitialGameplaySync &&
                _gameRules != null &&
                _gameRules.IsPrepared &&
                _gameUI != null &&
                _gameUI.CreationItemCount > 0)
            {
                SyncGameplayCandidates();
                _didInitialGameplaySync = true;
            }

            EnsureUpgradeMenuIntroCandidate();

            ValidateActiveCandidate();
            AdvanceCandidates();
            TryShowBestCandidate();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _isApplicationPaused = pauseStatus;

            if (pauseStatus)
                SetBlockReason("application paused");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            _hasApplicationFocus = hasFocus;

            if (!hasFocus)
                SetBlockReason("application unfocused");
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
        {
            _isSceneTransitioning = false;
            CancelActiveReminder("scene changed");
            _candidates.Clear();
            _selectedShopTab = -1;
            _didInitialGameplaySync = false;
            UnsubscribeFromUI();
            UnsubscribeFromGameplay();
            _uiController = null;
            _gameUI = null;
            _gameRules = null;
            ResolveSceneReferences();
            SubscribeToGameplay();
            SetBlockReason(null);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene != SceneManager.GetActiveScene())
                return;

            _isSceneTransitioning = true;
            CancelActiveReminder("scene unloading");
            _candidates.Clear();
            _selectedShopTab = -1;
            _didInitialGameplaySync = false;
            UnsubscribeFromUI();
            UnsubscribeFromGameplay();
            _uiController = null;
            _gameUI = null;
            _gameRules = null;
        }

        private void HandleShopOpened()
        {
            ResolveSceneReferencesIfNeeded();
            RetryReadyCandidates();
        }

        private void HandleShopTabSelected(int tabIndex)
        {
            _selectedShopTab = tabIndex;

            if (tabIndex == 1)
            {
                HandleUpgradeMenuOpened();
                RetryReadyCandidates();
                return;
            }

            ReminderCandidate pendingUpgrade = FindCandidate(ReminderType.UpgradeMenuIntro, -1);

            if (pendingUpgrade != null && pendingUpgrade.TriggeredByMenuOpen)
            {
                _candidates.Remove(pendingUpgrade);
                LogTransition("UpgradeMenuIntro pending menu-open candidate cancelled: upgrade menu closed");
            }

            RetryReadyCandidates();
        }

        private void HandleManagerPanelOpened(int index)
        {
            RetryReadyCandidates();
        }

        private void EnsureUpgradeMenuIntroCandidate()
        {
            if (!_planetTutorialCompleted ||
                PlayerPrefs.GetInt(UpgradeMenuIntroShownKey, 0) == 1 ||
                FindCandidate(ReminderType.UpgradeMenuIntro, -1) != null)
            {
                return;
            }

            GetOrCreateCandidate(ReminderType.UpgradeMenuIntro, -1);
        }

        private void HandleUpgradeMenuOpened()
        {
            if (_activeCandidate != null &&
                _activeCandidate.Type == ReminderType.UpgradeMenuIntro &&
                !_activeCandidate.TriggeredByMenuOpen)
            {
                CompleteActiveReminder("upgrade menu opened");
                return;
            }

            RefreshPlanetTutorialCompletion();

            if (!_planetTutorialCompleted ||
                PlayerPrefs.GetInt(UpgradeMenuIntroShownKey, 0) == 1)
            {
                return;
            }

            ReminderCandidate candidate = FindCandidate(ReminderType.UpgradeMenuIntro, -1) ??
                                           GetOrCreateCandidate(ReminderType.UpgradeMenuIntro, -1);
            candidate.TriggeredByMenuOpen = true;
            candidate.Ready = true;
            candidate.TimerStarted = false;
            candidate.EligibleElapsed = 0f;
            HideHighlight();

            LogTransition("UpgradeMenuIntro triggered by early menu open");
            RetryReadyCandidates();
        }

        private void HandleMessageShown(MessageData message)
        {
            ReminderCandidate candidate = FindCandidate(message != null ? message.Id : null);

            if (candidate == null)
                return;

            _activeCandidate = candidate;
            candidate.Shown = true;
            _lastHintShownAt = Time.unscaledTime;
            SetBlockReason(null);

            if (candidate.Type == ReminderType.UpgradeMenuIntro)
            {
                PlayerPrefs.SetInt(UpgradeMenuIntroShownKey, 1);
                PlayerPrefs.Save();
                LogTransition("UpgradeMenuIntro shown");
                LogTransition("persistence saved: UpgradeMenuIntro.Shown=1");

                if (candidate.TriggeredByMenuOpen)
                    LogTransition("UpgradeMenuIntro shown from menu open");
            }
            else
            {
                candidate.DisplayConsumed = true;
                candidate.Ready = false;
                LogTransition($"{GetCandidateLabel(candidate)} shown index={candidate.Index}");
            }
        }

        private void HandleMessageClosed(MessageData message)
        {
            if (_activeCandidate == null ||
                message == null ||
                message.Id != GetCandidateId(_activeCandidate))
            {
                return;
            }

            ReminderCandidate closedCandidate = _activeCandidate;
            _activeCandidate = null;
            HideHighlight();

            if (closedCandidate.Type != ReminderType.UpgradeMenuIntro)
            {
                closedCandidate.DisplayConsumed = true;
                closedCandidate.Ready = false;
                closedCandidate.TimerStarted = false;
                closedCandidate.EligibleElapsed = 0f;
            }

            if (closedCandidate.Type == ReminderType.UpgradeMenuIntro)
            {
                _candidates.Remove(closedCandidate);

                if (!_isMessageCloseRequested && !_isSceneTransitioning)
                    LogTransition("UpgradeMenuIntro cancelled");
            }
            else if (!_isMessageCloseRequested && !_isSceneTransitioning)
            {
                LogTransition($"{GetCandidateLabel(closedCandidate)} closed index={closedCandidate.Index}");
            }

            _isMessageCloseRequested = false;
        }

        private void HandleItemReadyToBuy(int index, int state)
        {
            RefreshUnlockCandidate(index);
            RefreshBuyMoreCandidate(index);
            RetryReadyCandidates();
        }

        private void HandleItemNotReadyToBuy(int index, int state)
        {
            RefreshUnlockCandidate(index);
            RefreshBuyMoreCandidate(index);
        }

        private void HandleManagerAvailability(int index, int state)
        {
            RefreshAutomationCandidate(index);
            RetryReadyCandidates();
        }

        private void HandleManagerNonAvailability(int index, int state)
        {
            RefreshAutomationCandidate(index);
        }

        private void HandleGameplayDataUpdated(int index, GameData data, GeneralGameData generalData)
        {
            if (_gameRules == null || !_gameRules.IsPrepared)
                return;

            RefreshUnlockCandidate(index);
            RefreshBuyMoreCandidate(index);
            RefreshAutomationCandidate(index);
            RetryReadyCandidates();
        }

        private void HandleItemFirstPurchaseConfirmed(int index)
        {
            CompleteCandidate(ReminderType.UnlockAvailable, index, "first object purchased");
            RefreshUnlockCandidate(index);
            RefreshBuyMoreCandidate(index);
        }

        private void HandleManagerPurchasedConfirmed(int index)
        {
            CompleteCandidate(ReminderType.AutomationAvailable, index, "manager purchased");
            RefreshAutomationCandidate(index);
        }

        private void HandleItemUpgradedConfirmed(int index)
        {
            CompleteCandidate(ReminderType.BuyMoreObject, index, "object purchased again");
            RefreshBuyMoreCandidate(index);
        }

        private void TryInitialize()
        {
            if (_isInitialized)
                return;

            _messageManager = MessageManager.Instance;

            if (_messageManager == null)
                return;

            if (_tutorialManager == null)
                _tutorialManager = _messageManager.GetComponent<TutorialManager>();

            if (_dialogManager == null)
                _dialogManager = _messageManager.GetComponent<DialogManager>();

            if (_highlightSystem == null)
                _highlightSystem = _messageManager.GetComponentInChildren<TutorialHighlightSystem>(true);

            _displayValidator = _messageManager.DisplayValidator;
            SubscribeToMessageManager();
            ResolveSceneReferences();
            SubscribeToGameplay();
            _planetTutorialCompleted = IsPlanetTutorialCompleted();
            _isInitialized = true;

            Debug.Log(
                $"[PostTutorialHint] initialized scene={SceneManager.GetActiveScene().name} " +
                $"planetTutorialCompleted={_planetTutorialCompleted} " +
                $"upgradeShown={PlayerPrefs.GetInt(UpgradeMenuIntroShownKey, 0) == 1}",
                this);
        }

        private void ResolveSceneReferencesIfNeeded()
        {
            if (_uiController == null || _gameUI == null || _gameRules == null)
                ResolveSceneReferences();
        }

        private void ResolveSceneReferences()
        {
            UnsubscribeFromUI();
            UnsubscribeFromGameplay();

            if (!IsSuitablePlanetScene())
                return;

            _uiController = FindFirstObjectByType<UIController>();
            _gameUI = FindFirstObjectByType<GameUI>();
            _gameRules = FindFirstObjectByType<GameRules>();
            SubscribeToUI();
            SubscribeToGameplay();
        }

        private void SubscribeToMessageManager()
        {
            if (_messageManager == null)
                return;

            _messageManager.OnMessageShown -= HandleMessageShown;
            _messageManager.OnMessageShown += HandleMessageShown;
            _messageManager.OnMessageClosed -= HandleMessageClosed;
            _messageManager.OnMessageClosed += HandleMessageClosed;
        }

        private void UnsubscribeFromMessageManager()
        {
            if (_messageManager == null)
                return;

            _messageManager.OnMessageShown -= HandleMessageShown;
            _messageManager.OnMessageClosed -= HandleMessageClosed;
        }

        private void SubscribeToUI()
        {
            if (_uiController != null)
            {
                _uiController.OnShopOpened -= HandleShopOpened;
                _uiController.OnShopOpened += HandleShopOpened;
                _uiController.OnShopTabSelected -= HandleShopTabSelected;
                _uiController.OnShopTabSelected += HandleShopTabSelected;
            }

            if (_gameUI != null)
            {
                _gameUI.OnManagerPanelOpened -= HandleManagerPanelOpened;
                _gameUI.OnManagerPanelOpened += HandleManagerPanelOpened;
            }
        }

        private void UnsubscribeFromUI()
        {
            if (_uiController != null)
            {
                _uiController.OnShopOpened -= HandleShopOpened;
                _uiController.OnShopTabSelected -= HandleShopTabSelected;
            }

            if (_gameUI != null)
                _gameUI.OnManagerPanelOpened -= HandleManagerPanelOpened;
        }

        private void SubscribeToGameplay()
        {
            if (_gameRules == null)
                return;

            _gameRules.OnItemReadyToBuy -= HandleItemReadyToBuy;
            _gameRules.OnItemReadyToBuy += HandleItemReadyToBuy;
            _gameRules.OnItemNotReadyToBuy -= HandleItemNotReadyToBuy;
            _gameRules.OnItemNotReadyToBuy += HandleItemNotReadyToBuy;
            _gameRules.OnManagerAvailability -= HandleManagerAvailability;
            _gameRules.OnManagerAvailability += HandleManagerAvailability;
            _gameRules.OnManagerNonAvailability -= HandleManagerNonAvailability;
            _gameRules.OnManagerNonAvailability += HandleManagerNonAvailability;
            _gameRules.OnUpdateData -= HandleGameplayDataUpdated;
            _gameRules.OnUpdateData += HandleGameplayDataUpdated;
            _gameRules.OnItemFirstPurchaseConfirmed -= HandleItemFirstPurchaseConfirmed;
            _gameRules.OnItemFirstPurchaseConfirmed += HandleItemFirstPurchaseConfirmed;
            _gameRules.OnManagerPurchasedConfirmed -= HandleManagerPurchasedConfirmed;
            _gameRules.OnManagerPurchasedConfirmed += HandleManagerPurchasedConfirmed;
            _gameRules.OnItemUpgradedConfirmed -= HandleItemUpgradedConfirmed;
            _gameRules.OnItemUpgradedConfirmed += HandleItemUpgradedConfirmed;
        }

        private void UnsubscribeFromGameplay()
        {
            if (_gameRules == null)
                return;

            _gameRules.OnItemReadyToBuy -= HandleItemReadyToBuy;
            _gameRules.OnItemNotReadyToBuy -= HandleItemNotReadyToBuy;
            _gameRules.OnManagerAvailability -= HandleManagerAvailability;
            _gameRules.OnManagerNonAvailability -= HandleManagerNonAvailability;
            _gameRules.OnUpdateData -= HandleGameplayDataUpdated;
            _gameRules.OnItemFirstPurchaseConfirmed -= HandleItemFirstPurchaseConfirmed;
            _gameRules.OnManagerPurchasedConfirmed -= HandleManagerPurchasedConfirmed;
            _gameRules.OnItemUpgradedConfirmed -= HandleItemUpgradedConfirmed;
        }

        private void SyncGameplayCandidates()
        {
            if (_gameRules == null || _gameUI == null)
                return;

            for (int index = 0; index < _gameUI.CreationItemCount; index++)
            {
                RefreshUnlockCandidate(index);
                RefreshBuyMoreCandidate(index);

                // Automation candidates are event-driven because GameRules owns the actual availability signal.
                RefreshAutomationCandidate(index);
            }
        }

        private void RefreshUnlockCandidate(int index)
        {
            if (!IsItemUnlockEligible(index))
            {
                RemoveCandidate(ReminderType.UnlockAvailable, index, true);
                return;
            }

            ReminderCandidate candidate = GetOrCreateCandidate(ReminderType.UnlockAvailable, index);
            candidate.Ready = !candidate.DisplayConsumed;
        }

        private void RefreshAutomationCandidate(int index)
        {
            if (!IsAutomationEligible(index))
            {
                RemoveCandidate(ReminderType.AutomationAvailable, index, true);
                return;
            }

            ReminderCandidate candidate = GetOrCreateCandidate(ReminderType.AutomationAvailable, index);
            candidate.Ready = !candidate.DisplayConsumed;
        }

        private void RefreshBuyMoreCandidate(int index)
        {
            if (!IsBuyMoreEligible(index))
            {
                RemoveCandidate(ReminderType.BuyMoreObject, index, true);
                return;
            }

            ReminderCandidate candidate = GetOrCreateCandidate(ReminderType.BuyMoreObject, index);
            if (!candidate.DisplayConsumed)
                candidate.Ready = candidate.Ready || candidate.EligibleElapsed >= BuyMoreObjectDelay;
        }

        private bool IsItemUnlockEligible(int index)
        {
            return _gameRules != null &&
                   _gameRules.IsPrepared &&
                   !_gameRules.IsItemOwned(index) &&
                   _gameRules.CurrentMoney >= _gameRules.GetItemUpgradeCost(index);
        }

        private bool IsAutomationEligible(int index)
        {
            return _gameRules != null &&
                   _gameRules.IsPrepared &&
                   _gameRules.IsItemOwned(index) &&
                   !_gameRules.IsManagerPurchased(index) &&
                   !_gameRules.IsItemPremium(index) &&
                   _gameRules.CurrentMoney > _gameRules.GetManagerPrice(index);
        }

        private bool IsBuyMoreEligible(int index)
        {
            return _gameRules != null &&
                   _gameRules.IsPrepared &&
                   _gameRules.IsItemOwned(index) &&
                   _gameRules.CurrentMoney >= _gameRules.GetItemUpgradeCost(index);
        }

        private void AdvanceCandidates()
        {
            string commonBlocker = GetCommonBlockReason(false);

            if (string.IsNullOrEmpty(commonBlocker))
                SetBlockReason(null);

            for (int i = _candidates.Count - 1; i >= 0; i--)
            {
                ReminderCandidate candidate = _candidates[i];

                if (!IsCandidateStillEligible(candidate))
                {
                    RemoveCandidate(candidate, true);
                    continue;
                }

                if (candidate.Ready || candidate.Type != ReminderType.BuyMoreObject && candidate.Type != ReminderType.UpgradeMenuIntro)
                    continue;

                if (candidate.Type == ReminderType.UpgradeMenuIntro)
                {
                    if (candidate.TriggeredByMenuOpen)
                        continue;

                    if (!CanAdvanceUpgradeTimer(commonBlocker))
                        continue;

                    if (!candidate.TimerStarted)
                    {
                        candidate.TimerStarted = true;
                        LogTransition("UpgradeMenuIntro timer started");
                    }

                    candidate.EligibleElapsed += Time.unscaledDeltaTime;

                    if (candidate.EligibleElapsed >= Mathf.Max(0f, _upgradeMenuIntroDelay))
                    {
                        candidate.Ready = true;
                        LogTransition("UpgradeMenuIntro candidate ready");
                    }

                    continue;
                }

                if (!string.IsNullOrEmpty(commonBlocker))
                    continue;

                if (!candidate.TimerStarted)
                {
                    candidate.TimerStarted = true;
                    LogTransition($"BuyMoreObject timer started index={candidate.Index}");
                }

                candidate.EligibleElapsed += Time.unscaledDeltaTime;

                if (candidate.EligibleElapsed >= BuyMoreObjectDelay)
                {
                    candidate.Ready = true;
                    LogTransition($"BuyMoreObject candidate ready index={candidate.Index}");
                }
            }
        }

        private bool CanAdvanceUpgradeTimer(string commonBlocker)
        {
            return string.IsNullOrEmpty(commonBlocker) &&
                   _uiController != null &&
                   _uiController.IsShopDisplayed == false &&
                   TryResolveUpgradeTarget(out _);
        }

        private bool IsCandidateStillEligible(ReminderCandidate candidate)
        {
            if (candidate == _activeCandidate && candidate.Shown)
                return true;

            switch (candidate.Type)
            {
                case ReminderType.UnlockAvailable:
                    return IsItemUnlockEligible(candidate.Index);
                case ReminderType.AutomationAvailable:
                    return IsAutomationEligible(candidate.Index);
                case ReminderType.BuyMoreObject:
                    return IsBuyMoreEligible(candidate.Index);
                case ReminderType.UpgradeMenuIntro:
                    return PlayerPrefs.GetInt(UpgradeMenuIntroShownKey, 0) != 1 &&
                           (!candidate.TriggeredByMenuOpen || IsUpgradeMenuOpen());
                default:
                    return false;
            }
        }

        private void TryShowBestCandidate()
        {
            if (_activeCandidate != null)
                return;

            ReminderCandidate best = null;

            for (int i = 0; i < _candidates.Count; i++)
            {
                ReminderCandidate candidate = _candidates[i];

                if (!candidate.Ready || !IsCandidateStillEligible(candidate))
                    continue;

                if (best == null || CompareCandidates(candidate, best) < 0)
                    best = candidate;
            }

            if (best != null)
                TryShowCandidate(best);
        }

        private void TryShowCandidate(ReminderCandidate candidate)
        {
            bool allowShopOpen = candidate.Type != ReminderType.UpgradeMenuIntro || candidate.TriggeredByMenuOpen;

            if (!TryResolveTarget(candidate, out GameObject target))
            {
                SetBlockReason(GetTargetBlockReason(candidate));
                return;
            }

            string blocker = GetBlockReason(target, allowShopOpen, true);

            if (!string.IsNullOrEmpty(blocker))
            {
                SetBlockReason(blocker);
                return;
            }

            if (_messageManager == null ||
                _displayValidator == null ||
                !CanUseGlobalCooldown())
            {
                SetBlockReason("global cooldown");
                return;
            }

            MessageData message = CreateMessage(candidate, allowShopOpen);

            if (_messageManager.IsMessageActiveOrQueued(message.Id))
                return;

            if (!_displayValidator.CanDisplay(message))
            {
                SetBlockReason("validator rejected hint");
                return;
            }

            if (!TryShowHighlight(candidate, target))
                return;

            if (!_messageManager.TryShowMessageImmediately(message))
            {
                HideHighlight();
                SetBlockReason("hint not shown");
                return;
            }

            // The immediate API never queues. OnMessageShown remains the factual display confirmation.
        }

        private bool TryShowHighlight(ReminderCandidate candidate, GameObject target)
        {
            bool shouldHighlight = candidate.Type != ReminderType.UpgradeMenuIntro || !candidate.TriggeredByMenuOpen;

            if (!shouldHighlight)
            {
                HideHighlight();
                return true;
            }

            _activeHighlightStep = CreateHighlightStep(candidate);

            if (_highlightSystem == null ||
                !_highlightSystem.Show(target, _activeHighlightStep))
            {
                _activeHighlightStep = null;
                SetBlockReason("highlight unavailable");
                return false;
            }

            _isHighlightVisible = true;
            return true;
        }

        private MessageData CreateMessage(ReminderCandidate candidate, bool allowShopOpen)
        {
            return new MessageData
            {
                Id = GetCandidateId(candidate),
                Text = GetCandidateText(candidate.Type),
                SpeakerId = ProfessorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                Type = MessageType.Tutorial,
                Channel = MessageChannel.Hint,
                Priority = GetPriority(candidate.Type),
                DisplayDuration = 6f,
                Lifetime = 6f,
                CanInterrupt = false,
                CanDisplayOverModal = false,
                CanDisplayOverShop = allowShopOpen,
                UseTypingAnimation = true,
                TypingSpeed = 30f,
                CanSkipTyping = true
            };
        }

        private void ValidateActiveCandidate()
        {
            if (_activeCandidate == null)
                return;

            if (!IsSuitablePlanetScene() ||
                !IsCandidateStillEligible(_activeCandidate) ||
                !TryResolveTarget(_activeCandidate, out GameObject target) ||
                !string.IsNullOrEmpty(GetBlockReason(
                    target,
                    _activeCandidate.Type != ReminderType.UpgradeMenuIntro || _activeCandidate.TriggeredByMenuOpen,
                    false)))
            {
                CancelActiveReminder("target or blocker became invalid");
            }
        }

        private void RetryReadyCandidates()
        {
            if (!_isInitialized || _activeCandidate != null)
                return;

            TryShowBestCandidate();
        }

        private void CompleteCandidate(ReminderType type, int index, string reason)
        {
            ReminderCandidate candidate = FindCandidate(type, index);

            if (candidate == null)
                return;

            if (candidate == _activeCandidate)
                CompleteActiveReminder(reason);
            else
            {
                _candidates.Remove(candidate);
                LogTransition($"{GetCandidateLabel(candidate)} completed index={index}: {reason}");
            }
        }

        private void CompleteActiveReminder(string reason)
        {
            if (_activeCandidate == null)
                return;

            ReminderCandidate completed = _activeCandidate;
            _isMessageCloseRequested = true;
            HideHighlight();
            _candidates.Remove(completed);
            _activeCandidate = null;

            if (_messageManager != null)
                _messageManager.CloseCurrentMessage(MessageChannel.Hint);

            LogTransition($"{GetCandidateLabel(completed)} completed index={completed.Index}: {reason}");
            _isMessageCloseRequested = false;
        }

        private void CancelActiveReminder(string reason)
        {
            if (_activeCandidate == null)
            {
                HideHighlight();
                return;
            }

            ReminderCandidate cancelled = _activeCandidate;
            _isMessageCloseRequested = true;
            HideHighlight();
            _candidates.Remove(cancelled);
            _activeCandidate = null;

            if (_messageManager != null)
                _messageManager.CloseCurrentMessage(MessageChannel.Hint);

            LogTransition($"{GetCandidateLabel(cancelled)} cancelled index={cancelled.Index}: {reason}");
            _isMessageCloseRequested = false;
        }

        private void RemoveCandidate(ReminderType type, int index, bool preserveActive)
        {
            ReminderCandidate candidate = FindCandidate(type, index);

            if (candidate != null && (!preserveActive || candidate != _activeCandidate))
                RemoveCandidate(candidate, false);
        }

        private void RemoveCandidate(ReminderCandidate candidate, bool log)
        {
            if (candidate == _activeCandidate)
                return;

            if (_candidates.Remove(candidate) && log)
                LogTransition($"{GetCandidateLabel(candidate)} became stale index={candidate.Index}");
        }

        private ReminderCandidate GetOrCreateCandidate(ReminderType type, int index)
        {
            ReminderCandidate candidate = FindCandidate(type, index);

            if (candidate != null)
                return candidate;

            candidate = new ReminderCandidate
            {
                Type = type,
                Index = index
            };
            _candidates.Add(candidate);
            LogTransition($"{GetCandidateLabel(candidate)} candidate created index={index}");
            return candidate;
        }

        private ReminderCandidate FindCandidate(ReminderType type, int index)
        {
            for (int i = 0; i < _candidates.Count; i++)
            {
                if (_candidates[i].Type == type && _candidates[i].Index == index)
                    return _candidates[i];
            }

            return null;
        }

        private ReminderCandidate FindCandidate(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
                return null;

            for (int i = 0; i < _candidates.Count; i++)
            {
                if (GetCandidateId(_candidates[i]) == messageId)
                    return _candidates[i];
            }

            return null;
        }

        private bool TryResolveTarget(ReminderCandidate candidate, out GameObject target)
        {
            target = null;

            if (_gameUI == null || _uiController == null)
                return false;

            switch (candidate.Type)
            {
                case ReminderType.UpgradeMenuIntro:
                    target = _uiController.UpgradeButtonTarget;
                    return target != null && target.activeInHierarchy &&
                           (!candidate.TriggeredByMenuOpen || IsUpgradeMenuOpen());
                case ReminderType.UnlockAvailable:
                    if (!IsCreationTabOpen())
                        return false;
                    target = _gameUI.GetCreationProgressButtonTarget(candidate.Index);
                    return target != null && target.activeInHierarchy;
                case ReminderType.AutomationAvailable:
                    if (IsManagerPanelOpen(candidate.Index))
                        target = _gameUI.GetManagerBuyButtonTarget(candidate.Index);
                    else if (IsCreationTabOpen())
                        target = _gameUI.GetManagerButtonTarget(candidate.Index);
                    return target != null && target.activeInHierarchy;
                case ReminderType.BuyMoreObject:
                    if (!IsCreationTabOpen())
                        return false;
                    target = _gameUI.GetCreationBuyButtonTarget(candidate.Index);
                    return target != null && target.activeInHierarchy;
                default:
                    return false;
            }
        }

        private string GetTargetBlockReason(ReminderCandidate candidate)
        {
            if (candidate.Type == ReminderType.UpgradeMenuIntro && candidate.TriggeredByMenuOpen)
                return "upgrade target missing or inactive";

            return candidate.Type == ReminderType.UpgradeMenuIntro
                ? "upgrade target missing or inactive"
                : "target unavailable: required shop tab or panel is closed";
        }

        private bool IsCreationTabOpen()
        {
            return _uiController != null &&
                   _uiController.IsShopDisplayed &&
                   _selectedShopTab == 0;
        }

        private bool IsUpgradeMenuOpen()
        {
            return _uiController != null &&
                   _uiController.IsShopDisplayed &&
                   _selectedShopTab == 1;
        }

        private bool IsManagerPanelOpen(int index)
        {
            GameObject target = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(index) : null;
            return target != null && target.activeInHierarchy;
        }

        private string GetBlockReason(GameObject target, bool allowShopOpen, bool includeCooldown)
        {
            string commonBlocker = GetCommonBlockReason(includeCooldown);

            if (!string.IsNullOrEmpty(commonBlocker))
                return commonBlocker;

            if (target == null || !target.activeInHierarchy)
                return "target missing or inactive";

            if (_displayValidator != null &&
                _displayValidator.IsShopOpen &&
                !allowShopOpen)
            {
                return "shop open";
            }

            return null;
        }

        private string GetCommonBlockReason(bool includeCooldown)
        {
            if (_isSceneTransitioning)
                return "scene transition";

            if (!_planetTutorialCompleted)
                return "Planet tutorial incomplete";

            if (_tutorialManager != null && _tutorialManager.IsRunning)
                return "main tutorial active";

            if (_dialogManager != null && _dialogManager.IsRunning)
                return "dialog active";

            if (_messageManager == null || _displayValidator == null)
                return "MessageManager unavailable";

            if (_displayValidator.IsTutorialRunning)
                return "validator: tutorial active";

            if (_displayValidator.IsDialogOpen)
                return "validator: dialog active";

            if (_displayValidator.IsModalWindowOpen)
            {
                return $"modal active source={_displayValidator.ModalBlockerSource ?? "unknown"}";
            }

            if (_displayValidator.IsAdvertisementOpen)
                return "advertisement active";

            if (_messageManager.GetCurrentMessage(MessageChannel.Tutorial) != null)
                return "tutorial channel busy";

            if (_messageManager.GetCurrentMessage(MessageChannel.Dialog) != null)
                return "dialog channel busy";

            MessageData currentHint = _messageManager.GetCurrentMessage(MessageChannel.Hint);

            if (currentHint != null &&
                (_activeCandidate == null || currentHint.Id != GetCandidateId(_activeCandidate)))
            {
                return "another Hint message active";
            }

            if (includeCooldown && !CanUseGlobalCooldown())
                return "global cooldown";

            return null;
        }

        private bool CanUseGlobalCooldown()
        {
            return Time.unscaledTime - _lastHintShownAt >= Mathf.Max(0f, _globalCooldown);
        }

        private bool TryResolveUpgradeTarget(out GameObject target)
        {
            target = _uiController != null ? _uiController.UpgradeButtonTarget : null;
            return target != null && target.activeInHierarchy;
        }

        private bool IsSuitablePlanetScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(_planetScenePrefix) &&
                   sceneName.StartsWith(_planetScenePrefix, StringComparison.Ordinal);
        }

        private void RefreshPlanetTutorialCompletion()
        {
            if (_planetTutorialCompleted || !IsPlanetTutorialCompleted())
                return;

            _planetTutorialCompleted = true;
            LogTransition("Planet tutorial is now complete");
        }

        private static bool IsPlanetTutorialCompleted()
        {
            return PlayerPrefs.GetInt(PlanetTutorialCompletedKey, 0) == 1;
        }

        private void SetBlockReason(string reason)
        {
            if (string.Equals(_lastBlockReason, reason, StringComparison.Ordinal))
                return;

            _lastBlockReason = reason;

            if (string.IsNullOrEmpty(reason))
                LogTransition("blockers cleared");
            else
                LogTransition($"blocked: {reason}");
        }

        private void HideHighlight()
        {
            if (!_isHighlightVisible && _highlightSystem == null)
                return;

            if (_highlightSystem != null)
                _highlightSystem.Hide();

            if (_isHighlightVisible)
                LogTransition("highlight cleanup");

            _activeHighlightStep = null;
            _isHighlightVisible = false;
        }

        private static int CompareCandidates(ReminderCandidate left, ReminderCandidate right)
        {
            int priorityComparison = GetPriority(left.Type).CompareTo(GetPriority(right.Type));

            if (priorityComparison != 0)
                return priorityComparison;

            return left.Index.CompareTo(right.Index);
        }

        private static int GetPriority(ReminderType type)
        {
            switch (type)
            {
                case ReminderType.UnlockAvailable:
                    return 1;
                case ReminderType.AutomationAvailable:
                    return 2;
                case ReminderType.BuyMoreObject:
                    return 3;
                case ReminderType.UpgradeMenuIntro:
                    return 4;
                default:
                    return int.MaxValue;
            }
        }

        private static string GetCandidateLabel(ReminderCandidate candidate)
        {
            return candidate != null ? candidate.Type.ToString() : "Reminder";
        }

        private static string GetCandidateText(ReminderType type)
        {
            switch (type)
            {
                case ReminderType.UnlockAvailable:
                    return UnlockAvailableText;
                case ReminderType.AutomationAvailable:
                    return AutomationAvailableText;
                case ReminderType.BuyMoreObject:
                    return BuyMoreObjectText;
                default:
                    return UpgradeMenuIntroText;
            }
        }

        private static string GetCandidateId(ReminderCandidate candidate)
        {
            switch (candidate.Type)
            {
                case ReminderType.UnlockAvailable:
                    return $"PostTutorialHint.UnlockAvailable.{candidate.Index}";
                case ReminderType.AutomationAvailable:
                    return $"PostTutorialHint.AutomationAvailable.{candidate.Index}";
                case ReminderType.BuyMoreObject:
                    return $"PostTutorialHint.BuyMoreObject.{candidate.Index}";
                default:
                    return UpgradeMenuIntroId;
            }
        }

        private static TutorialStepData CreateHighlightStep(ReminderCandidate candidate)
        {
            return new TutorialStepData
            {
                StepId = GetCandidateId(candidate),
                TargetUI = null,
                CompletionCondition = TutorialCompletionCondition.Manual,
                SpeakerId = ProfessorSpeakerId,
                EnableHighlight = true,
                HighlightColor = new Color(1f, 0.85f, 0.15f, 0.35f),
                HighlightPadding = new Vector2(16f, 16f),
                EnablePulse = true,
                PulseSpeed = 2f,
                PulseScale = 0.08f,
                DisableInteractionCutout = true,
                ShowArrow = false
            };
        }

        private static void LogTransition(string message)
        {
            Debug.Log($"[PostTutorialHint] {message}");
        }
    }
}

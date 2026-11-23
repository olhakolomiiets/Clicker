using System;
using System.Collections.Generic;
using Lean.Localization;
using PlanetBuilder.Loading;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.Dialogs;
using PlanetBuilder.Messages.Tutorial;
using PlanetBuilder.Messages.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PlanetBuilder.Messages.Hints
{
    [DisallowMultipleComponent]
    public sealed class PostTutorialHintController : MonoBehaviour
    {
        private const string PlanetTutorialCompletedKey = "PlanetTutorialCompleted";
        private const string UpgradeMenuIntroShownKey = "PostTutorialHint.UpgradeMenuIntro.Shown";
        private const string ProfessorSpeakerId = "1";
        private const string UpgradeMenuIntroId = "PostTutorialHint.UpgradeMenuIntro";
        private const string PassiveIncomeChestId = "PostTutorialHint.PassiveIncomeChest";
        private const string PassiveIncomeChestTextKey = "post_tutorial_hint_passive_income_chest";
        private const string UnlockAvailableTextKey = "post_tutorial_hint_unlock_available";
        private const string AutomationAvailableTextKey = "post_tutorial_hint_automation_available";
        private const string BuyMoreObjectTextKey = "post_tutorial_hint_buy_more_object";
        private const string UpgradeMenuIntroTextKey = "post_tutorial_hint_upgrade_menu_intro";
        private const string PassiveIncomeChestFallbackText =
            "Где-то на этой планете спрятан сундук с сокровищами! Найди его и забери награду!";

        private const float BuyMoreObjectDelay = 60f;
        private const string UpgradeMenuIntroText =
            "Здесь находятся улучшения. Некоторые из них заметно ускоряют развитие планеты.";
        private const string UnlockAvailableText =
            "Отлично. Денег уже хватает, чтобы открыть новый объект.";
        private const string AutomationAvailableText =
            "Можно автоматизировать этот объект. Тогда он будет приносить доход без постоянных нажатий.";
        private const string BuyMoreObjectText =
            "У вас уже хватает средств. Купите ещё один объект, и доход будет расти быстрее.";

        private const string EngagementMessageIdPrefix = "PostTutorialHint.Engagement.";

        private enum EngagementCategory
        {
            AdsX2,
            AdsDiamonds,
            MetaVisit,
            MetaArrival,
            Store
        }

        private static readonly (string Prefix, int Count, MessageUsage Usage)[] EngagementMessages =
        {
            ("ADS_X2", 5, MessageUsage.AdsX2Messages),
            ("ADS_DIAMONDS", 5, MessageUsage.AdsDiamondsMessages),
            ("META_VISIT", 6, MessageUsage.MetaVisitMessages),
            ("META_ARRIVAL", 3, MessageUsage.MetaArrivalMessages),
            ("STORE", 11, MessageUsage.StoreMessages)
        };

        private enum ReminderType
        {
            None,
            UnlockAvailable,
            AutomationAvailable,
            BuyMoreObject,
            UpgradeMenuIntro,
            PassiveIncomeChest
        }

        private enum PassiveIncomeChestDisplayMode
        {
            Timed,
            UntilFound
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
            public int ShownItemCount;
            public double ShownPrice;
            public PassiveIncomePop Chest;
        }

        private static PostTutorialHintController _instance;

        [Header("Post-Tutorial Hints")]
        [SerializeField, Min(0f)] private float _globalCooldown = 60f;
        [SerializeField, Min(0.1f)] private float _normalHintDisplayDuration = 6f;
        [SerializeField, Min(0f)] private float _upgradeMenuIntroDelay = 60f;
        [SerializeField] private string _planetScenePrefix = "PlanetLevel";

        [Header("Passive Income Chest Hint")]
        [SerializeField] private PassiveIncomeChestDisplayMode _passiveIncomeChestDisplayMode =
            PassiveIncomeChestDisplayMode.Timed;
        [SerializeField, Min(0.1f)] private float _passiveIncomeChestTimedDuration = 10f;

        [Header("Professor Engagement Messages (Seconds)")]
        [SerializeField] private bool _enableEngagementMessages = true;
        [SerializeField, Min(0f)] private float _firstMessageDelay = 90f;
        [SerializeField, Min(0f)] private float _messageInterval = 240f;
        [SerializeField, Min(0f)] private float _messageDisplayDuration = 10f;
        [SerializeField, Min(0f)] private float _categoryCooldown = 600f;

        [Header("Engagement Message Categories")]
        [SerializeField] private bool _enableAdsX2Messages = true;
        [SerializeField] private bool _enableAdsDiamondsMessages = true;
        [SerializeField] private bool _enableMetaVisitMessages = true;
        [SerializeField] private bool _enableMetaArrivalMessages = true;
        [SerializeField] private bool _enableStoreMessages = true;

        [Header("Existing Message Infrastructure")]
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private DialogManager _dialogManager;
        [SerializeField] private TutorialHighlightSystem _highlightSystem;

        private readonly List<ReminderCandidate> _candidates = new();
        private readonly Dictionary<ReminderType, int> _nextTextVariants = new();
        private readonly int[] _nextEngagementVariants = new int[EngagementMessages.Length];
        private RewardsManager _rewardsManager;
        private MetaUIController _metaUIController;
        private MetaGameRules _metaGameRules;
        private float _engagementElapsed;
        private int _nextEngagementCategory;
        private bool _hasShownEngagementMessage;
        private bool _metaArrivalShown;
        private MessageManager _messageManager;
        private MessageDisplayValidator _displayValidator;
        private GameRules _gameRules;
        private GameUI _gameUI;
        private UIController _uiController;
        private PassiveIncome _passiveIncome;
        private PassiveIncomePop _lastShownChest;
        private ReminderCandidate _activeCandidate;
        private TutorialStepData _activeHighlightStep;
        private float _lastHintShownAt = float.NegativeInfinity;
        private string _lastBlockReason;
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

            CloseEngagementMessage();
            CancelActiveReminder("controller disabled");
            _isInitialized = false;
            _didInitialGameplaySync = false;
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
                UpdateEngagementMessages();
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
            UpdateEngagementMessages();
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
            CloseEngagementMessage();
            _metaArrivalShown = false;
            if (IsMetaPlanetScene())
                _nextEngagementCategory = (int)EngagementCategory.MetaArrival;
            CancelActiveReminder("scene changed");
            _candidates.Clear();
            _didInitialGameplaySync = false;
            UnsubscribeFromUI();
            UnsubscribeFromGameplay();
            _uiController = null;
            _gameUI = null;
            _gameRules = null;
            _passiveIncome = null;
            _lastShownChest = null;
            ResolveSceneReferences();
            SubscribeToGameplay();
            SetBlockReason(null);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            if (scene != SceneManager.GetActiveScene())
                return;

            _isSceneTransitioning = true;
            CloseEngagementMessage();
            _rewardsManager = null;
            _metaUIController = null;
            _metaGameRules = null;
            CancelActiveReminder("scene unloading");
            _candidates.Clear();
            _didInitialGameplaySync = false;
            UnsubscribeFromUI();
            UnsubscribeFromGameplay();
            _uiController = null;
            _gameUI = null;
            _gameRules = null;
            _passiveIncome = null;
            _lastShownChest = null;
        }

        private void HandleShopVisibilityChanged()
        {
            ResolveSceneReferencesIfNeeded();
            ValidateActiveCandidate();

            if (_activeCandidate?.Type == ReminderType.UnlockAvailable)
                RefreshUnlockHighlight(_activeCandidate);

            RetryReadyCandidates();
        }

        private void HandleShopTabSelected(int tabIndex)
        {
            if (_activeCandidate?.Type == ReminderType.UnlockAvailable)
                ValidateActiveCandidate();

            if (tabIndex == 1)
            {
                HandleUpgradeMenuOpened();
                RetryReadyCandidates();
                return;
            }

            ReminderCandidate pendingUpgrade = FindCandidate(ReminderType.UpgradeMenuIntro, -1);

            if (pendingUpgrade != null && pendingUpgrade != _activeCandidate && pendingUpgrade.TriggeredByMenuOpen)
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

            LogTransition("UpgradeMenuIntro triggered by early menu open");
            RetryReadyCandidates();
        }

        private void HandleMessageShown(MessageData message)
        {
            if (message == null || message.Channel != MessageChannel.Hint ||
                !ReferenceEquals(message.Owner, this))
            {
                return;
            }

            ReminderCandidate candidate = FindCandidate(message.Id);

            if (candidate == null)
                return;

            _activeCandidate = candidate;
            candidate.Shown = true;
            _lastHintShownAt = Time.unscaledTime;
            SetBlockReason(null);

            if (candidate.Type == ReminderType.UnlockAvailable)
                RefreshUnlockHighlight(candidate);

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
                AdvanceTextVariant(candidate.Type, message);
                candidate.DisplayConsumed = true;
                candidate.Ready = false;

                if (candidate.Type == ReminderType.PassiveIncomeChest)
                {
                    _lastShownChest = candidate.Chest;
                }
                else if (candidate.Type == ReminderType.BuyMoreObject)
                {
                    candidate.ShownItemCount = _gameRules.GetItemCount(candidate.Index);
                    candidate.ShownPrice = _gameRules.GetItemUpgradeCost(candidate.Index);
                }

                LogTransition($"{GetCandidateLabel(candidate)} shown index={candidate.Index}");
            }
        }

        private void HandleMessageClosed(MessageData message)
        {
            if (_activeCandidate == null ||
                message == null ||
                message.Channel != MessageChannel.Hint ||
                !ReferenceEquals(message.Owner, this) ||
                message.Id != GetCandidateId(_activeCandidate))
            {
                return;
            }

            if (_displayValidator.IsTutorialRunning || _displayValidator.IsDialogOpen)
            {
                CancelActiveReminder("mandatory tutorial or dialog started");
                return;
            }

            ReminderCandidate closedCandidate = _activeCandidate;
            _activeCandidate = null;
            HideHighlight();

            if (closedCandidate.Type == ReminderType.PassiveIncomeChest)
            {
                _candidates.Remove(closedCandidate);
            }
            else if (closedCandidate.Type != ReminderType.UpgradeMenuIntro)
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

            _rewardsManager = FindFirstObjectByType<RewardsManager>(FindObjectsInactive.Include);
            _metaUIController = FindFirstObjectByType<MetaUIController>();
            _metaGameRules = FindFirstObjectByType<MetaGameRules>();

            if (!IsSuitablePlanetScene())
                return;

            _uiController = FindFirstObjectByType<UIController>();
            _gameUI = FindFirstObjectByType<GameUI>();
            _gameRules = FindFirstObjectByType<GameRules>();
            _passiveIncome = FindFirstObjectByType<PassiveIncome>();
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
                _uiController.OnShopOpened -= HandleShopVisibilityChanged;
                _uiController.OnShopOpened += HandleShopVisibilityChanged;
                _uiController.OnShopClosed -= HandleShopVisibilityChanged;
                _uiController.OnShopClosed += HandleShopVisibilityChanged;
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
                _uiController.OnShopOpened -= HandleShopVisibilityChanged;
                _uiController.OnShopClosed -= HandleShopVisibilityChanged;
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
                   _gameRules.CurrentMoney > _gameRules.GetItemUpgradeCost(index);
        }

        private bool IsAutomationEligible(int index)
        {
            return _gameRules != null &&
                   _gameRules.IsPrepared &&
                   _gameRules.IsItemOwned(index) &&
                   !_gameRules.IsManagerPurchased(index) &&
                   !_gameRules.IsItemPremium(index) &&
                   _gameRules.CurrentMoney >= _gameRules.GetManagerPrice(index);
        }

        private bool IsBuyMoreEligible(int index)
        {
            if (_gameRules == null || !_gameRules.IsPrepared || !_gameRules.IsItemOwned(index))
                return false;

            GameObject itemTarget = _gameUI != null ? _gameUI.GetCreationItemVisualTarget(index) : null;
            return _gameRules.CurrentMoney >= _gameRules.GetItemUpgradeCost(index) &&
                   (itemTarget == null || !itemTarget.TryGetComponent(out ItemController item) ||
                    _gameRules.GetItemCount(index) < item.CreationPurchaseLimit);
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

        private PassiveIncomePop GetAvailablePassiveIncomeChest()
        {
            return _passiveIncome != null && _passiveIncome.gameObject.scene == SceneManager.GetActiveScene()
                ? _passiveIncome.AvailableChest
                : null;
        }

        private void SyncPassiveIncomeChestCandidate()
        {
            if (!_planetTutorialCompleted || _activeCandidate?.Type == ReminderType.PassiveIncomeChest)
                return;

            PassiveIncomePop chest = GetAvailablePassiveIncomeChest();
            if (chest == null || chest == _lastShownChest)
                return;

            ReminderCandidate candidate = GetOrCreateCandidate(ReminderType.PassiveIncomeChest, -1);
            candidate.Chest = chest;
            candidate.Ready = true;
        }

        private bool IsCandidateStillEligible(ReminderCandidate candidate)
        {
            switch (candidate.Type)
            {
                case ReminderType.PassiveIncomeChest:
                    return candidate.Chest != null && candidate.Chest == GetAvailablePassiveIncomeChest() &&
                           (candidate == _activeCandidate || candidate.Chest != _lastShownChest);
                case ReminderType.UnlockAvailable:
                    return IsItemUnlockEligible(candidate.Index);
                case ReminderType.AutomationAvailable:
                    return IsAutomationEligible(candidate.Index);
                case ReminderType.BuyMoreObject:
                    return IsBuyMoreEligible(candidate.Index);
                case ReminderType.UpgradeMenuIntro:
                    bool isCurrentDisplay = candidate == _activeCandidate && candidate.Shown;
                    return (isCurrentDisplay || PlayerPrefs.GetInt(UpgradeMenuIntroShownKey, 0) != 1) &&
                           (!candidate.TriggeredByMenuOpen || IsUpgradeMenuOpen());
                default:
                    return false;
            }
        }

        private void TryShowBestCandidate()
        {
            if (_isApplicationPaused || !_hasApplicationFocus || !IsSuitablePlanetScene())
                return;

            SyncPassiveIncomeChestCandidate();

            if (_activeCandidate != null)
                return;

            string blocker = GetCommonBlockReason(true);
            if (!string.IsNullOrEmpty(blocker))
            {
                SetBlockReason(blocker);
                return;
            }

            for (int i = 0; i < _candidates.Count; i++)
            {
                ReminderCandidate candidate = _candidates[i];
                if (!candidate.Ready || !IsCandidateStillEligible(candidate) ||
                    !TryPrepareCandidate(candidate, out MessageData message))
                {
                    continue;
                }

                if (!_messageManager.TryShowMessageImmediately(message))
                {
                    HideHighlight();
                    SetBlockReason("hint not shown");
                }

                // Keep the candidate ready and recheck its eligibility on the next attempt.
                return;
            }
        }

        private bool TryPrepareCandidate(ReminderCandidate candidate, out MessageData message)
        {
            message = null;
            bool isUnlockHint = candidate.Type == ReminderType.UnlockAvailable;
            if (isUnlockHint && IsCreationTabOpen())
                return false;

            GameObject target = null;
            if (!isUnlockHint && !TryResolveTarget(candidate, out target))
            {
                SetBlockReason(GetTargetBlockReason(candidate));
                return false;
            }

            bool allowShopOpen = candidate.Type != ReminderType.PassiveIncomeChest &&
                                 (candidate.Type != ReminderType.UpgradeMenuIntro || candidate.TriggeredByMenuOpen);
            message = CreateMessage(candidate, allowShopOpen);

            if (_messageManager.IsMessageActiveOrQueued(message.Id))
                return false;

            if (!_messageManager.CanDisplay(message))
            {
                SetBlockReason("message blocked or ToastView busy");
                return false;
            }

            return isUnlockHint || candidate.Type == ReminderType.PassiveIncomeChest ||
                   TryShowHighlight(candidate, target);
        }

        private void RefreshUnlockHighlight(ReminderCandidate candidate)
        {
            GameObject target = IsShopOpen()
                ? _uiController?.CreationButtonTarget
                : _uiController?.ToggleButtonTarget;

            HideHighlight();
            if (IsUsableButton(target))
                TryShowHighlight(candidate, target);
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
            _activeHighlightStep.TargetUI = target;

            if (_highlightSystem == null ||
                !_highlightSystem.Show(target, _activeHighlightStep))
            {
                _activeHighlightStep = null;
                if (candidate.Type != ReminderType.UnlockAvailable)
                    SetBlockReason("highlight unavailable");

                return false;
            }

            _isHighlightVisible = true;
            return true;
        }

        private MessageData CreateMessage(ReminderCandidate candidate, bool allowShopOpen)
        {
            bool isChestHint = candidate.Type == ReminderType.PassiveIncomeChest;
            float displayDuration = Mathf.Max(0.1f, _normalHintDisplayDuration);

            if (isChestHint)
            {
                displayDuration = _passiveIncomeChestDisplayMode == PassiveIncomeChestDisplayMode.UntilFound
                    ? 0f
                    : Mathf.Max(0.1f, _passiveIncomeChestTimedDuration);
            }

            return new MessageData
            {
                Id = GetCandidateId(candidate),
                Owner = this,
                Text = GetCandidateText(candidate.Type),
                Usage = MessageUsage.NormalHint,
                SpeakerId = ProfessorSpeakerId,
                CharacterMood = CharacterMood.Thinking,
                Type = MessageType.Hint,
                Channel = MessageChannel.Hint,
                UseToastView = true,
                Priority = GetPriority(candidate.Type),
                DisplayDuration = displayDuration,
                Lifetime = isChestHint ? 0f : displayDuration,
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
            ReminderCandidate candidate = _activeCandidate;
            if (candidate == null)
                return;

            if (candidate.Type == ReminderType.BuyMoreObject &&
                _gameRules != null && _gameRules.IsPrepared &&
                (candidate.ShownItemCount != _gameRules.GetItemCount(candidate.Index) ||
                 candidate.ShownPrice != _gameRules.GetItemUpgradeCost(candidate.Index)))
            {
                CancelActiveReminder("next copy changed");
                RefreshBuyMoreCandidate(candidate.Index);
                return;
            }

            bool isUnlockHint = candidate.Type == ReminderType.UnlockAvailable;
            GameObject target = null;

            if (!IsSuitablePlanetScene() ||
                !string.IsNullOrEmpty(GetCommonBlockReason(false)) ||
                !IsCandidateStillEligible(candidate) ||
                (!isUnlockHint && !TryResolveTarget(candidate, out target)))
            {
                CancelActiveReminder("purchase, target or blocker became invalid");
                return;
            }

            if (isUnlockHint)
            {
                if (IsCreationTabOpen())
                    CloseCandidateMessage(candidate);
                else if (_isHighlightVisible && !IsUsableButton(_activeHighlightStep.TargetUI))
                    HideHighlight();

                return;
            }

            if (_isHighlightVisible && _activeHighlightStep.TargetUI != target && !TryShowHighlight(candidate, target))
                CancelActiveReminder("highlight target became invalid");
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

            CloseCandidateMessage(completed);

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

            CloseCandidateMessage(cancelled);

            LogTransition($"{GetCandidateLabel(cancelled)} cancelled index={cancelled.Index}: {reason}");
            _isMessageCloseRequested = false;
        }

        private void CloseCandidateMessage(ReminderCandidate candidate)
        {
            MessageData current = _messageManager?.GetCurrentMessage(MessageChannel.Hint);
            if (ReferenceEquals(current?.Owner, this) && current.Id == GetCandidateId(candidate))
                _messageManager.CloseCurrentMessage(MessageChannel.Hint);
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
            _candidates.Sort(CompareCandidates);
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

            if (candidate.Type == ReminderType.PassiveIncomeChest)
            {
                return !_displayValidator.IsShopOpen &&
                       (_uiController == null || (!_uiController.IsShopDisplayed && !_uiController.isLevelsDisplayed));
            }

            if (candidate.Type == ReminderType.UpgradeMenuIntro)
            {
                if (candidate.TriggeredByMenuOpen)
                    return IsUpgradeMenuOpen();

                return _uiController != null && !_uiController.IsShopDisplayed &&
                       !_displayValidator.IsShopOpen && TryResolveUpgradeTarget(out target) &&
                       IsUsableButton(target);
            }

            if (_gameUI == null || _uiController == null)
                return false;

            switch (candidate.Type)
            {
                case ReminderType.AutomationAvailable:
                    if (IsManagerPanelOpen(candidate.Index))
                        target = _gameUI.GetManagerBuyButtonTarget(candidate.Index);
                    else if (IsCreationTabOpen())
                        target = _gameUI.GetManagerButtonTarget(candidate.Index);
                    return IsUsableButton(target);
                case ReminderType.BuyMoreObject:
                    if (!IsCreationTabOpen())
                        return false;
                    target = _gameUI.GetCreationBuyButtonTarget(candidate.Index);
                    return IsUsableButton(target);
                default:
                    return false;
            }
        }

        private static bool IsUsableButton(GameObject target)
        {
            return target != null && target.TryGetComponent(out Button button) &&
                   button.isActiveAndEnabled && button.IsInteractable();
        }

        private string GetTargetBlockReason(ReminderCandidate candidate)
        {
            if (candidate.Type == ReminderType.PassiveIncomeChest)
                return "shop or planet levels panel open";

            if (candidate.Type != ReminderType.UpgradeMenuIntro)
                return "purchase target or required shop tab unavailable";

            return candidate.TriggeredByMenuOpen ? "upgrade menu closed" : "upgrade button unavailable or shop open";
        }

        private bool IsShopOpen()
        {
            return _uiController != null ? _uiController.IsShopDisplayed : _displayValidator.IsShopOpen;
        }

        private bool IsCreationTabOpen()
        {
            return _uiController != null &&
                   _uiController.IsShopDisplayed &&
                   _uiController.SelectedShopTab == 0;
        }

        private bool IsUpgradeMenuOpen()
        {
            return _uiController != null &&
                   _uiController.IsShopDisplayed &&
                   _uiController.SelectedShopTab == 1;
        }

        private bool IsManagerPanelOpen(int index)
        {
            GameObject target = _gameUI != null ? _gameUI.GetManagerBuyButtonTarget(index) : null;
            return target != null && target.activeInHierarchy;
        }

        private string GetCommonBlockReason(bool includeCooldown, bool engagement = false)
        {
            if (_isSceneTransitioning)
                return "scene transition";

            if (!engagement && !_planetTutorialCompleted)
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
                (engagement ? !IsEngagementMessage(currentHint) :
                    _activeCandidate == null || currentHint.Id != GetCandidateId(_activeCandidate)))
            {
                return "another Hint message active";
            }

            if (includeCooldown && !CanUseGlobalCooldown())
                return "global cooldown";

            return null;
        }

        private void UpdateEngagementMessages()
        {
            bool isMetaPlanet = IsMetaPlanetScene();
            if (isMetaPlanet && (_metaUIController == null || _metaGameRules == null))
                ResolveSceneReferences();

            bool sceneReady = isMetaPlanet
                ? _metaUIController != null && _metaUIController.isActiveAndEnabled &&
                  _metaGameRules != null && _metaGameRules.IsPrepared &&
                  !MetaPlanetTutorialController.ShouldStartTutorial()
                : IsSuitablePlanetScene() && _planetTutorialCompleted &&
                  _gameRules != null && _gameRules.IsPrepared &&
                  _uiController != null && _uiController.isActiveAndEnabled;

            if (!_enableEngagementMessages || !sceneReady || Time.timeScale <= 0f ||
                (PlanetLoadingScreen.Instance != null && PlanetLoadingScreen.Instance.IsShowing) ||
                !string.IsNullOrEmpty(GetCommonBlockReason(false, engagement: true)) ||
                _displayValidator.IsShopOpen ||
                (_uiController != null && (_uiController.IsShopDisplayed || _uiController.isLevelsDisplayed)) ||
                (_metaUIController != null && (_metaUIController.isDisplayed || _metaUIController.isLevelsDisplayed)))
            {
                CloseEngagementMessage();
                return;
            }

            MessageData current = _messageManager.GetCurrentMessage(MessageChannel.Hint);
            if (IsEngagementMessage(current))
            {
                for (int i = 0; i < EngagementMessages.Length; i++)
                {
                    if (current.Id == GetEngagementMessageId(i) &&
                        !IsEngagementCategoryAvailable((EngagementCategory)i, isActive: true))
                    {
                        CloseEngagementMessage();
                        break;
                    }
                }
                return;
            }

            if (_messageManager.CurrentMessage != null || _messageDisplayDuration <= 0f)
                return;

            float delay = Mathf.Max(0f, _hasShownEngagementMessage ? _messageInterval : _firstMessageDelay);
            // Only free gameplay advances this clock; scene changes keep the accumulated time.
            _engagementElapsed = Mathf.Min(delay, _engagementElapsed + Time.unscaledDeltaTime);
            if (_engagementElapsed < delay)
                return;

            for (int offset = 0; offset < EngagementMessages.Length; offset++)
            {
                int index = (_nextEngagementCategory + offset) % EngagementMessages.Length;
                if (!IsEngagementCategoryAvailable((EngagementCategory)index))
                    continue;

                var definition = EngagementMessages[index];
                string key = $"{definition.Prefix}_{_nextEngagementVariants[index] + 1:00}";
                string text = GetLocalizedText(key, null);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                MessageData message = new()
                {
                    Id = GetEngagementMessageId(index),
                    Owner = this,
                    Text = text,
                    Usage = definition.Usage,
                    SpeakerId = ProfessorSpeakerId,
                    CharacterMood = CharacterMood.Thinking,
                    Type = MessageType.Hint,
                    Channel = MessageChannel.Hint,
                    UseToastView = true,
                    DisplayDuration = _messageDisplayDuration,
                    Cooldown = Mathf.Max(0f, _categoryCooldown),
                    CanInterrupt = false,
                    CanDisplayOverModal = false,
                    CanDisplayOverShop = false,
                    UseTypingAnimation = true,
                    TypingSpeed = 30f,
                    CanSkipTyping = true
                };

                // The manager owns category cooldowns and the shared ToastView lock.
                if (!_messageManager.TryShowMessageImmediately(message))
                    continue;

                _nextEngagementVariants[index] = (_nextEngagementVariants[index] + 1) % definition.Count;
                _nextEngagementCategory = (index + 1) % EngagementMessages.Length;
                _engagementElapsed = 0f;
                _hasShownEngagementMessage = true;
                if ((EngagementCategory)index == EngagementCategory.MetaArrival)
                    _metaArrivalShown = true;
                return;
            }
        }

        private bool IsEngagementCategoryAvailable(EngagementCategory category, bool isActive = false)
        {
            bool isMetaPlanet = IsMetaPlanetScene();
            switch (category)
            {
                case EngagementCategory.AdsX2:
                    return _enableAdsX2Messages && !isMetaPlanet && IsSuitablePlanetScene() &&
                           _rewardsManager != null && _rewardsManager.IsBoosterRewardAvailable;
                case EngagementCategory.AdsDiamonds:
                    return _enableAdsDiamondsMessages && !isMetaPlanet && IsSuitablePlanetScene() &&
                           _rewardsManager != null && _rewardsManager.IsDiamondsRewardAvailable;
                case EngagementCategory.MetaVisit:
                    return _enableMetaVisitMessages && !isMetaPlanet && IsSuitablePlanetScene();
                case EngagementCategory.MetaArrival:
                    return _enableMetaArrivalMessages && isMetaPlanet && (!_metaArrivalShown || isActive);
                case EngagementCategory.Store:
                    return _enableStoreMessages && (isMetaPlanet
                        ? _metaUIController != null && _metaUIController.isActiveAndEnabled &&
                          IsUsableButton(_metaUIController.ToggleButtonGameObject) &&
                          IsUsableButton(_metaUIController.ShopButtonTarget)
                        : _uiController != null && _uiController.isActiveAndEnabled &&
                          IsUsableButton(_uiController.ToggleButtonTarget) &&
                          IsUsableButton(_uiController.ShopButtonTarget));
                default:
                    return false;
            }
        }

        private bool IsEngagementMessage(MessageData message)
        {
            return ReferenceEquals(message?.Owner, this) &&
                   message.Id?.StartsWith(EngagementMessageIdPrefix, StringComparison.Ordinal) == true;
        }

        private void CloseEngagementMessage()
        {
            if (_messageManager != null &&
                IsEngagementMessage(_messageManager.GetCurrentMessage(MessageChannel.Hint)))
            {
                _messageManager.CloseCurrentMessage(MessageChannel.Hint);
            }
        }

        private static string GetEngagementMessageId(int index)
        {
            return EngagementMessageIdPrefix + EngagementMessages[index].Prefix;
        }

        private static bool IsMetaPlanetScene()
        {
            return SceneManager.GetActiveScene().name == "PlanetMeta";
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
            if (_isHighlightVisible)
            {
                if (_highlightSystem != null)
                    _highlightSystem.HideIfShowing(_activeHighlightStep);

                LogTransition("highlight cleanup");
            }

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
                case ReminderType.PassiveIncomeChest:
                    return 0;
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

        private string GetCandidateText(ReminderType type)
        {
            switch (type)
            {
                case ReminderType.PassiveIncomeChest:
                    return GetVariantText(type, PassiveIncomeChestTextKey, PassiveIncomeChestFallbackText);
                case ReminderType.UnlockAvailable:
                    return GetVariantText(type, UnlockAvailableTextKey, UnlockAvailableText);
                case ReminderType.AutomationAvailable:
                    return GetVariantText(type, AutomationAvailableTextKey, AutomationAvailableText);
                case ReminderType.BuyMoreObject:
                    return GetVariantText(type, BuyMoreObjectTextKey, BuyMoreObjectText);
                default:
                    return GetLocalizedText(UpgradeMenuIntroTextKey, UpgradeMenuIntroText);
            }
        }

        private string GetVariantText(ReminderType type, string baseKey, string fallback)
        {
            _nextTextVariants.TryGetValue(type, out int variant);
            string key = variant == 0 ? baseKey : $"{baseKey}_{variant + 1}";
            return GetLocalizedText(key, fallback);
        }

        private void AdvanceTextVariant(ReminderType type, MessageData message)
        {
            foreach (ToastView view in FindObjectsByType<ToastView>(FindObjectsSortMode.None))
            {
                MessageData shown = view.DisplayedMessage;
                if (!view.isActiveAndEnabled || shown == null ||
                    !ReferenceEquals(shown.Owner, this) || shown.Id != message.Id)
                {
                    continue;
                }

                _nextTextVariants.TryGetValue(type, out int variant);
                _nextTextVariants[type] = (variant + 1) % 3;
                return;
            }
        }

        private static string GetLocalizedText(string key, string fallback)
        {
            string text = LeanLocalization.GetTranslationText(key, fallback);
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string GetCandidateId(ReminderCandidate candidate)
        {
            switch (candidate.Type)
            {
                case ReminderType.PassiveIncomeChest:
                    return PassiveIncomeChestId;
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

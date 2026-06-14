using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetBuilder.Messages.Tutorial
{
    public class TutorialManager : MonoBehaviour
    {
        private const string DefaultProgressKey = "MessagesTutorialCurrentStepId";
        private const string DefaultCompletedKey = "MessagesTutorialCompleted";

        [Header("Steps")]
        [SerializeField] private List<TutorialStepData> _steps = new();
        [SerializeField] private bool _startAutomatically = true;

        [Header("Persistence")]
        [SerializeField] private string _progressKey = DefaultProgressKey;
        [SerializeField] private string _completedKey = DefaultCompletedKey;

        [Header("Message")]
        [SerializeField] private int _messagePriority = 100;
        [SerializeField] private bool _useTypingAnimation = true;
        [SerializeField] private float _typingSpeed = 30f;
        [SerializeField] private bool _canSkipTyping = true;

        [Header("Interaction Overlay")]
        [SerializeField] private TutorialInteractionOverlay _interactionOverlay;
        [SerializeField] private TutorialHighlightSystem _highlightSystem;

        private int _currentStepIndex = -1;
        private Button _targetButton;
        private TutorialTargetClickRelay _targetClickRelay;

        public TutorialStepData CurrentStep =>
            _steps != null && _currentStepIndex >= 0 && _currentStepIndex < _steps.Count
                ? _steps[_currentStepIndex]
                : null;

        public bool IsCompleted { get; private set; }
        public bool IsRunning { get; private set; }

        public event Action<TutorialStepData> OnStepStarted;
        public event Action<TutorialStepData> OnStepCompleted;
        public event Action OnTutorialCompleted;

        public void Configure(
            List<TutorialStepData> steps,
            string progressKey,
            string completedKey,
            bool startAutomatically = false)
        {
            if (IsRunning)
            {
                Debug.LogWarning("TutorialManager cannot be configured while it is running.", this);
                return;
            }

            _steps = steps ?? new List<TutorialStepData>();
            _progressKey = progressKey;
            _completedKey = completedKey;
            _startAutomatically = startAutomatically;
        }

        public void SetStartAutomatically(bool startAutomatically)
        {
            if (IsRunning)
                return;

            _startAutomatically = startAutomatically;
        }

        public void SetTypingSettings(
            bool useTypingAnimation,
            float typingSpeed,
            bool canSkipTyping)
        {
            _useTypingAnimation = useTypingAnimation;
            _typingSpeed = typingSpeed;
            _canSkipTyping = canSkipTyping;
        }

        private void Start()
        {
            if (_startAutomatically)
                StartTutorial();
        }

        private void OnDisable()
        {
            CleanupActiveTutorial();
        }

        private void OnDestroy()
        {
            CleanupActiveTutorial();
        }

        private void OnValidate()
        {
            if (_interactionOverlay == null)
                Debug.LogWarning("TutorialManager requires a TutorialInteractionOverlay.", this);

            if (_steps == null)
            {
                Debug.LogWarning("TutorialManager steps list is missing.", this);
                return;
            }

            HashSet<string> stepIds = new();

            for (int i = 0; i < _steps.Count; i++)
            {
                TutorialStepData step = _steps[i];

                if (step == null || string.IsNullOrEmpty(step.StepId))
                {
                    Debug.LogWarning($"Tutorial step at index {i} requires a StepId.", this);
                    continue;
                }

                if (!stepIds.Add(step.StepId))
                    Debug.LogWarning($"Duplicate tutorial StepId: {step.StepId}", this);

                if (step.CompletionCondition != TutorialCompletionCondition.TargetUIClicked)
                    continue;

                if (step.TargetUI == null)
                {
                    Debug.LogWarning($"Tutorial step '{step.StepId}' requires TargetUI.", this);
                    continue;
                }

                if (step.TargetUI.GetComponent<Button>() == null &&
                    step.TargetUI.GetComponent<TutorialTargetClickRelay>() == null)
                {
                    Debug.LogWarning(
                        $"Tutorial step '{step.StepId}' TargetUI requires a Button or TutorialTargetClickRelay.",
                        this);
                }
            }
        }

        public void StartTutorial()
        {
            if (IsRunning)
            {
                Debug.LogWarning("TutorialManager is already running.", this);
                return;
            }

            DisconnectTargetUI();

            if (!HasSteps() || !HasValidSteps())
                return;

            LoadProgress();

            if (IsCompleted)
                return;

            IsRunning = true;
            SetValidationState(true);
            SaveProgress();
            ShowCurrentStep();
        }

        public void RestartTutorial()
        {
            Debug.Log(
                $"[MetaTutorialTrace] TutorialManager.RestartTutorial ENTER isRunning={IsRunning} steps={(_steps != null ? _steps.Count : -1)} currentStep={CurrentStep?.StepId ?? "null"}",
                this);

            if (IsRunning)
            {
                Debug.LogWarning("TutorialManager cannot restart while it is running.", this);
                Debug.Log("[MetaTutorialTrace] TutorialManager.RestartTutorial STOP IsRunning", this);
                return;
            }

            if (!HasSteps() || !HasValidSteps())
            {
                Debug.Log("[MetaTutorialTrace] TutorialManager.RestartTutorial STOP invalid steps", this);
                return;
            }

            DisconnectTargetUI();
            IsCompleted = false;
            _currentStepIndex = 0;
            IsRunning = true;
            SetValidationState(true);
            SaveProgress();
            ShowCurrentStep();
        }

        public void CancelTutorial()
        {
            CleanupActiveTutorial();
        }

        public void NotifyConditionCompleted(TutorialCompletionCondition condition)
        {
            if (!IsRunning)
                return;

            TutorialStepData step = CurrentStep;

            if (step == null || step.CompletionCondition != condition)
                return;

            CompleteCurrentStep();
        }

        public void CompleteManualStep()
        {
            NotifyConditionCompleted(TutorialCompletionCondition.Manual);
        }

        private void ShowCurrentStep()
        {
            Debug.Log(
                $"[MetaTutorialTrace] TutorialManager.ShowCurrentStep ENTER isRunning={IsRunning} currentIndex={_currentStepIndex} steps={(_steps != null ? _steps.Count : -1)} currentStep={CurrentStep?.StepId ?? "null"}",
                this);

            if (!IsRunning)
            {
                Debug.Log("[MetaTutorialTrace] TutorialManager.ShowCurrentStep STOP not running", this);
                return;
            }

            if (_currentStepIndex >= _steps.Count)
            {
                Debug.Log("[MetaTutorialTrace] TutorialManager.ShowCurrentStep COMPLETE tutorial", this);
                CompleteTutorial();
                return;
            }

            TutorialStepData step = CurrentStep;

            if (step == null)
            {
                StopWithWarning($"Tutorial step at index {_currentStepIndex} is null.");
                Debug.Log("[MetaTutorialTrace] TutorialManager.ShowCurrentStep STOP null step", this);
                return;
            }

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null)
            {
                StopWithWarning("TutorialManager requires an active MessageManager.");
                Debug.Log("[MetaTutorialTrace] TutorialManager.ShowCurrentStep STOP no MessageManager", this);
                return;
            }

            if (!TryConnectTargetUI(step))
            {
                Debug.Log($"[MetaTutorialTrace] TutorialManager.ShowCurrentStep STOP TryConnectTargetUI step={step.StepId}", this);
                return;
            }

            if (!TryShowInteractionOverlay(step))
            {
                Debug.Log($"[MetaTutorialTrace] TutorialManager.ShowCurrentStep STOP TryShowInteractionOverlay step={step.StepId}", this);
                return;
            }

            if (!TryShowHighlight(step))
            {
                Debug.Log($"[MetaTutorialTrace] TutorialManager.ShowCurrentStep STOP TryShowHighlight step={step.StepId}", this);
                return;
            }

            messageManager.ShowMessage(CreateMessage(step));
            Debug.Log($"[MetaTutorialTrace] TutorialManager.ShowCurrentStep SHOWN step={step.StepId}", this);
            OnStepStarted?.Invoke(step);
        }

        private void CompleteCurrentStep()
        {
            TutorialStepData completedStep = CurrentStep;

            if (completedStep == null)
                return;

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.MarkCurrentMessageCompleted(MessageChannel.Tutorial);

            DisconnectTargetUI();
            HideInteractionOverlay();
            HideHighlight();
            OnStepCompleted?.Invoke(completedStep);

            _currentStepIndex++;
            SaveProgress();

            if (messageManager != null)
                messageManager.CloseCurrentMessage(MessageChannel.Tutorial);

            ShowCurrentStep();
        }

        private void CompleteTutorial()
        {
            DisconnectTargetUI();
            HideInteractionOverlay();
            HideHighlight();
            IsRunning = false;
            SetValidationState(false);
            IsCompleted = true;
            SaveProgress();
            OnTutorialCompleted?.Invoke();
        }

        private bool TryConnectTargetUI(TutorialStepData step)
        {
            DisconnectTargetUI();

            if (step.CompletionCondition != TutorialCompletionCondition.TargetUIClicked)
                return true;

            if (step.TargetUI == null)
            {
                StopWithWarning($"Tutorial step '{step.StepId}' requires TargetUI.");
                return false;
            }

            _targetButton = step.TargetUI.GetComponent<Button>();

            if (_targetButton != null)
            {
                _targetButton.onClick.AddListener(HandleTargetUIClicked);
                return true;
            }

            _targetClickRelay = step.TargetUI.GetComponent<TutorialTargetClickRelay>();

            if (_targetClickRelay != null)
            {
                _targetClickRelay.OnClicked += HandleTargetUIClicked;
                return true;
            }

            StopWithWarning($"Tutorial step '{step.StepId}' TargetUI requires a Button or TutorialTargetClickRelay.");
            return false;
        }

        private void DisconnectTargetUI()
        {
            if (_targetButton != null)
            {
                _targetButton.onClick.RemoveListener(HandleTargetUIClicked);
                _targetButton = null;
            }

            if (_targetClickRelay != null)
            {
                _targetClickRelay.OnClicked -= HandleTargetUIClicked;
                _targetClickRelay = null;
            }
        }

        private bool TryShowInteractionOverlay(TutorialStepData step)
        {
            if (_interactionOverlay == null)
            {
                StopWithWarning("TutorialManager requires a TutorialInteractionOverlay.");
                return false;
            }

            if (_interactionOverlay.Show(step.TargetUI, !step.DisableInteractionCutout))
                return true;

            StopWithWarning($"Tutorial step '{step.StepId}' could not configure its interaction overlay.");
            return false;
        }

        private void HideInteractionOverlay()
        {
            if (_interactionOverlay != null)
                _interactionOverlay.Hide();
        }

        private bool TryShowHighlight(TutorialStepData step)
        {
            if (!step.EnableHighlight && !step.ShowArrow)
                return true;

            TutorialHighlightSystem highlightSystem = GetHighlightSystem();

            if (highlightSystem == null)
            {
                StopWithWarning("TutorialManager requires a TutorialHighlightSystem for highlighted steps.");
                return false;
            }

            if (highlightSystem.Show(step.TargetUI, step))
                return true;

            StopWithWarning($"Tutorial step '{step.StepId}' could not configure its highlight.");
            return false;
        }

        private void HideHighlight()
        {
            TutorialHighlightSystem highlightSystem = GetHighlightSystem();

            if (highlightSystem != null)
                highlightSystem.Hide();
        }

        private TutorialHighlightSystem GetHighlightSystem()
        {
            if (_highlightSystem == null && _interactionOverlay != null)
                _highlightSystem = _interactionOverlay.GetComponent<TutorialHighlightSystem>();

            return _highlightSystem;
        }

        private void HandleTargetUIClicked()
        {
            NotifyConditionCompleted(TutorialCompletionCondition.TargetUIClicked);
        }

        private bool HasSteps()
        {
            if (_steps != null && _steps.Count > 0)
                return true;

            StopWithWarning("TutorialManager requires at least one tutorial step.");
            return false;
        }

        private bool HasValidSteps()
        {
            HashSet<string> stepIds = new();

            for (int i = 0; i < _steps.Count; i++)
            {
                TutorialStepData step = _steps[i];

                if (step == null)
                {
                    StopWithWarning($"Tutorial step at index {i} is null.");
                    return false;
                }

                if (string.IsNullOrEmpty(step.StepId))
                {
                    StopWithWarning($"Tutorial step at index {i} requires a StepId.");
                    return false;
                }

                if (!stepIds.Add(step.StepId))
                {
                    StopWithWarning($"Duplicate tutorial StepId: {step.StepId}");
                    return false;
                }
            }

            return true;
        }

        private void StopWithWarning(string message)
        {
            CleanupActiveTutorial();
            Debug.LogWarning(message, this);
        }

        private void CleanupActiveTutorial()
        {
            DisconnectTargetUI();
            HideInteractionOverlay();
            HideHighlight();

            if (IsRunning)
            {
                MessageManager messageManager = MessageManager.Instance;

                if (messageManager != null)
                    messageManager.CloseCurrentMessage(MessageChannel.Tutorial);
            }

            IsRunning = false;
            SetValidationState(false);
        }

        private static void SetValidationState(bool isRunning)
        {
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.SetTutorialRunning(isRunning);
        }

        private MessageData CreateMessage(TutorialStepData step)
        {
            return new MessageData
            {
                Id = $"Tutorial.{step.StepId}",
                Text = step.MessageText,
                SpeakerId = step.SpeakerId,
                CharacterMood = step.CharacterMood,
                Type = MessageType.Tutorial,
                Channel = MessageChannel.Tutorial,
                Priority = _messagePriority,
                CanInterrupt = true,
                CanDisplayOverModal = true,
                UseTypingAnimation = _useTypingAnimation,
                TypingSpeed = _typingSpeed,
                CanSkipTyping = _canSkipTyping
            };
        }

        private void LoadProgress()
        {
            IsCompleted = PlayerPrefs.GetInt(GetCompletedKey(), 0) == 1;

            if (IsCompleted)
            {
                _currentStepIndex = _steps?.Count ?? 0;
                return;
            }

            string savedStepId = PlayerPrefs.GetString(GetProgressKey(), string.Empty);
            _currentStepIndex = FindStepIndex(savedStepId);
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetInt(GetCompletedKey(), IsCompleted ? 1 : 0);

            TutorialStepData step = CurrentStep;

            if (step != null && !string.IsNullOrEmpty(step.StepId))
                PlayerPrefs.SetString(GetProgressKey(), step.StepId);
            else
                PlayerPrefs.DeleteKey(GetProgressKey());

            PlayerPrefs.Save();
        }

        private int FindStepIndex(string stepId)
        {
            if (string.IsNullOrEmpty(stepId))
                return 0;

            if (_steps == null)
                return 0;

            for (int i = 0; i < _steps.Count; i++)
            {
                if (_steps[i] != null && _steps[i].StepId == stepId)
                    return i;
            }

            Debug.LogWarning($"Saved tutorial step '{stepId}' was not found. Starting from the first step.", this);
            return 0;
        }

        private string GetProgressKey()
        {
            return string.IsNullOrEmpty(_progressKey) ? DefaultProgressKey : _progressKey;
        }

        private string GetCompletedKey()
        {
            return string.IsNullOrEmpty(_completedKey) ? DefaultCompletedKey : _completedKey;
        }
    }
}

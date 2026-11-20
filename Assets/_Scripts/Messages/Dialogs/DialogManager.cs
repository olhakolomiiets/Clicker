using System;
using PlanetBuilder.Messages;
using PlanetBuilder.Messages.Tutorial;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetBuilder.Messages.Dialogs
{
    public class DialogManager : MonoBehaviour
    {
        [SerializeField] private Button _nextButton;
        private Button _overlayButton;
        [SerializeField] private TutorialInteractionOverlay _interactionOverlay;

        private const float MetaNextCooldown = 0.3f;
        private float _lastMetaNextTime = float.NegativeInfinity;

        [Header("Message")]
        [SerializeField] private int _priority = 100;
        [SerializeField] private bool _useTypingAnimation = true;
        [SerializeField] private float _typingSpeed = 30f;
        [SerializeField] private bool _canSkipTyping = true;

        private DialogData _activeDialog;
        private int _currentLineIndex = -1;
        private MessageManager _subscribedMessageManager;

        public bool IsRunning => _activeDialog != null;
        public DialogData ActiveDialog => _activeDialog;
        public int CurrentLineIndex => _currentLineIndex;

        internal bool IsMetaDialog => _activeDialog?.Owner is MetaPlanetTutorialController;

        private bool IsBlockingMetaDialog => IsMetaDialog &&
            _interactionOverlay != null &&
            _interactionOverlay.IsBlockingMetaDialogOwnedBy(_activeDialog.Owner);

        private bool CanAdvanceOverlayDialog
        {
            get
            {
                if (!isActiveAndEnabled || !IsBlockingMetaDialog)
                    return false;

                MessageData message = MessageManager.Instance?.GetCurrentMessage(MessageChannel.Dialog);
                return message != null && ReferenceEquals(message.Owner, _activeDialog) &&
                    message.Id == GetCurrentMessageId();
            }
        }

        public event Action<DialogData> OnDialogStarted;
        public event Action<DialogData> OnDialogCompleted;
        public event Action<DialogData> OnDialogCancelled;
        public event Action<DialogLine> OnLineShown;

        public void SetTypingSettings(
            bool useTypingAnimation,
            float typingSpeed,
            bool canSkipTyping)
        {
            _useTypingAnimation = useTypingAnimation;
            _typingSpeed = typingSpeed;
            _canSkipTyping = canSkipTyping;
        }

        private void OnEnable()
        {
            if (_interactionOverlay == null)
                _interactionOverlay = GetComponentInChildren<TutorialInteractionOverlay>(true);

            _overlayButton = _interactionOverlay != null ? _interactionOverlay.GetComponent<Button>() : null;
            if (_overlayButton != null)
                _overlayButton.interactable = false;

            if (_nextButton != null)
                _nextButton.onClick.AddListener(ShowNext);

            SubscribeToMessages(MessageManager.Instance);
        }

        private void LateUpdate()
        {
            if (_overlayButton != null)
                _overlayButton.interactable = CanAdvanceOverlayDialog &&
                    Time.unscaledTime - _lastMetaNextTime >= MetaNextCooldown;
        }

        internal void ShowNextFromOverlay()
        {
            if (_overlayButton != null && _overlayButton.isActiveAndEnabled &&
                _overlayButton.IsInteractable() && CanAdvanceOverlayDialog)
            {
                ShowNext();
            }
        }

        private void OnDisable()
        {
            if (_overlayButton != null)
                _overlayButton.interactable = false;

            if (_nextButton != null)
                _nextButton.onClick.RemoveListener(ShowNext);

            SubscribeToMessages(null);

            CancelDialog();
        }

        private void OnValidate()
        {
            if (_nextButton == null)
                Debug.LogWarning("DialogManager requires a Next button.", this);
        }

        public bool StartDialog(DialogData dialog)
        {
            if (IsRunning)
            {
                Debug.LogWarning("DialogManager is already running a dialog.", this);
                return false;
            }

            if (!IsValid(dialog))
                return false;

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null)
            {
                Debug.LogWarning("DialogManager requires an active MessageManager.", this);
                return false;
            }

            SubscribeToMessages(messageManager);

            if (messageManager.GetCurrentMessage(MessageChannel.Dialog) != null)
            {
                Debug.LogWarning("DialogManager cannot start while another dialog message is active.", this);
                return false;
            }

            _activeDialog = dialog;
            _currentLineIndex = 0;
            messageManager.SetDialogOpen(true);
            if (!ReferenceEquals(_activeDialog, dialog))
                return false;

            OnDialogStarted?.Invoke(dialog);
            if (!ReferenceEquals(_activeDialog, dialog))
                return false;

            ShowCurrentLine(messageManager);
            return true;
        }

        public void ShowNext()
        {
            if (!IsRunning)
                return;

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null &&
                ReferenceEquals(messageManager.GetCurrentMessage(MessageChannel.Dialog)?.Owner, _activeDialog))
            {
                if (IsMetaDialog)
                {
                    if (Time.unscaledTime - _lastMetaNextTime < MetaNextCooldown)
                        return;

                    _lastMetaNextTime = Time.unscaledTime;
                }

                messageManager.AdvanceCurrentMessage(MessageChannel.Dialog);
            }
        }

        public void CancelDialog()
        {
            DialogData cancelledDialog = _activeDialog;
            if (cancelledDialog == null)
                return;

            _activeDialog = null;
            _currentLineIndex = -1;

            MessageManager messageManager = MessageManager.Instance;
            if (messageManager != null)
            {
                messageManager.SetDialogOpen(false);
                messageManager.DiscardMessagesOwnedBy(MessageChannel.Dialog, cancelledDialog);
            }

            OnDialogCancelled?.Invoke(cancelledDialog);
        }

        private void HandleMessageClosed(MessageData message)
        {
            MessageTutorialTrace.LogHide(
                "DialogManager.HandleMessageClosed",
                gameObject,
                message != null
                    ? $"messageId={message.Id} dialogId={_activeDialog?.DialogId ?? "null"} isRunning={IsRunning}"
                    : $"message=null dialogId={_activeDialog?.DialogId ?? "null"} isRunning={IsRunning}");

            if (!IsRunning ||
                message == null ||
                message.Channel != MessageChannel.Dialog ||
                !ReferenceEquals(message.Owner, _activeDialog) ||
                message.Id != GetCurrentMessageId())
            {
                return;
            }

            _currentLineIndex++;

            if (_currentLineIndex >= _activeDialog.ListDialogLine.Count)
            {
                CompleteDialog();
                return;
            }

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                ShowCurrentLine(messageManager);
            else
                CancelDialog();
        }

        private void ShowCurrentLine(MessageManager messageManager)
        {
            DialogLine line = _activeDialog.ListDialogLine[_currentLineIndex];

            messageManager.ShowMessage(new MessageData
            {
                Id = GetCurrentMessageId(),
                Text = line.Text,
                SpeakerId = line.SpeakerId,
                CharacterMood = line.CharacterMood,
                Type = MessageType.Dialog,
                Channel = MessageChannel.Dialog,
                Owner = _activeDialog,
                VideoView = _activeDialog.VideoView,
                Priority = _priority,
                CanInterrupt = false,
                CanDisplayOverModal = true,
                UseTypingAnimation = _useTypingAnimation,
                TypingSpeed = _typingSpeed,
                CanSkipTyping = _canSkipTyping
            });

            OnLineShown?.Invoke(line);
        }

        private void CompleteDialog()
        {
            DialogData completedDialog = _activeDialog;
            _activeDialog = null;
            _currentLineIndex = -1;
            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.SetDialogOpen(false);

            OnDialogCompleted?.Invoke(completedDialog);
        }

        private void SubscribeToMessages(MessageManager messageManager)
        {
            if (_subscribedMessageManager == messageManager)
                return;

            if (_subscribedMessageManager != null)
                _subscribedMessageManager.OnMessageClosed -= HandleMessageClosed;

            _subscribedMessageManager = messageManager;

            if (_subscribedMessageManager != null)
                _subscribedMessageManager.OnMessageClosed += HandleMessageClosed;
        }

        private string GetCurrentMessageId()
        {
            return IsRunning
                ? $"Dialog.{_activeDialog.DialogId}.{_currentLineIndex}"
                : null;
        }

        private bool IsValid(DialogData dialog)
        {
            if (dialog == null)
            {
                Debug.LogWarning("DialogManager requires DialogData.", this);
                return false;
            }

            if (string.IsNullOrEmpty(dialog.DialogId))
            {
                Debug.LogWarning("DialogData requires DialogId.", this);
                return false;
            }

            if (dialog.ListDialogLine == null || dialog.ListDialogLine.Count == 0)
            {
                Debug.LogWarning($"Dialog '{dialog.DialogId}' requires at least one line.", this);
                return false;
            }

            for (int i = 0; i < dialog.ListDialogLine.Count; i++)
            {
                if (dialog.ListDialogLine[i] != null)
                    continue;

                Debug.LogWarning($"Dialog '{dialog.DialogId}' line at index {i} is null.", this);
                return false;
            }

            return true;
        }
    }
}

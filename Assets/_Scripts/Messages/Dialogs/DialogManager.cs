using System;
using PlanetBuilder.Messages;
using UnityEngine;
using UnityEngine.UI;

namespace PlanetBuilder.Messages.Dialogs
{
    public class DialogManager : MonoBehaviour
    {
        [SerializeField] private Button _nextButton;

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

        public event Action<DialogData> OnDialogStarted;
        public event Action<DialogData> OnDialogCompleted;
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
            if (_nextButton != null)
                _nextButton.onClick.AddListener(ShowNext);

            SubscribeToMessages(MessageManager.Instance);
        }

        private void OnDisable()
        {
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
            OnDialogStarted?.Invoke(dialog);
            ShowCurrentLine(messageManager);
            return true;
        }

        public void ShowNext()
        {
            if (!IsRunning)
                return;

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager != null)
                messageManager.AdvanceCurrentMessage(MessageChannel.Dialog);
        }

        public void CancelDialog()
        {
            if (!IsRunning)
                return;

            MessageManager messageManager = MessageManager.Instance;
            string currentMessageId = GetCurrentMessageId();

            _activeDialog = null;
            _currentLineIndex = -1;

            if (messageManager != null)
                messageManager.SetDialogOpen(false);

            if (messageManager == null)
                return;

            MessageData currentMessage = messageManager.GetCurrentMessage(MessageChannel.Dialog);

            if (currentMessage != null && currentMessage.Id == currentMessageId)
                messageManager.CloseCurrentMessage(MessageChannel.Dialog);
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
                Priority = _priority,
                CanInterrupt = false,
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

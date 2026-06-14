using System;
using System.Collections.Generic;
using PlanetBuilder.Messages.UI;
using UnityEngine;

namespace PlanetBuilder.Messages
{
    public class MessageManager : MonoBehaviour
    {
        private const float CooldownCleanupInterval = 30f;

        private static readonly MessageChannel[] Channels =
        {
            MessageChannel.Tutorial,
            MessageChannel.Dialog,
            MessageChannel.Hint,
            MessageChannel.Toast
        };

        private static MessageManager _instance;

        private readonly MessageQueueManager _queueManager = new();
        private readonly Dictionary<MessageChannel, ActiveMessage> _activeMessages = new();
        private readonly Dictionary<string, float> _cooldownEndTimes = new();
        private readonly List<string> _expiredCooldownIds = new();
        private readonly HashSet<MessageChannel> _reservedChannels = new();
        private readonly HashSet<string> _reservedMessageIds = new();
        private readonly List<DeferredMessage> _deferredMessages = new();
        private readonly HashSet<string> _deferredMessageIds = new();
        private readonly List<ScheduledReminder> _scheduledReminders = new();
        private readonly HashSet<string> _scheduledReminderIds = new();
        private readonly MessageDisplayValidator _displayValidator = new();
        private readonly MessageAnalyticsService _analyticsService = new();

        [Header("Views")]
        [SerializeField] private MessageView _messageView;
        [SerializeField] private CharacterBubbleView _characterBubbleView;
        [SerializeField] private ToastView _toastView;
        [SerializeField] private TutorialView _tutorialView;

        private float _nextCooldownCleanupTime;
        private bool _isProcessingDeferredMessages;

        public static MessageManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = FindFirstObjectByType<MessageManager>();

                return _instance;
            }
        }

        public static bool HasInstance => Instance != null;

        public MessageData CurrentMessage => GetHighestPriorityActiveMessage()?.CreateSnapshot();
        public MessageQueueManager QueueManager => _queueManager;
        public MessageDisplayValidator DisplayValidator => _displayValidator;
        public MessageAnalyticsService AnalyticsService => _analyticsService;

        public event Action<MessageData> OnMessageShown;
        public event Action<MessageData> OnMessageClosed;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            float currentTime = Time.unscaledTime;

            for (int i = 0; i < Channels.Length; i++)
            {
                MessageChannel channel = Channels[i];

                if (_activeMessages.TryGetValue(channel, out ActiveMessage activeMessage) &&
                    activeMessage.CloseTime > 0f &&
                    currentTime >= activeMessage.CloseTime)
                {
                    CloseCurrentMessage(channel, false, MessageCloseReason.TimedOut);
                }
            }

            RemoveExpiredCooldowns(currentTime);
            ProcessScheduledReminders(currentTime);
            TryShowQueuedMessages(currentTime);
        }

        public void ShowMessage(MessageData message)
        {
            if (message == null)
                return;

            float submittedAt = Time.unscaledTime;
            MessageData snapshot = message.CreateSnapshot();

            if (IsMessageActiveOrQueued(snapshot.Id) || IsCooldownActive(snapshot.Id, submittedAt))
                return;

            StartCooldown(snapshot, submittedAt);

            if (_reservedChannels.Contains(snapshot.Channel))
            {
                AddDeferredMessage(snapshot, submittedAt);
                return;
            }

            ProcessAcceptedMessage(snapshot, submittedAt);
        }

        private void ProcessAcceptedMessage(MessageData message, float submittedAt)
        {
            if (!_displayValidator.CanDisplay(message))
            {
                _queueManager.Enqueue(message, submittedAt);
                return;
            }

            if (_activeMessages.TryGetValue(message.Channel, out ActiveMessage activeMessage))
            {
                if (activeMessage.Message.CanInterrupt &&
                    message.Priority > activeMessage.Message.Priority)
                {
                    DisplayInterruptingMessage(message, submittedAt);
                }
                else
                {
                    _queueManager.Enqueue(message, submittedAt);
                }

                return;
            }

            if (CanDisplay(message))
                DisplayMessage(message, submittedAt);
            else
                _queueManager.Enqueue(message, submittedAt);
        }

        public void QueueMessage(MessageData message)
        {
            if (message == null)
                return;

            float submittedAt = Time.unscaledTime;

            if (IsMessageActiveOrQueued(message.Id) || IsCooldownActive(message.Id, submittedAt))
                return;

            StartCooldown(message, submittedAt);
            _queueManager.Enqueue(message, submittedAt);
            TryShowQueuedMessages(submittedAt);
        }

        public void SetAdvertisementOpen(bool isOpen)
        {
            _displayValidator.SetAdvertisementOpen(isOpen);
        }

        public void SetShopOpen(bool isOpen)
        {
            _displayValidator.SetShopOpen(isOpen);
        }

        public void SetConversationViewsTopPlacement(bool useTopPlacement, string stepId = null, string reason = null)
        {
            if (_messageView != null)
                _messageView.SetTopPlacement(useTopPlacement, stepId, reason);

            if (_tutorialView != null)
                _tutorialView.SetTopPlacement(useTopPlacement, stepId, reason);
        }

        public void SetModalWindowOpen(bool isOpen)
        {
            _displayValidator.SetModalWindowOpen(isOpen);
        }

        public void RegisterModalWindow(object modalWindow)
        {
            _displayValidator.RegisterModalWindow(modalWindow);
        }

        public void UnregisterModalWindow(object modalWindow)
        {
            _displayValidator.UnregisterModalWindow(modalWindow);
        }

        public void SetTutorialRunning(bool isRunning)
        {
            _displayValidator.SetTutorialRunning(isRunning);
        }

        public void SetDialogOpen(bool isOpen)
        {
            _displayValidator.SetDialogOpen(isOpen);
        }

        public bool IsMessageActiveOrQueued(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            if (_reservedMessageIds.Contains(id))
                return true;

            if (_deferredMessageIds.Contains(id))
                return true;

            if (_scheduledReminderIds.Contains(id))
                return true;

            foreach (ActiveMessage activeMessage in _activeMessages.Values)
            {
                if (activeMessage.Message.Id == id)
                    return true;
            }

            return _queueManager.ContainsId(id, Time.unscaledTime);
        }

        public void CloseCurrentMessage()
        {
            for (int i = 0; i < Channels.Length; i++)
                CloseCurrentMessage(Channels[i], false);

            TryShowQueuedMessages(Time.unscaledTime);
        }

        private void AddDeferredMessage(MessageData message, float submittedAt)
        {
            _deferredMessages.Add(new DeferredMessage(message, submittedAt));

            if (!string.IsNullOrEmpty(message.Id))
                _deferredMessageIds.Add(message.Id);
        }

        private void ProcessDeferredMessages()
        {
            if (_isProcessingDeferredMessages)
                return;

            _isProcessingDeferredMessages = true;

            try
            {
                while (_deferredMessages.Count > 0)
                {
                    DeferredMessage deferredMessage = _deferredMessages[0];
                    _deferredMessages.RemoveAt(0);

                    if (!string.IsNullOrEmpty(deferredMessage.Message.Id))
                        _deferredMessageIds.Remove(deferredMessage.Message.Id);

                    ProcessAcceptedMessage(deferredMessage.Message, deferredMessage.SubmittedAt);
                }
            }
            finally
            {
                _isProcessingDeferredMessages = false;
            }
        }

        public void CloseCurrentMessage(MessageChannel channel)
        {
            CloseCurrentMessage(channel, true);
        }

        public bool CompleteCurrentMessage(MessageChannel channel)
        {
            if (!_activeMessages.ContainsKey(channel))
                return false;

            _analyticsService.TrackCompleted(channel, Time.unscaledTime);
            CloseCurrentMessage(channel, true);
            return true;
        }

        public bool MarkCurrentMessageCompleted(MessageChannel channel)
        {
            return _activeMessages.ContainsKey(channel) &&
                   _analyticsService.TrackCompleted(channel, Time.unscaledTime);
        }

        public MessageData GetCurrentMessage(MessageChannel channel)
        {
            return _activeMessages.TryGetValue(channel, out ActiveMessage activeMessage)
                ? activeMessage.Message.CreateSnapshot()
                : null;
        }

        public void AdvanceCurrentMessage(MessageChannel channel)
        {
            if (!_activeMessages.TryGetValue(channel, out ActiveMessage activeMessage))
                return;

            BaseMessageView view = GetView(activeMessage.Message.Type);

            if (view is TypewriterMessageView typewriterMessageView &&
                typewriterMessageView.IsTyping)
            {
                typewriterMessageView.HandleClick();
                return;
            }

            if (activeMessage.Message.Type == MessageType.Tutorial)
                CloseCurrentMessage(channel);
            else
                CompleteCurrentMessage(channel);
        }

        private void CloseCurrentMessage(MessageChannel channel, bool showNextMessages)
        {
            CloseCurrentMessage(channel, showNextMessages, MessageCloseReason.Manual);
        }

        private void CloseCurrentMessage(
            MessageChannel channel,
            bool showNextMessages,
            MessageCloseReason closeReason)
        {
            if (!_activeMessages.TryGetValue(channel, out ActiveMessage activeMessage))
                return;

            _activeMessages.Remove(channel);
            HideView(activeMessage.Message);

            float closedAt = Time.unscaledTime;

            if (closeReason == MessageCloseReason.TimedOut)
            {
                _analyticsService.TrackIgnored(activeMessage.Message, closedAt);
                ScheduleReminder(activeMessage.Message);
            }

            _analyticsService.TrackClosed(activeMessage.Message, closedAt);

            OnMessageClosed?.Invoke(activeMessage.Message.CreateSnapshot());

            if (showNextMessages)
                TryShowQueuedMessages(Time.unscaledTime);
        }

        private void TryShowQueuedMessages(float currentTime)
        {
            while (_queueManager.TryDequeue(
                       currentTime,
                       CanDisplay,
                       out MessageData message,
                       out float submittedAt))
            {
                DisplayMessage(message, submittedAt);
            }
        }

        private void DisplayMessage(MessageData message, float submittedAt)
        {
            float currentTime = Time.unscaledTime;
            float closeTime = GetCloseTime(message, submittedAt, currentTime);

            if (closeTime > 0f && currentTime >= closeTime)
                return;

            _activeMessages[message.Channel] = new ActiveMessage(message, submittedAt, closeTime);
            _analyticsService.TrackShown(message, currentTime);

            ShowView(message);
            OnMessageShown?.Invoke(message.CreateSnapshot());
        }

        private void ShowView(MessageData message)
        {
            BaseMessageView view = GetView(message.Type);

            if (view != null)
                view.Show(message);
        }

        private void HideView(MessageData message)
        {
            BaseMessageView view = GetView(message.Type);

            MessageTutorialTrace.LogHide(
                "MessageManager.HideView",
                view != null ? view.gameObject : null,
                message != null
                    ? $"messageId={message.Id} type={message.Type} channel={message.Channel}"
                    : "message=null");

            if (view != null && ReferenceEquals(view.DisplayedMessage, message))
                view.Hide();
        }

        private BaseMessageView GetView(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Tutorial:
                    return _tutorialView;
                case MessageType.Dialog:
                    return _messageView;
                case MessageType.Hint:
                    return _characterBubbleView;
                case MessageType.Toast:
                    return _toastView;
                default:
                    return null;
            }
        }

        private bool CanDisplay(MessageData message)
        {
            return !_activeMessages.ContainsKey(message.Channel) &&
                   !_reservedChannels.Contains(message.Channel) &&
                   _displayValidator.CanDisplay(message);
        }

        private void DisplayInterruptingMessage(MessageData message, float submittedAt)
        {
            _reservedChannels.Add(message.Channel);
            bool hasMessageId = !string.IsNullOrEmpty(message.Id);

            if (hasMessageId)
                _reservedMessageIds.Add(message.Id);

            try
            {
                CloseCurrentMessage(message.Channel, false, MessageCloseReason.Interrupted);
                DisplayMessage(message, submittedAt);
            }
            finally
            {
                if (hasMessageId)
                    _reservedMessageIds.Remove(message.Id);

                _reservedChannels.Remove(message.Channel);
            }

            ProcessDeferredMessages();
            TryShowQueuedMessages(Time.unscaledTime);
        }

        private void ScheduleReminder(MessageData message)
        {
            if (message == null ||
                string.IsNullOrEmpty(message.Id) ||
                message.ReminderInterval <= 0f ||
                !CanScheduleReminder(message.ReminderCount, message.MaxShows) ||
                IsMessageActiveOrQueued(message.Id))
            {
                return;
            }

            MessageData reminder = message.CreateSnapshot();
            reminder.ReminderCount++;
            _scheduledReminders.Add(new ScheduledReminder(
                reminder,
                Time.unscaledTime + reminder.ReminderInterval));
            _scheduledReminderIds.Add(reminder.Id);
        }

        public static bool CanScheduleReminder(int reminderCount, int maxShows)
        {
            // reminderCount excludes the initial display. maxShows includes it.
            return maxShows > 1 &&
                   reminderCount >= 0 &&
                   reminderCount < maxShows - 1;
        }

        private void ProcessScheduledReminders(float currentTime)
        {
            int index = 0;

            while (index < _scheduledReminders.Count)
            {
                ScheduledReminder scheduledReminder = _scheduledReminders[index];

                if (currentTime < scheduledReminder.ShowTime ||
                    IsCooldownActive(scheduledReminder.Message.Id, currentTime))
                {
                    index++;
                    continue;
                }

                _scheduledReminders.RemoveAt(index);
                _scheduledReminderIds.Remove(scheduledReminder.Message.Id);
                ShowMessage(scheduledReminder.Message);
            }
        }

        private void StartCooldown(MessageData message, float currentTime)
        {
            if (!string.IsNullOrEmpty(message.Id) && message.Cooldown > 0f)
                _cooldownEndTimes[message.Id] = currentTime + message.Cooldown;
        }

        private bool IsCooldownActive(string messageId, float currentTime)
        {
            if (string.IsNullOrEmpty(messageId))
                return false;

            if (!_cooldownEndTimes.TryGetValue(messageId, out float cooldownEndTime))
                return false;

            if (currentTime < cooldownEndTime)
                return true;

            _cooldownEndTimes.Remove(messageId);
            return false;
        }

        private MessageData GetHighestPriorityActiveMessage()
        {
            MessageData highestPriorityMessage = null;

            foreach (ActiveMessage activeMessage in _activeMessages.Values)
            {
                if (highestPriorityMessage == null ||
                    activeMessage.Message.Priority > highestPriorityMessage.Priority)
                {
                    highestPriorityMessage = activeMessage.Message;
                }
            }

            return highestPriorityMessage;
        }

        private void RemoveExpiredCooldowns(float currentTime)
        {
            if (currentTime < _nextCooldownCleanupTime)
                return;

            _nextCooldownCleanupTime = currentTime + CooldownCleanupInterval;
            _expiredCooldownIds.Clear();

            foreach (KeyValuePair<string, float> cooldown in _cooldownEndTimes)
            {
                if (currentTime >= cooldown.Value)
                    _expiredCooldownIds.Add(cooldown.Key);
            }

            for (int i = 0; i < _expiredCooldownIds.Count; i++)
                _cooldownEndTimes.Remove(_expiredCooldownIds[i]);
        }

        private static float GetCloseTime(MessageData message, float submittedAt, float currentTime)
        {
            float closeTime = message.DisplayDuration > 0f
                ? currentTime + message.DisplayDuration
                : 0f;

            if (message.Lifetime <= 0f)
                return closeTime;

            float lifetimeEndTime = submittedAt + message.Lifetime;

            return closeTime <= 0f || lifetimeEndTime < closeTime
                ? lifetimeEndTime
                : closeTime;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private readonly struct ActiveMessage
        {
            public ActiveMessage(MessageData message, float submittedAt, float closeTime)
            {
                Message = message;
                SubmittedAt = submittedAt;
                CloseTime = closeTime;
            }

            public MessageData Message { get; }
            public float SubmittedAt { get; }
            public float CloseTime { get; }
        }

        private readonly struct DeferredMessage
        {
            public DeferredMessage(MessageData message, float submittedAt)
            {
                Message = message;
                SubmittedAt = submittedAt;
            }

            public MessageData Message { get; }
            public float SubmittedAt { get; }
        }

        private readonly struct ScheduledReminder
        {
            public ScheduledReminder(MessageData message, float showTime)
            {
                Message = message;
                ShowTime = showTime;
            }

            public MessageData Message { get; }
            public float ShowTime { get; }
        }

        private enum MessageCloseReason
        {
            Manual,
            Interrupted,
            TimedOut
        }
    }

    internal static class MessageTutorialTrace
    {
        public static void LogHide(string methodName, GameObject target, string reason)
        {
            Debug.LogWarning(
                "[MetaTutorialTrace] UI_HIDE_TRACE\n" +
                $"method={methodName}\n" +
                $"target={(target != null ? target.name : "null")}\n" +
                $"path={GetTransformPath(target != null ? target.transform : null)}\n" +
                $"activeSelfBefore={(target != null && target.activeSelf)}\n" +
                $"activeInHierarchyBefore={(target != null && target.activeInHierarchy)}\n" +
                $"parentPath={GetTransformPath(target != null ? target.transform.parent : null)}\n" +
                $"reason={reason}",
                target);
        }

        public static string GetTransformPath(Transform target)
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
    }
}

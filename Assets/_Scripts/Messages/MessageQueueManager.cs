using System;
using System.Collections.Generic;

namespace PlanetBuilder.Messages
{
    public class MessageQueueManager
    {
        private readonly SortedSet<QueuedMessage> _messages = new(QueuedMessageComparer.Instance);
        private readonly List<QueuedMessage> _expiredMessages = new();
        private long _nextSequence;

        public bool Enqueue(MessageData message, float submittedAt)
        {
            if (message == null)
                return false;

            RemoveExpiredMessages(submittedAt);

            if (ContainsId(message.Id))
                return false;

            QueuedMessage queuedMessage = new(message.CreateSnapshot(), submittedAt, _nextSequence++);
            _messages.Add(queuedMessage);
            return true;
        }

        public bool ContainsId(string messageId, float currentTime)
        {
            if (string.IsNullOrEmpty(messageId))
                return false;

            RemoveExpiredMessages(currentTime);
            return ContainsId(messageId);
        }

        public bool TryDequeue(float currentTime, out MessageData message)
        {
            return TryDequeue(currentTime, null, out message, out _);
        }

        public bool TryDequeue(
            float currentTime,
            Func<MessageData, bool> canDequeue,
            out MessageData message,
            out float submittedAt)
        {
            RemoveExpiredMessages(currentTime);

            foreach (QueuedMessage queuedMessage in _messages)
            {
                if (canDequeue != null && !canDequeue(queuedMessage.Message))
                    continue;

                _messages.Remove(queuedMessage);
                message = queuedMessage.Message;
                submittedAt = queuedMessage.SubmittedAt;
                return true;
            }

            message = null;
            submittedAt = 0f;
            return false;
        }

        public int GetCount(float currentTime)
        {
            RemoveExpiredMessages(currentTime);
            return _messages.Count;
        }

        public void Clear()
        {
            _messages.Clear();
        }

        private bool ContainsId(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
                return false;

            foreach (QueuedMessage queuedMessage in _messages)
            {
                if (queuedMessage.Message.Id == messageId)
                    return true;
            }

            return false;
        }

        private void RemoveExpiredMessages(float currentTime)
        {
            _expiredMessages.Clear();

            foreach (QueuedMessage message in _messages)
            {
                if (IsExpired(message, currentTime))
                    _expiredMessages.Add(message);
            }

            for (int i = 0; i < _expiredMessages.Count; i++)
                _messages.Remove(_expiredMessages[i]);
        }

        private sealed class QueuedMessageComparer : IComparer<QueuedMessage>
        {
            public static readonly QueuedMessageComparer Instance = new();

            public int Compare(QueuedMessage left, QueuedMessage right)
            {
                int priorityComparison = right.Message.Priority.CompareTo(left.Message.Priority);

                return priorityComparison != 0
                    ? priorityComparison
                    : left.Sequence.CompareTo(right.Sequence);
            }
        }

        private static bool IsExpired(QueuedMessage message, float currentTime)
        {
            return message.Message.Lifetime > 0f &&
                   currentTime - message.SubmittedAt >= message.Message.Lifetime;
        }

        private readonly struct QueuedMessage
        {
            public QueuedMessage(MessageData message, float submittedAt, long sequence)
            {
                Message = message;
                SubmittedAt = submittedAt;
                Sequence = sequence;
            }

            public MessageData Message { get; }
            public float SubmittedAt { get; }
            public long Sequence { get; }
        }
    }
}

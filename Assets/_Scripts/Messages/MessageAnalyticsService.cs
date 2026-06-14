using System;
using System.Collections.Generic;

namespace PlanetBuilder.Messages
{
    /// <summary>
    /// Collects message lifecycle analytics independently from message views.
    /// External analytics providers can subscribe to OnEventTracked.
    /// </summary>
    public sealed class MessageAnalyticsService
    {
        private readonly Dictionary<MessageChannel, MessageAnalyticsRecord> _activeRecords = new();
        private readonly List<MessageAnalyticsRecord> _records = new();

        public IReadOnlyList<MessageAnalyticsRecord> Records => _records;

        public event Action<MessageAnalyticsEventType, MessageAnalyticsRecord> OnEventTracked;

        public void TrackShown(MessageData message, float shownAt)
        {
            if (message == null)
                return;

            MessageAnalyticsRecord record = new(message, shownAt);
            _activeRecords[message.Channel] = record;
            _records.Add(record);
            OnEventTracked?.Invoke(MessageAnalyticsEventType.Shown, record);
        }

        public bool TrackCompleted(MessageChannel channel, float completedAt)
        {
            if (!_activeRecords.TryGetValue(channel, out MessageAnalyticsRecord record) ||
                record.WasCompleted)
            {
                return false;
            }

            record.WasCompleted = true;
            record.CompletedAt = completedAt;
            record.CompletionTime = Math.Max(0f, completedAt - record.ShownAt);
            OnEventTracked?.Invoke(MessageAnalyticsEventType.Completed, record);
            return true;
        }

        public void TrackIgnored(MessageData message, float ignoredAt)
        {
            if (!TryGetActiveRecord(message, out MessageAnalyticsRecord record) ||
                record.WasIgnored)
            {
                return;
            }

            record.WasIgnored = true;
            record.IgnoredAt = ignoredAt;
            OnEventTracked?.Invoke(MessageAnalyticsEventType.Ignored, record);
        }

        public void TrackClosed(MessageData message, float closedAt)
        {
            if (!TryGetActiveRecord(message, out MessageAnalyticsRecord record))
                return;

            record.WasClosed = true;
            record.ClosedAt = closedAt;
            OnEventTracked?.Invoke(MessageAnalyticsEventType.Closed, record);
            _activeRecords.Remove(message.Channel);
        }

        private bool TryGetActiveRecord(
            MessageData message,
            out MessageAnalyticsRecord record)
        {
            record = null;

            return message != null &&
                   _activeRecords.TryGetValue(message.Channel, out record) &&
                   record.MessageId == message.Id;
        }
    }
}

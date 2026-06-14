namespace PlanetBuilder.Messages
{
    /// <summary>
    /// Stores one display lifecycle. Times use Unity unscaled time in seconds.
    /// ReminderCount excludes the initial display and identifies the shown reminder.
    /// </summary>
    public sealed class MessageAnalyticsRecord
    {
        internal MessageAnalyticsRecord(MessageData message, float shownAt)
        {
            MessageId = message.Id;
            MessageType = message.Type;
            MessageChannel = message.Channel;
            ShownAt = shownAt;
            ReminderCount = message.ReminderCount;
            WasShown = true;
        }

        public string MessageId { get; }
        public MessageType MessageType { get; }
        public MessageChannel MessageChannel { get; }
        public bool WasShown { get; internal set; }
        public bool WasClosed { get; internal set; }
        public bool WasCompleted { get; internal set; }
        public bool WasIgnored { get; internal set; }
        public float ShownAt { get; }
        public float ClosedAt { get; internal set; }
        public float CompletedAt { get; internal set; }
        public float IgnoredAt { get; internal set; }
        public float CompletionTime { get; internal set; }
        public int ReminderCount { get; }
    }
}

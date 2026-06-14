using System;
using PlanetBuilder.Messages;
using UnityEditor;
using UnityEngine;

public static class MessageAnalyticsScenarioChecks
{
    [MenuItem("Tools/Planet Builder/Run Message Analytics Scenario Checks")]
    public static void Run()
    {
        CheckCompletedMessage();
        CheckIgnoredReminder();
        Debug.Log("Message analytics scenario checks passed.");
    }

    private static void CheckCompletedMessage()
    {
        MessageAnalyticsService service = new();
        MessageData message = CreateMessage("Analytics.Completed", 0);

        service.TrackShown(message, 10f);
        service.TrackCompleted(message.Channel, 13f);
        service.TrackClosed(message, 14f);

        MessageAnalyticsRecord record = service.Records[0];

        if (!record.WasShown ||
            !record.WasCompleted ||
            !record.WasClosed ||
            record.WasIgnored ||
            record.ShownAt != 10f ||
            record.CompletedAt != 13f ||
            record.CompletionTime != 3f)
        {
            throw new InvalidOperationException("Completed message analytics is invalid.");
        }
    }

    private static void CheckIgnoredReminder()
    {
        MessageAnalyticsService service = new();
        MessageData message = CreateMessage("Analytics.Ignored", 2);

        service.TrackShown(message, 20f);
        service.TrackIgnored(message, 25f);
        service.TrackClosed(message, 25f);

        MessageAnalyticsRecord record = service.Records[0];

        if (!record.WasIgnored ||
            !record.WasClosed ||
            record.WasCompleted ||
            record.IgnoredAt != 25f ||
            record.ReminderCount != 2)
        {
            throw new InvalidOperationException("Ignored reminder analytics is invalid.");
        }
    }

    private static MessageData CreateMessage(string id, int reminderCount)
    {
        return new MessageData
        {
            Id = id,
            Type = PlanetBuilder.Messages.MessageType.Hint,
            Channel = MessageChannel.Hint,
            ReminderCount = reminderCount
        };
    }
}

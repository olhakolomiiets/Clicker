using PlanetBuilder.Messages;
using UnityEditor;
using UnityEngine;

public static class MessageReminderScenarioChecks
{
    [MenuItem("Tools/Planet Builder/Run Message Reminder Scenario Checks")]
    public static void Run()
    {
        CheckMaxShows(1, 0);
        CheckMaxShows(2, 1);
        CheckMaxShows(3, 2);
        CheckMaxShows(5, 4);
        Debug.Log("Message reminder scenario checks passed.");
    }

    private static void CheckMaxShows(int maxShows, int expectedReminderCount)
    {
        int reminderCount = 0;

        while (MessageManager.CanScheduleReminder(reminderCount, maxShows))
            reminderCount++;

        if (reminderCount != expectedReminderCount)
        {
            throw new System.InvalidOperationException(
                $"MaxShows={maxShows}: expected {expectedReminderCount} reminders, got {reminderCount}.");
        }
    }
}

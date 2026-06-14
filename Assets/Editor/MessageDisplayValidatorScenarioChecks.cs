using System;
using PlanetBuilder.Messages;
using UnityEditor;
using UnityEngine;

public static class MessageDisplayValidatorScenarioChecks
{
    [MenuItem("Tools/Planet Builder/Run Message Display Validator Scenario Checks")]
    public static void Run()
    {
        CheckQueueRetry();
        CheckGlobalBlockers();
        CheckTutorialFiltering();
        CheckDialogFiltering();
        Debug.Log("Message display validator scenario checks passed.");
    }

    private static void CheckQueueRetry()
    {
        MessageDisplayValidator validator = new();
        MessageQueueManager queue = new();
        MessageData message = CreateMessage("Validator.QueueRetry", MessageChannel.Hint);

        validator.SetShopOpen(true);
        queue.Enqueue(message, 0f);

        if (queue.TryDequeue(1f, validator.CanDisplay, out _, out _) ||
            queue.GetCount(1f) != 1)
        {
            throw new InvalidOperationException("Blocked message must remain queued.");
        }

        validator.SetShopOpen(false);

        if (!queue.TryDequeue(2f, validator.CanDisplay, out MessageData dequeued, out _) ||
            dequeued.Id != message.Id)
        {
            throw new InvalidOperationException("Queued message must display after unblocking.");
        }
    }

    private static void CheckGlobalBlockers()
    {
        MessageDisplayValidator validator = new();
        MessageData message = CreateMessage("Validator.Global", MessageChannel.Toast);

        validator.SetAdvertisementOpen(true);
        AssertBlocked(validator, message, "advertisement");
        validator.SetAdvertisementOpen(false);

        validator.SetShopOpen(true);
        AssertBlocked(validator, message, "shop");
        validator.SetShopOpen(false);

        validator.SetModalWindowOpen(true);
        AssertBlocked(validator, message, "modal window");
        AssertAllowed(
            validator,
            new MessageData
            {
                Id = "Validator.TutorialModal",
                Channel = MessageChannel.Tutorial,
                CanDisplayOverModal = true
            },
            "tutorial modal override");
        validator.SetModalWindowOpen(false);

        object firstModal = new();
        object secondModal = new();
        validator.RegisterModalWindow(firstModal);
        validator.RegisterModalWindow(secondModal);
        validator.UnregisterModalWindow(firstModal);
        AssertBlocked(validator, message, "remaining registered modal window");
        validator.UnregisterModalWindow(secondModal);
        AssertAllowed(validator, message, "closed modal windows");
    }

    private static void CheckTutorialFiltering()
    {
        MessageDisplayValidator validator = new();
        validator.SetTutorialRunning(true);

        AssertAllowed(validator, CreateMessage("Validator.Tutorial", MessageChannel.Tutorial), "tutorial");
        AssertBlocked(validator, CreateMessage("Validator.Hint", MessageChannel.Hint), "tutorial");
    }

    private static void CheckDialogFiltering()
    {
        MessageDisplayValidator validator = new();
        validator.SetDialogOpen(true);

        AssertAllowed(validator, CreateMessage("Validator.Dialog", MessageChannel.Dialog), "dialog");
        AssertBlocked(validator, CreateMessage("Validator.Toast", MessageChannel.Toast), "dialog");
    }

    private static MessageData CreateMessage(string id, MessageChannel channel)
    {
        return new MessageData
        {
            Id = id,
            Channel = channel
        };
    }

    private static void AssertAllowed(
        MessageDisplayValidator validator,
        MessageData message,
        string blocker)
    {
        if (!validator.CanDisplay(message))
            throw new InvalidOperationException($"{blocker} message must be allowed.");
    }

    private static void AssertBlocked(
        MessageDisplayValidator validator,
        MessageData message,
        string blocker)
    {
        if (validator.CanDisplay(message))
            throw new InvalidOperationException($"Message must be blocked by {blocker}.");
    }
}

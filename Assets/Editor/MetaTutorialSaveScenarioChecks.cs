using System;
using PlanetBuilder.Messages.Tutorial;
using UnityEditor;
using UnityEngine;

public static class MetaTutorialSaveScenarioChecks
{
    private const string MainTutorialCompletedKey = "TutorialCompleted";
    private const string MetaTutorialCompletedKey = "MetaPlanetTutorialCompleted";

    [MenuItem("Tools/Planet Builder/Run Meta Tutorial Save Scenario Checks")]
    public static void Run()
    {
        PlayerPrefsSnapshot mainTutorial = PlayerPrefsSnapshot.Capture(MainTutorialCompletedKey);
        PlayerPrefsSnapshot metaTutorial = PlayerPrefsSnapshot.Capture(MetaTutorialCompletedKey);

        try
        {
            CheckMainCompletedMetaNotCompleted();
            CheckMetaNotCompleted();
            CheckMetaCompleted();
            Debug.Log("Meta tutorial save scenario checks passed.");
        }
        finally
        {
            mainTutorial.Restore();
            metaTutorial.Restore();
            PlayerPrefs.Save();
        }
    }

    private static void CheckMainCompletedMetaNotCompleted()
    {
        PlayerPrefs.SetInt(MainTutorialCompletedKey, 1);
        PlayerPrefs.DeleteKey(MetaTutorialCompletedKey);

        Assert(
            MetaPlanetTutorialController.ShouldStartTutorial(),
            "Completed main tutorial must not block Meta Planet Tutorial.");
    }

    private static void CheckMetaNotCompleted()
    {
        PlayerPrefs.SetInt(MainTutorialCompletedKey, 0);
        PlayerPrefs.DeleteKey(MetaTutorialCompletedKey);

        Assert(
            MetaPlanetTutorialController.ShouldStartTutorial(),
            "Meta Planet Tutorial must start while its own completion key is absent.");
    }

    private static void CheckMetaCompleted()
    {
        PlayerPrefs.SetInt(MainTutorialCompletedKey, 0);
        PlayerPrefs.SetInt(MetaTutorialCompletedKey, 1);

        Assert(
            !MetaPlanetTutorialController.ShouldStartTutorial(),
            "Completed Meta Planet Tutorial must not start again.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private readonly struct PlayerPrefsSnapshot
    {
        private readonly string _key;
        private readonly bool _exists;
        private readonly int _value;

        private PlayerPrefsSnapshot(string key, bool exists, int value)
        {
            _key = key;
            _exists = exists;
            _value = value;
        }

        public static PlayerPrefsSnapshot Capture(string key)
        {
            return new PlayerPrefsSnapshot(key, PlayerPrefs.HasKey(key), PlayerPrefs.GetInt(key, 0));
        }

        public void Restore()
        {
            if (_exists)
                PlayerPrefs.SetInt(_key, _value);
            else
                PlayerPrefs.DeleteKey(_key);
        }
    }
}

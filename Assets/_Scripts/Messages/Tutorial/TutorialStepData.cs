using System;
using PlanetBuilder.Messages.Characters;
using UnityEngine;

namespace PlanetBuilder.Messages.Tutorial
{
    [Serializable]
    public class TutorialStepData
    {
        public string StepId;
        [TextArea] public string MessageText;
        public GameObject TargetUI;
        public TutorialCompletionCondition CompletionCondition;

        [Header("Character")]
        public string SpeakerId;
        public CharacterMood CharacterMood;

        [Header("Highlight")]
        public bool EnableHighlight;
        public Color HighlightColor = new(1f, 0.85f, 0.15f, 0.35f);
        public Vector2 HighlightPadding = new(16f, 16f);
        public bool EnablePulse = true;
        [Min(0f)] public float PulseSpeed = 2f;
        [Min(0f)] public float PulseScale = 0.08f;
        public bool DisableInteractionCutout;

        [Header("Arrow")]
        public bool ShowArrow;
        public Color ArrowColor = Color.white;
        [Min(0f)] public float ArrowDistance = 56f;
    }
}

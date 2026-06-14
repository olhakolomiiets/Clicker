using System;
using UnityEngine;

namespace PlanetBuilder.Messages.Hints
{
    [Serializable]
    public class HintData
    {
        public string Id;
        [TextArea] public string Text;
        public string Condition;
        [Min(0f)] public float Cooldown;
        public int Priority;

        [Header("Triggers")]
        public HintTriggerType Triggers;
        [Min(0f)] public float TimerDelay;
        [Min(0.01f)] public float TimerInterval = 30f;
        public string GameEventId;

        [Header("Message")]
        [Min(0f)] public float DisplayDuration = 4f;
        [Min(0f)] public float Lifetime;
        public bool CanInterrupt;
    }
}

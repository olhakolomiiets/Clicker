using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetBuilder.Messages.Hints
{
    public class HintManager : MonoBehaviour
    {
        [SerializeField] private List<HintData> _hints = new();
        [SerializeField, Min(0.05f)] private float _conditionCheckInterval = 0.25f;

        private readonly Dictionary<string, Func<bool>> _conditionProviders = new();
        private readonly List<HintRuntimeState> _runtimeStates = new();

        private float _nextConditionCheckTime;

        private void Awake()
        {
            RebuildRuntimeStates();
        }

        private void OnEnable()
        {
            ResetTimerSchedule();
        }

        private void Update()
        {
            float currentTime = Time.unscaledTime;
            ProcessTimers(currentTime);

            if (currentTime < _nextConditionCheckTime)
                return;

            _nextConditionCheckTime = currentTime + Mathf.Max(0.05f, _conditionCheckInterval);
            ProcessConditions();
        }

        private void OnValidate()
        {
            if (_hints == null)
            {
                Debug.LogWarning("HintManager hints list is missing.", this);
                return;
            }

            HashSet<string> hintIds = new();

            for (int i = 0; i < _hints.Count; i++)
            {
                HintData hint = _hints[i];

                if (hint == null || string.IsNullOrEmpty(hint.Id))
                {
                    Debug.LogWarning($"Hint at index {i} requires an Id.", this);
                    continue;
                }

                if (!hintIds.Add(hint.Id))
                    Debug.LogWarning($"Duplicate hint Id: {hint.Id}", this);

                if ((hint.Triggers & HintTriggerType.GameEvent) != 0 &&
                    string.IsNullOrEmpty(hint.GameEventId))
                {
                    Debug.LogWarning($"Hint '{hint.Id}' requires GameEventId.", this);
                }

                if ((hint.Triggers & HintTriggerType.Condition) != 0 &&
                    string.IsNullOrEmpty(hint.Condition))
                {
                    Debug.LogWarning($"Hint '{hint.Id}' requires Condition.", this);
                }
            }
        }

        public void NotifyGameEvent(string gameEventId)
        {
            if (string.IsNullOrEmpty(gameEventId))
                return;

            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                HintData hint = _runtimeStates[i].Hint;

                if (hint == null ||
                    (hint.Triggers & HintTriggerType.GameEvent) == 0 ||
                    hint.GameEventId != gameEventId)
                {
                    continue;
                }

                TryShowHint(hint);
            }
        }

        public bool ShowHint(string hintId)
        {
            if (string.IsNullOrEmpty(hintId))
                return false;

            HintData hint = FindHint(hintId);
            return hint != null && TryShowHint(hint);
        }

        public void RegisterCondition(string conditionId, Func<bool> conditionProvider)
        {
            if (string.IsNullOrEmpty(conditionId) || conditionProvider == null)
                return;

            _conditionProviders[conditionId] = conditionProvider;
        }

        public void UnregisterCondition(string conditionId)
        {
            if (!string.IsNullOrEmpty(conditionId))
                _conditionProviders.Remove(conditionId);
        }

        private void ProcessTimers(float currentTime)
        {
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                HintRuntimeState state = _runtimeStates[i];
                HintData hint = state.Hint;

                if (hint == null ||
                    (hint.Triggers & HintTriggerType.Timer) == 0 ||
                    currentTime < state.NextTimerTime)
                {
                    continue;
                }

                state.NextTimerTime = currentTime + Mathf.Max(0.01f, hint.TimerInterval);
                TryShowHint(hint);
            }
        }

        private void ProcessConditions()
        {
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                HintRuntimeState state = _runtimeStates[i];
                HintData hint = state.Hint;

                if (hint == null || (hint.Triggers & HintTriggerType.Condition) == 0)
                    continue;

                bool isConditionMet = EvaluateCondition(hint);

                if (isConditionMet && !state.WasConditionMet)
                    TryShowHint(hint);

                state.WasConditionMet = isConditionMet;
            }
        }

        private bool TryShowHint(HintData hint)
        {
            if (hint == null || string.IsNullOrEmpty(hint.Id))
                return false;

            if (!EvaluateCondition(hint))
                return false;

            MessageManager messageManager = MessageManager.Instance;

            if (messageManager == null)
            {
                Debug.LogWarning("HintManager requires an active MessageManager.", this);
                return false;
            }

            if (messageManager.IsMessageActiveOrQueued(hint.Id))
                return false;

            messageManager.ShowMessage(CreateMessage(hint));
            return true;
        }

        private bool EvaluateCondition(HintData hint)
        {
            if (string.IsNullOrEmpty(hint.Condition))
                return true;

            if (!_conditionProviders.TryGetValue(hint.Condition, out Func<bool> conditionProvider))
                return false;

            try
            {
                return conditionProvider();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        private void RebuildRuntimeStates()
        {
            _runtimeStates.Clear();

            if (_hints == null)
                return;

            float currentTime = Time.unscaledTime;

            for (int i = 0; i < _hints.Count; i++)
            {
                HintData hint = _hints[i];

                if (hint != null)
                    _runtimeStates.Add(new HintRuntimeState(hint, currentTime));
            }
        }

        private void ResetTimerSchedule()
        {
            float currentTime = Time.unscaledTime;

            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                HintRuntimeState state = _runtimeStates[i];
                state.NextTimerTime = currentTime + Mathf.Max(0f, state.Hint.TimerDelay);
                state.WasConditionMet = false;
            }

            _nextConditionCheckTime = currentTime;
        }

        private HintData FindHint(string hintId)
        {
            for (int i = 0; i < _runtimeStates.Count; i++)
            {
                HintData hint = _runtimeStates[i].Hint;

                if (hint != null && hint.Id == hintId)
                    return hint;
            }

            return null;
        }

        private static MessageData CreateMessage(HintData hint)
        {
            return new MessageData
            {
                Id = hint.Id,
                Text = hint.Text,
                Type = MessageType.Hint,
                Channel = MessageChannel.Hint,
                Priority = hint.Priority,
                DisplayDuration = hint.DisplayDuration,
                Lifetime = hint.Lifetime,
                Cooldown = hint.Cooldown,
                CanInterrupt = hint.CanInterrupt
            };
        }

        private sealed class HintRuntimeState
        {
            public HintRuntimeState(HintData hint, float currentTime)
            {
                Hint = hint;
                NextTimerTime = currentTime + Mathf.Max(0f, hint.TimerDelay);
            }

            public HintData Hint { get; }
            public float NextTimerTime { get; set; }
            public bool WasConditionMet { get; set; }
        }
    }
}

using System;
using PlanetBuilder.Messages.Characters;

namespace PlanetBuilder.Messages
{
    [Serializable]
    public class MessageData
    {
        public string Id;
        public string Text;
        public string SpeakerId;
        public CharacterMood CharacterMood;
        public MessageType Type;
        public MessageChannel Channel;
        public int Priority;
        public float DisplayDuration;
        public float Lifetime;
        public float Cooldown;
        public bool CanInterrupt;
        public bool CanDisplayOverModal;
        public bool UseTypingAnimation;
        public float TypingSpeed;
        public bool CanSkipTyping;
        // MaxShows is the total number of displays, including the initial display.
        // ReminderCount stores the number of reminders already scheduled after that initial display.
        public int ReminderCount;
        public float ReminderInterval;
        public int MaxShows;

        public MessageData()
        {
        }

        public MessageData(MessageData source)
        {
            Id = source.Id;
            Text = source.Text;
            SpeakerId = source.SpeakerId;
            CharacterMood = source.CharacterMood;
            Type = source.Type;
            Channel = source.Channel;
            Priority = source.Priority;
            DisplayDuration = source.DisplayDuration;
            Lifetime = source.Lifetime;
            Cooldown = source.Cooldown;
            CanInterrupt = source.CanInterrupt;
            CanDisplayOverModal = source.CanDisplayOverModal;
            UseTypingAnimation = source.UseTypingAnimation;
            TypingSpeed = source.TypingSpeed;
            CanSkipTyping = source.CanSkipTyping;
            ReminderCount = source.ReminderCount;
            ReminderInterval = source.ReminderInterval;
            MaxShows = source.MaxShows;
        }

        public MessageData CreateSnapshot()
        {
            return new MessageData(this);
        }
    }
}

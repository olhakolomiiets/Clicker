using System;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.UI;

namespace PlanetBuilder.Messages
{
    public enum MessageUsage
    {
        Unspecified = 0,
        MetaPlanetTutorial = 1,
        PlanetTutorial = 2,
        NormalHint = 3,
        AdsX2Messages = 4,
        AdsDiamondsMessages = 5,
        MetaVisitMessages = 6,
        MetaArrivalMessages = 7,
        StoreMessages = 8
    }

    [Serializable]
    public class MessageData
    {
        public string Id;
        public string Text;
        public string SpeakerId;
        public CharacterMood CharacterMood;
        public MessageUsage Usage;
        public MessageType Type;
        public MessageChannel Channel;
        public int Priority;
        public float DisplayDuration;
        public float Lifetime;
        public float Cooldown;
        public bool CanInterrupt;
        public bool CanDisplayOverModal;
        public bool CanDisplayOverShop;
        public bool UseTypingAnimation;
        public float TypingSpeed;
        public bool CanSkipTyping;
        // MaxShows is the total number of displays, including the initial display.
        // ReminderCount stores the number of reminders already scheduled after that initial display.
        public int ReminderCount;
        public float ReminderInterval;
        public int MaxShows;

        internal bool UseToastView { get; set; }
        internal object Owner { get; set; }
        internal VideoDialogView VideoView { get; set; }

        public MessageData()
        {
        }

        public MessageData(MessageData source)
        {
            Id = source.Id;
            Text = source.Text;
            SpeakerId = source.SpeakerId;
            CharacterMood = source.CharacterMood;
            Usage = source.Usage;
            Type = source.Type;
            Channel = source.Channel;
            Priority = source.Priority;
            DisplayDuration = source.DisplayDuration;
            Lifetime = source.Lifetime;
            Cooldown = source.Cooldown;
            CanInterrupt = source.CanInterrupt;
            CanDisplayOverModal = source.CanDisplayOverModal;
            CanDisplayOverShop = source.CanDisplayOverShop;
            UseTypingAnimation = source.UseTypingAnimation;
            TypingSpeed = source.TypingSpeed;
            CanSkipTyping = source.CanSkipTyping;
            ReminderCount = source.ReminderCount;
            ReminderInterval = source.ReminderInterval;
            MaxShows = source.MaxShows;
            UseToastView = source.UseToastView;
            Owner = source.Owner;
            VideoView = source.VideoView;
        }

        public MessageData CreateSnapshot()
        {
            return new MessageData(this);
        }
    }
}

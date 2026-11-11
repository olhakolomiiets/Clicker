using System;
using System.Collections.Generic;
using PlanetBuilder.Messages.Characters;
using PlanetBuilder.Messages.UI;

namespace PlanetBuilder.Messages.Dialogs
{
    [Serializable]
    public class DialogData
    {
        public string DialogId;
        public List<DialogLine> ListDialogLine = new();
        internal VideoDialogView VideoView { get; set; }
    }

    [Serializable]
    public class DialogLine
    {
        public string Text;
        public string SpeakerId;
        public CharacterMood CharacterMood;
    }
}

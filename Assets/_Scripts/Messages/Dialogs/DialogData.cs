using System;
using System.Collections.Generic;
using PlanetBuilder.Messages.Characters;

namespace PlanetBuilder.Messages.Dialogs
{
    [Serializable]
    public class DialogData
    {
        public string DialogId;
        public List<DialogLine> ListDialogLine = new();
    }

    [Serializable]
    public class DialogLine
    {
        public string Text;
        public string SpeakerId;
        public CharacterMood CharacterMood;
    }
}

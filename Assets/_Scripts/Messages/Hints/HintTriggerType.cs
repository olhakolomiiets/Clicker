using System;

namespace PlanetBuilder.Messages.Hints
{
    [Flags]
    public enum HintTriggerType
    {
        None = 0,
        Timer = 1,
        GameEvent = 2,
        Condition = 4
    }
}

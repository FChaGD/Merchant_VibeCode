using System;

namespace Game.Core
{
    public interface IEncounterManager
    {
        event Action OnEncounterTriggered;
    }
}

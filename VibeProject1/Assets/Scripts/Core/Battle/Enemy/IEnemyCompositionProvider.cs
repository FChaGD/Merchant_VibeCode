using System.Collections.Generic;

namespace Game.Core
{
    public interface IEnemyCompositionProvider
    {
        IReadOnlyList<BattleUnitStats> GetEncounterComposition();
    }
}

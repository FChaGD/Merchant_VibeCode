using System;

namespace Game.Core
{
    public interface IBattleResultSource
    {
        event Action<BattleResult> OnBattleEnded;
    }
}

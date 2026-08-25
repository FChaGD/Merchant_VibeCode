using System;

namespace Game.Core
{
    public interface IBattleResultRule
    {
        void Evaluate(Action<BattleResult> onResult);
    }
}

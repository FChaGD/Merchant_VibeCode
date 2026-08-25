using System;

namespace Game.Core
{
    public interface IBattleResultEvaluator
    {
        void Evaluate(Action<BattleResult> onResult);
    }
}

using System;
using UnityEngine;

namespace Game.Core
{
    public class BattleManager : MonoBehaviour, IBattleController, IBattleResultSource, IManagedComponent
    {
        public event Action<BattleResult> OnBattleEnded;

        private IBattleResultEvaluator resultEvaluator;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IBattleController>(this);
            registrar.Register<IBattleResultSource>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // TODO: FormationComponent, TacticsComponent, MoraleComponent 연결 - 하위 컴포넌트 설계 후 구현
            resultEvaluator = GetComponent<IBattleResultEvaluator>();
            if (resultEvaluator == null)
            {
                throw new InvalidOperationException($"{nameof(BattleManager)}와 같은 GameObject에 {nameof(IBattleResultEvaluator)} 구현체가 없다.");
            }
        }

        public void StartBattle()
        {
            resultEvaluator.Evaluate(EndBattle);
        }

        private void EndBattle(BattleResult result)
        {
            OnBattleEnded?.Invoke(result);
        }
    }
}

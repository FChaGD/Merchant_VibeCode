using System;
using UnityEngine;

namespace Game.Core
{
    public class BattleManager : MonoBehaviour, IBattleController, IBattleResultSource, IManagedComponent
    {
        public event Action<BattleResult> OnBattleEnded;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IBattleController>(this);
            registrar.Register<IBattleResultSource>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // TODO: FormationComponent, TacticsComponent, MoraleComponent, BattleResultEvaluator 연결 - 하위 컴포넌트 설계 후 구현
        }

        public void StartBattle()
        {
            // TODO: 전투 세션 시작 로직 - 하위 컴포넌트 설계 후 구현
        }

        private void EndBattle(BattleResult result)
        {
            OnBattleEnded?.Invoke(result);
        }
    }
}

using System;
using UnityEngine;

namespace Game.Core
{
    public class EncounterManager : MonoBehaviour, IEncounterManager, IManagedComponent
    {
        public event Action OnEncounterTriggered;

        private IBattleController battleController;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IEncounterManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 전투 시작 요청은 EncounterManager의 핵심 책임이므로 Resolve(필수)로 조회한다.
            battleController = registrar.Resolve<IBattleController>();
        }

        private void TriggerEncounter()
        {
            // TODO: IEncounterRule(랜덤/확정) 판정 - 하위 컴포넌트 설계 후 구현
            OnEncounterTriggered?.Invoke();
            battleController.StartBattle();
        }
    }
}

using System;
using UnityEngine;

namespace Game.Core
{
    public class BattleManager : MonoBehaviour, IBattleController, IBattleResultSource, IDefeatConsequenceSource, IBattleSimulationEvents, IManagedComponent
    {
        public event Action<BattleResult> OnBattleEnded;
        public event Action<BattleSimulationLoop> OnSimulationBuilt;

        private IBattleResultEvaluator resultEvaluator;
        private IDefeatConsequenceRule consequenceRule;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IBattleController>(this);
            registrar.Register<IBattleResultSource>(this);
            registrar.Register<IDefeatConsequenceSource>(this);
            registrar.Register<IBattleSimulationEvents>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // TODO: TacticsComponent 연결 - 하위 컴포넌트 설계 후 구현
            resultEvaluator = GetComponent<IBattleResultEvaluator>();
            if (resultEvaluator == null)
            {
                throw new InvalidOperationException($"{nameof(BattleManager)}와 같은 GameObject에 {nameof(IBattleResultEvaluator)} 구현체가 없다.");
            }

            consequenceRule = GetComponent<IDefeatConsequenceRule>();
            if (consequenceRule == null)
            {
                throw new InvalidOperationException($"{nameof(BattleManager)}와 같은 GameObject에 {nameof(IDefeatConsequenceRule)} 구현체가 없다.");
            }

            // 규칙이 IFormationReader/ICaravanRosterProvider를 필요로 하면 주입한다 - 어떤 규칙인지는
            // 몰라도 된다(OCP). 이 지원 코드는 여기 한 번만 추가되며, 이후 같은 마커를 구현하는 새
            // 규칙이 추가/교체돼도 BattleManager는 다시 바뀌지 않는다.
            var rule = GetComponent<IBattleResultRule>();

            // IFormationReader 자체는 DI에 등록되지 않는다 - InMemoryFormationRepository는
            // IFormationRepository로만 등록하므로(제네릭 타입 키라 상위 인터페이스로는 조회 불가),
            // IFormationRepository로 조회해 업캐스트한다(UIManager의 기존 패턴과 동일). 상행 관리
            // 데이터 시스템이 아직 없는 동안은 CLAUDE.md 컨벤션대로 TryResolve로 선택 조회한다.
            if (rule is IRequiresFormationReader formationConsumer
                && registrar.TryResolve<IFormationRepository>(out var formationRepository))
            {
                formationConsumer.SetFormationReader(formationRepository);
            }

            if (rule is IRequiresCaravanRoster rosterConsumer
                && registrar.TryResolve<ICaravanRosterProvider>(out var rosterProvider))
            {
                rosterConsumer.SetCaravanRoster(rosterProvider);
            }

            // 규칙이 시뮬레이션 생성 이벤트를 노출하면(IBattleSimulationEvents), 그대로 흘려보낸다 -
            // 뷰 계층(FieldUIController/BattleViewPresenter)은 BattleManager만 알면 되고 규칙의 구체
            // 타입(LiveBattleSimulationRule)을 몰라도 된다(DIP).
            if (rule is IBattleSimulationEvents simulationEvents)
            {
                simulationEvents.OnSimulationBuilt += loop => OnSimulationBuilt?.Invoke(loop);
            }
        }

        public void StartBattle()
        {
            resultEvaluator.Evaluate(EndBattle);
        }

        public DefeatConsequence ResolveDefeatConsequence() => consequenceRule.Resolve();

        private void EndBattle(BattleResult result)
        {
            OnBattleEnded?.Invoke(result);
        }
    }
}

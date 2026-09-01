using System;
using UnityEngine;

namespace Game.Core
{
    public class BattleManager : MonoBehaviour, IBattleController, IBattleResultSource, IDefeatConsequenceSource, IBattleSimulationEvents, IManagedComponent
    {
        public event Action<BattleResult> OnBattleEnded;
        public event Action<BattleSimulationLoop> OnSimulationBuilt;

        private IBattleResultRule resultRule;
        private IDefeatConsequenceRule consequenceRule;
        private IPausableBattleSimulation pausableSimulation;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IBattleController>(this);
            registrar.Register<IBattleResultSource>(this);
            registrar.Register<IDefeatConsequenceSource>(this);
            registrar.Register<IBattleSimulationEvents>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 승패 판정을 IBattleResultRule 전략에 위임한다(OCP 확장점) - 실제 전투 로직이 생겨도
            // PlaceholderBattleResultRule 교체만으로 끝나고 BattleManager는 무변경으로 유지된다.
            resultRule = GetComponent<IBattleResultRule>();
            if (resultRule == null)
            {
                throw new InvalidOperationException($"{nameof(BattleManager)}와 같은 GameObject에 {nameof(IBattleResultRule)} 구현체가 없다.");
            }

            consequenceRule = GetComponent<IDefeatConsequenceRule>();
            if (consequenceRule == null)
            {
                throw new InvalidOperationException($"{nameof(BattleManager)}와 같은 GameObject에 {nameof(IDefeatConsequenceRule)} 구현체가 없다.");
            }

            // 규칙이 IFormationReader/ICaravanRosterProvider를 필요로 하면 주입한다 - 어떤 규칙인지는
            // 몰라도 된다(OCP). 이 지원 코드는 여기 한 번만 추가되며, 이후 같은 마커를 구현하는 새
            // 규칙이 추가/교체돼도 BattleManager는 다시 바뀌지 않는다.

            // IFormationReader 자체는 DI에 등록되지 않는다 - InMemoryFormationRepository는
            // IFormationRepository로만 등록하므로(제네릭 타입 키라 상위 인터페이스로는 조회 불가),
            // IFormationRepository로 조회해 업캐스트한다(UIManager의 기존 패턴과 동일). 상행 관리
            // 데이터 시스템이 아직 없는 동안은 CLAUDE.md 컨벤션대로 TryResolve로 선택 조회한다.
            if (resultRule is IRequiresFormationReader formationConsumer
                && registrar.TryResolve<IFormationRepository>(out var formationRepository))
            {
                formationConsumer.SetFormationReader(formationRepository);
            }

            if (resultRule is IRequiresCaravanRoster rosterConsumer
                && registrar.TryResolve<ICaravanRosterProvider>(out var rosterProvider))
            {
                rosterConsumer.SetCaravanRoster(rosterProvider);
            }

            // 유닛 상태(HP) 저장소도 같은 이유로 TryResolve(설계 15번) - 인스톨러를 아직 재실행하지
            // 않은 씬에서는 자연히 건너뛰고, LiveBattleSimulationRule은 직업 기준 기본 스탯으로 폴백한다.
            if (resultRule is IRequiresUnitConditionRepository conditionConsumer
                && registrar.TryResolve<IUnitConditionRepository>(out var conditionRepository))
            {
                conditionConsumer.SetUnitConditionRepository(conditionRepository);
            }

            // ITacticsRepository도 같은 이유로 TryResolve - InMemoryTacticsRepository는
            // ITacticsRepository로만 등록되므로 ITacticsReader로 업캐스트해 넘긴다(IFormationReader와
            // 동일 패턴). 인스톨러를 아직 재실행하지 않은 씬에서는 자연히 건너뛴다.
            if (resultRule is IRequiresTacticsReader tacticsConsumer
                && registrar.TryResolve<ITacticsRepository>(out var tacticsRepository))
            {
                tacticsConsumer.SetTacticsReader(tacticsRepository);
            }

            // 규칙이 시뮬레이션 생성 이벤트를 노출하면(IBattleSimulationEvents), 그대로 흘려보낸다 -
            // 뷰 계층(FieldUIController/BattleViewPresenter)은 BattleManager만 알면 되고 규칙의 구체
            // 타입(LiveBattleSimulationRule)을 몰라도 된다(DIP).
            if (resultRule is IBattleSimulationEvents simulationEvents)
            {
                simulationEvents.OnSimulationBuilt += loop => OnSimulationBuilt?.Invoke(loop);
            }

            // 규칙이 일시정지/재개를 지원하면(IPausableBattleSimulation) 보관해둔다 - 화면이 완전히
            // 드러난 뒤 ResumeSimulation()으로 재개한다(FieldEncounterFlowCoordinator 참고).
            pausableSimulation = resultRule as IPausableBattleSimulation;
        }

        public void StartBattle()
        {
            resultRule.Evaluate(EndBattle);
        }

        public void ResumeSimulation() => pausableSimulation?.ResumeSimulation();

        public DefeatConsequence ResolveDefeatConsequence() => consequenceRule.Resolve();

        private void EndBattle(BattleResult result)
        {
            OnBattleEnded?.Invoke(result);
        }
    }
}

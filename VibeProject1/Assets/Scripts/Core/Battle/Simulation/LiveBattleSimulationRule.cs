using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PlaceholderBattleResultRule을 대체하는 실제 전투 시뮬레이션. BattleManager가
    /// GetComponent&lt;IBattleResultRule&gt;()로 조회하므로 이 컴포넌트를 붙이는 것만으로 교체된다
    /// (BattleManager 무변경). 승패 조건: 적 전멸(사망+도주)=Victory, 아군 전멸(사망+도주)
    /// 또는 보호 목표 파괴=Defeat.
    /// </summary>
    public class LiveBattleSimulationRule : MonoBehaviour, IBattleResultRule, IRequiresFormationReader, IRequiresCaravanRoster, IRequiresTacticsReader, IBattleSimulationEvents, IPausableBattleSimulation
    {
        // 직업→역할군 매핑 - 실제 데이터(직업별 항목)는 에디터에서 에셋을 만들어 채운다
        // (Docs/설계/12번 §2.1). 비어있으면 UnitTacticsProfileResolver가 경고 후 기본값으로 대체한다.
        [SerializeField] private MercenaryRoleGroupMapAsset roleGroupMap;

        private readonly IBattleUnitStatProvider statProvider = new PlaceholderBattleUnitStatProvider();
        private readonly IEncounterSpawnPointSelector spawnSelector = new UniformRandomSpawnPointSelector();
        private readonly IEnemyCompositionProvider enemyProvider = new PlaceholderBanditCompositionProvider();
        // 아군 좌표 변환과 스폰/반지름 계산은 서로 다른 인터페이스지만 구현은 하나 - 내부 헬퍼
        // 공유 때문에 클래스까지 나누지 않았다(BattleFieldLayout, Docs/설계/12번 §5.2).
        private readonly BattleFieldLayout sharedFieldLayout = new();
        private IAllyPositionLayout FieldPositionLayout => sharedFieldLayout;
        private IBattleFieldGeometry FieldGeometry => sharedFieldLayout;
        private readonly IDamageFormula damageFormula = new PlaceholderDamageFormula();
        private readonly IUnitSpatialQuery spatialQuery = new LinearScanUnitSpatialQuery();

        private IFormationReader formationReader;
        private ICaravanRosterProvider rosterProvider;
        private ITacticsReader tacticsReader;
        private BattleSimulationLoop simulation;
        private Action<BattleResult> onResult;
        private bool resultReported;

        // 화면(커튼)이 완전히 드러나기 전까지는 유닛 위치만 잡아두고 틱은 멈춰둔다(사용자 확정) -
        // 안 그러면 페이드 아웃 도중 반투명해진 커튼 너머로 이미 움직이는 전투가 비쳐 보인다.
        // Evaluate()가 매 전투 시작 시 다시 true로 세팅하고, ResumeSimulation()이 걷힘 완료 시
        // false로 풀어준다(FieldEncounterFlowCoordinator 참고).
        private bool paused;

        public event Action<BattleSimulationLoop> OnSimulationBuilt;

        // BattleManager.ResolveDependencies가 IRequiresFormationReader/IRequiresCaravanRoster/
        // IRequiresTacticsReader로 캐스팅해 호출한다.
        public void SetFormationReader(IFormationReader reader) => formationReader = reader;
        public void SetCaravanRoster(ICaravanRosterProvider provider) => rosterProvider = provider;
        public void SetTacticsReader(ITacticsReader reader) => tacticsReader = reader;

        public void Evaluate(Action<BattleResult> onResult)
        {
            this.onResult = onResult;
            resultReported = false;
            paused = true;
            simulation = BuildSimulation();
            OnSimulationBuilt?.Invoke(simulation);
        }

        // BattleManager.ResumeSimulation()이 IPausableBattleSimulation으로 캐스팅해 호출한다.
        public void ResumeSimulation() => paused = false;

        private void Update()
        {
            if (simulation == null || resultReported || paused) return;

            simulation.Tick(Time.deltaTime);

            if (simulation.IsEnemyWiped) Report(BattleOutcome.Victory);
            else if (simulation.IsAllyWiped || simulation.IsProtectionTargetDestroyed) Report(BattleOutcome.Defeat);
        }

        private void Report(BattleOutcome outcome)
        {
            resultReported = true;
            onResult(new BattleResult(outcome));
        }

        private BattleSimulationLoop BuildSimulation()
        {
            // hasLayout을 별도 변수로 옮겨 담으면 컴파일러의 확정 대입 분석이 layout과의 연결을
            // 추적하지 못해(CS0165) layout을 먼저 null로 초기화해둬야 한다.
            FormationLayout layout = null;
            var hasLayout = formationReader != null && formationReader.TryLoadCurrent(out layout);
            // 배치가 없을 때(hasLayout=false) 스폰 반지름/도주 이탈 거리를 계산할 기준 열 수 -
            // 아군이 없으면 어차피 즉시 패배하므로 정확한 값이 중요하지 않지만, 계산 자체는 항상
            // 유효한 columnCount를 필요로 한다. FormationLayout.DefaultColumnCount가 FormationGridView
            // 기본값과 공유하는 단일 출처다.
            var columnCount = hasLayout ? layout.ColumnCount : FormationLayout.DefaultColumnCount;
            var spawnCenter = FieldGeometry.ComputeSpawnPoint(spawnSelector.SelectSpawnPointIndex(), columnCount);
            // 스폰 반지름에서 파생되므로 대형이 클수록(columnCount가 클수록) 도주 이탈 거리도 늘어난다
            // (BattleFieldLayout 참고) - 대형 크기와 무관하게 항상 "전장을 벗어난 곳에서 스폰"이 성립한다.
            var fleeTravelDistance = FieldGeometry.ComputeFleeTravelDistance(columnCount);
            var allyMorale = new PartyMorale(); // 전투마다 새로 시작
            var enemyMorale = new PartyMorale();

            // tacticsReader가 없으면(인스톨러 미실행 등) 방향성 지시 없이 기존 동작으로 자연히
            // 폴백한다 - BuildAllies에 null을 넘기면 UnitTacticsBehaviors도 null이 되어
            // BattleCharacterUnit이 적 유닛과 같은 경로(TickEngageWithoutTactics)를 탄다.
            IUnitTacticsProfileResolver tacticsProfileResolver = tacticsReader != null
                ? new UnitTacticsProfileResolver(tacticsReader, roleGroupMap)
                : null;
            var standardActivityRadius = FieldGeometry.ComputeStandardActivityRadius(columnCount);
            var fieldRadius = FieldGeometry.ComputeFieldRadius(columnCount);
            var spawnRadius = FieldGeometry.ComputeSpawnRadius(columnCount);
            // tacticsReader가 없으면 방향성 지시 자체가 비활성화되므로 파티 추적 설정을 읽을 수 없다 -
            // 이때는 어차피 Blocking 전열 후보가 하나도 없어(모든 RoleGroup이 null) 어떤 프리셋을
            // 넘기든 무해하다(기본값 OffensiveJudgment로 대체). BuildAllies보다 먼저 만들어야
            // BlockingPositioningStrategy(Docs/설계/12번 §12.12 7단계)에 주입할 수 있다.
            var partyPursuitPreset = tacticsReader?.GetPartySettings().Pursuit ?? PursuitPreset.OffensiveJudgment;
            var frontlineCoordinator = new FrontlineFormationCoordinator(standardActivityRadius, partyPursuitPreset);
            // 포위(Surround) 조율자(Docs/설계/12번 §13.3′) - frontlineCoordinator와 같은 이유로
            // BuildAllies보다 먼저 생성해야 SurroundPositioningStrategy에 주입할 수 있다. 군집화
            // 알고리즘 재사용을 위해 frontlineCoordinator 참조가 필요하다(§13.3′ "로직 공유").
            var rangedSurroundCoordinator = new RangedSurroundCoordinator(standardActivityRadius, frontlineCoordinator);

            var allies = hasLayout
                ? BuildAllies(layout, spawnCenter, allyMorale, fleeTravelDistance, tacticsProfileResolver, standardActivityRadius, fieldRadius, frontlineCoordinator, rangedSurroundCoordinator)
                : new List<IBattleCombatant>();
            var enemies = BuildEnemies(spawnCenter, enemyMorale, fleeTravelDistance);
            var protectedUnits = hasLayout ? BuildProtectedUnits(layout) : new List<IDamageable>();

            return new BattleSimulationLoop(allies, enemies, protectedUnits, fieldRadius, spawnRadius, frontlineCoordinator, rangedSurroundCoordinator);
        }

        private List<IBattleCombatant> BuildAllies(
            FormationLayout layout, Vector2 spawnCenter, PartyMorale allyMorale, float fleeTravelDistance,
            IUnitTacticsProfileResolver tacticsProfileResolver, float standardActivityRadius, float fieldRadius,
            FrontlineFormationCoordinator frontlineCoordinator, RangedSurroundCoordinator rangedSurroundCoordinator)
        {
            var allies = new List<IBattleCombatant>();

            for (var slotIndex = 0; slotIndex < layout.SlotCount; slotIndex++)
            {
                var unitId = layout.GetUnitId(slotIndex);
                if (unitId == null) continue;

                var rosterUnit = FindRosterUnit(unitId);
                if (rosterUnit == null || rosterUnit.Kind != FormationUnitKind.Character) continue;

                // 직업 정보가 없는 Character(로스터 구현체가 IMercenaryUnit이 아닌 경우)는 예외적
                // 상황이라 Warrior를 기본값으로 둔다 - 정식 로스터 시스템이 생기면 모든 Character가
                // IMercenaryUnit을 구현하게 되어 이 분기 자체가 필요 없어질 것으로 예상된다.
                var mercenaryClass = rosterUnit is IMercenaryUnit mercenaryUnit ? mercenaryUnit.Class : MercenaryClass.Warrior;

                var column = slotIndex % layout.ColumnCount;
                var row = slotIndex / layout.ColumnCount;
                var position = FieldPositionLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
                var stats = statProvider.GetStats(mercenaryClass);

                // 배치 슬롯 좌표(position)가 곧 방향성 지시의 HomePosition이다 - "정비창 슬롯 좌표"라는
                // 같은 개념을 두 번 계산하지 않는다.
                UnitTacticsBehaviors tacticsBehaviors = null;
                if (tacticsProfileResolver != null)
                {
                    var profile = tacticsProfileResolver.Resolve(mercenaryClass, position);
                    tacticsBehaviors = UnitTacticsBehaviorsFactory.Build(profile, standardActivityRadius, fieldRadius, spatialQuery, frontlineCoordinator, rangedSurroundCoordinator);
                }

                allies.Add(new BattleCharacterUnit(position, isAlly: true, stats, damageFormula, allyMorale, spatialQuery, fleeTravelDistance, tacticsBehaviors));
            }
            return allies;
        }

        private List<IBattleCombatant> BuildEnemies(Vector2 spawnCenter, PartyMorale enemyMorale, float fleeTravelDistance)
        {
            return enemyProvider.GetEncounterComposition()
                .Select(enemyStats => (IBattleCombatant)new BattleCharacterUnit(
                    spawnCenter, isAlly: false, enemyStats, damageFormula, enemyMorale, spatialQuery, fleeTravelDistance))
                .ToList();
        }

        private List<IDamageable> BuildProtectedUnits(FormationLayout layout)
        {
            var result = new List<IDamageable>();
            for (var slotIndex = 0; slotIndex < layout.SlotCount; slotIndex++)
            {
                var unitId = layout.GetUnitId(slotIndex);
                if (unitId == null) continue;

                var rosterUnit = FindRosterUnit(unitId);
                if (rosterUnit == null || rosterUnit.Kind == FormationUnitKind.Character) continue;

                var column = slotIndex % layout.ColumnCount;
                var row = slotIndex / layout.ColumnCount;
                var position = FieldPositionLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
                result.Add(new BattleProtectedUnit(position, ProtectedUnitTuning.MaxHp, rosterUnit.Icon));
            }
            return result;
        }

        // 슬롯의 unitId가 실제로 어떤 유닛(종류/직업)인지는 로스터를 순회해야 알 수 있다(FormationLayout
        // 자체엔 그 정보가 없다). 로스터가 커지면 Dictionary 캐싱으로 바꿀 대상.
        private IFormationUnit FindRosterUnit(string unitId)
        {
            if (rosterProvider == null) return null;

            foreach (var unit in rosterProvider.GetRoster())
            {
                if (unit.Id == unitId) return unit;
            }
            return null;
        }
    }
}

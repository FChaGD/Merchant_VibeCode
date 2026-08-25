using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PlaceholderBattleResultRule을 대체하는 실제 전투 시뮬레이션. BattleResultEvaluator가
    /// GetComponent&lt;IBattleResultRule&gt;()로 조회하므로 이 컴포넌트를 붙이는 것만으로 교체된다
    /// (BattleResultEvaluator 무변경). 승패 조건: 적 전멸(사망+도주)=Victory, 아군 전멸(사망+도주)
    /// 또는 보호 목표 파괴=Defeat.
    /// </summary>
    public class LiveBattleSimulationRule : MonoBehaviour, IBattleResultRule, IRequiresFormationReader, IRequiresCaravanRoster, IBattleSimulationEvents
    {
        // 배치가 없을 때(hasLayout=false) 스폰 반지름/도주 이탈 거리를 계산할 기준 열 수 -
        // FormationGridView의 기본값(8)과 맞춘다. 아군이 없으면 어차피 즉시 패배하므로 정확한 값이
        // 중요하지 않지만, 계산 자체는 항상 유효한 columnCount를 필요로 한다.
        private const int DefaultColumnCount = 8;

        private readonly IBattleUnitStatProvider statProvider = new PlaceholderBattleUnitStatProvider();
        private readonly IEncounterSpawnPointSelector spawnSelector = new UniformRandomSpawnPointSelector();
        private readonly IEnemyCompositionProvider enemyProvider = new PlaceholderBanditCompositionProvider();
        private readonly IBattleFieldLayout fieldLayout = new BattleFieldLayout();
        private readonly IDamageFormula damageFormula = new PlaceholderDamageFormula();
        private readonly IUnitSpatialQuery spatialQuery = new LinearScanUnitSpatialQuery();

        private IFormationReader formationReader;
        private ICaravanRosterProvider rosterProvider;
        private BattleSimulationLoop simulation;
        private Action<BattleResult> onResult;
        private bool resultReported;

        public event Action<BattleSimulationLoop> OnSimulationBuilt;

        // BattleManager.ResolveDependencies가 IRequiresFormationReader/IRequiresCaravanRoster로
        // 캐스팅해 호출한다.
        public void SetFormationReader(IFormationReader reader) => formationReader = reader;
        public void SetCaravanRoster(ICaravanRosterProvider provider) => rosterProvider = provider;

        public void Evaluate(Action<BattleResult> onResult)
        {
            this.onResult = onResult;
            resultReported = false;
            simulation = BuildSimulation();
            OnSimulationBuilt?.Invoke(simulation);
        }

        private void Update()
        {
            if (simulation == null || resultReported) return;

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
            var columnCount = hasLayout ? layout.ColumnCount : DefaultColumnCount;
            var spawnCenter = fieldLayout.ComputeSpawnPoint(spawnSelector.SelectSpawnPointIndex(), columnCount);
            // 스폰 반지름에서 파생되므로 대형이 클수록(columnCount가 클수록) 도주 이탈 거리도 늘어난다
            // (BattleFieldLayout 참고) - 대형 크기와 무관하게 항상 "전장을 벗어난 곳에서 스폰"이 성립한다.
            var fleeTravelDistance = fieldLayout.ComputeFleeTravelDistance(columnCount);
            var allyMorale = new PartyMorale(); // 전투마다 새로 시작
            var enemyMorale = new PartyMorale();

            var allies = hasLayout ? BuildAllies(layout, spawnCenter, allyMorale, fleeTravelDistance) : new List<IBattleCombatant>();
            var enemies = BuildEnemies(spawnCenter, enemyMorale, fleeTravelDistance);
            var protectedUnits = hasLayout ? BuildProtectedUnits(layout) : new List<IDamageable>();

            return new BattleSimulationLoop(allies, enemies, protectedUnits);
        }

        private List<IBattleCombatant> BuildAllies(FormationLayout layout, Vector2 spawnCenter, PartyMorale allyMorale, float fleeTravelDistance)
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
                var position = fieldLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
                var stats = statProvider.GetStats(mercenaryClass);
                allies.Add(new BattleCharacterUnit(position, isAlly: true, stats, damageFormula, allyMorale, spatialQuery, fleeTravelDistance));
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
                var position = fieldLayout.ComputeAllyPosition(column, row, layout.ColumnCount);
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

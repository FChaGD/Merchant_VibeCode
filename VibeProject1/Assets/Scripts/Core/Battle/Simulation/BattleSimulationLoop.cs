using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 순수 C# 합성 객체. 아군/적/보호 목표 리스트를 들고 매 틱 각 유닛의 Tick을 돌리고, 전멸·패배
    /// 여부를 노출한다. 생존 수는 매 프레임 리스트를 다시 스캔하지 않고 OnDied/OnFled
    /// 이벤트로 카운터를 유지한다. 보호 목표는 마차·시설을 따로 센다 - 패배 조건이 둘을 다르게 본다(Docs/기획/73번).
    /// </summary>
    public class BattleSimulationLoop
    {
        private readonly List<IBattleCombatant> allies;
        private readonly List<IBattleCombatant> enemies;
        // 종류(마차/시설)를 읽어야 해서 구체 타입으로 받는다. 외부 노출(ProtectedUnits)은 IDamageable 그대로.
        private readonly List<BattleProtectedUnit> protectedUnits;
        // 방진 형성 조율자(Docs/설계/12번 §12.2) - PartyMorale과 같은 자리, 전투마다 새로 만들어진다.
        // null 가능성 없음 - tacticsReader가 없어 방향성 지시가 전부 비활성화된 전투에서도 코디네이터
        // 자체는 만들어지지만, Blocking 전열 후보가 하나도 없어(모든 RoleGroup이 null) Update가
        // 아무 것도 하지 않는 것으로 자연히 무해해진다(BattleCharacterUnit의 tacticsBehaviors=null
        // 폴백 패턴과 같은 방향).
        private readonly FrontlineFormationCoordinator frontlineCoordinator;
        // 포위(Surround) 조율자(Docs/설계/12번 §13.3) - frontlineCoordinator와 같은 자리·같은 이유.
        private readonly RangedSurroundCoordinator rangedSurroundCoordinator;
        // 사기 파동 조율자(Docs/설계/14번 §6) - 진영별로 하나씩, PartyMorale과 같은 자리.
        private readonly MoraleWaveCoordinator allyWaveCoordinator;
        private readonly MoraleWaveCoordinator enemyWaveCoordinator;
        // 적 표적 후보에 더할 추가 대상(마차 잔해, 설계 79번 §3.3) - 화물 원장이 들고 있는 살아 있는 목록 참조라 전투 중
        // 잔해가 생기면 다음 틱부터 자연히 후보가 된다. 배틀 테스트·화물 없는 전투는 빈 목록.
        private readonly IReadOnlyList<IDamageable> extraEnemyTargets;
        private int aliveAllyCount;
        private int aliveEnemyCount;
        private readonly int totalWagonCount;
        private int aliveWagonCount;
        private int aliveFacilityCount;

        // 이번 전투의 전장 반지름(BattleFieldLayout 기준) - BattleViewPresenter가 전투 뷰 카메라의
        // 시야 경계를 잡을 때 쓴다(Docs/설계/13-2026-08-29-전투뷰_월드오브젝트_전환_아키텍처.md). 시뮬레이션
        // 로직 자체는 이 값을 쓰지 않는다 - 렌더링 소비자를 위해 그대로 들고만 있는다.
        public float FieldRadius { get; }
        // 이번 전투의 스폰 반지름(FieldRadius보다 항상 더 바깥, BattleFieldLayout.ComputeSpawnRadius
        // 기준) - BattleBackgroundGridView가 적 스폰 링을 전부 감싸는 배경 타일 그리드 크기를 잡을 때
        // 쓴다(Docs/설계/13번, FieldRadius와 같은 자리·같은 이유로 추가).
        public float SpawnRadius { get; }

        public BattleSimulationLoop(
            List<IBattleCombatant> allies, List<IBattleCombatant> enemies, List<BattleProtectedUnit> protectedUnits,
            float fieldRadius, float spawnRadius, FrontlineFormationCoordinator frontlineCoordinator, RangedSurroundCoordinator rangedSurroundCoordinator,
            MoraleWaveCoordinator allyWaveCoordinator, MoraleWaveCoordinator enemyWaveCoordinator,
            IReadOnlyList<IDamageable> extraEnemyTargets = null)
        {
            this.extraEnemyTargets = extraEnemyTargets ?? System.Array.Empty<IDamageable>();
            this.allies = allies;
            this.enemies = enemies;
            this.protectedUnits = protectedUnits;
            this.frontlineCoordinator = frontlineCoordinator;
            this.rangedSurroundCoordinator = rangedSurroundCoordinator;
            this.allyWaveCoordinator = allyWaveCoordinator;
            this.enemyWaveCoordinator = enemyWaveCoordinator;
            FieldRadius = fieldRadius;
            SpawnRadius = spawnRadius;
            aliveAllyCount = allies.Count;
            aliveEnemyCount = enemies.Count;

            // 사망(OnDied)과 도주(OnFled) 둘 다 "전장에서 사라짐"이므로 둘 다 카운트를 줄인다(기획 §7.4).
            foreach (var ally in allies) SubscribeDeathAndFlee(ally, isAlly: true);
            foreach (var enemy in enemies) SubscribeDeathAndFlee(enemy, isAlly: false);
            foreach (var unit in protectedUnits)
            {
                if (unit.Kind == ProtectedUnitKind.Wagon)
                {
                    totalWagonCount++;
                    unit.OnDied += () => aliveWagonCount--;
                }
                else
                {
                    unit.OnDied += () => aliveFacilityCount--;
                }
            }
            aliveWagonCount = totalWagonCount;
            aliveFacilityCount = protectedUnits.Count - totalWagonCount;
        }

        private void SubscribeDeathAndFlee(IBattleCombatant unit, bool isAlly)
        {
            if (isAlly)
            {
                unit.OnDied += () => aliveAllyCount--;
                unit.OnFled += () => aliveAllyCount--;
            }
            else
            {
                unit.OnDied += () => aliveEnemyCount--;
                unit.OnFled += () => aliveEnemyCount--;
            }
        }

        // 배틀 테스트 씬 전용(BattleTestSimulationRule) - 전투 도중 드래그로 유닛을 즉시 추가할 때 쓴다.
        // 생성자와 같은 생존 카운트/사망·도주 구독 로직을 공유해야 IsAllyWiped/IsEnemyWiped가 새로
        // 추가된 유닛도 정확히 반영한다. 실제 게임(LiveBattleSimulationRule)은 이 메서드를 호출하지
        // 않는다 - 순수 추가, 기존 동작 불변.
        public void RegisterAdditionalUnit(IBattleCombatant unit, bool isAlly)
        {
            if (isAlly)
            {
                allies.Add(unit);
                aliveAllyCount++;
            }
            else
            {
                enemies.Add(unit);
                aliveEnemyCount++;
            }
            SubscribeDeathAndFlee(unit, isAlly);
        }

        public IReadOnlyList<IBattleCombatant> Allies => allies;
        public IReadOnlyList<IBattleCombatant> Enemies => enemies;
        public IReadOnlyList<IDamageable> ProtectedUnits => protectedUnits;
        // 디버깅 전용(Assets/Scripts/Core/Debug/Battle/BattleSurroundGizmoView가 포위망 원을
        // 그리는 용도) - ActiveRings 조회 외에는 쓰이지 않는다. 삭제 시 이 프로퍼티만 지우면 된다.
        public RangedSurroundCoordinator SurroundCoordinator => rangedSurroundCoordinator;
        // 디버깅 전용(Assets/Scripts/Core/Debug/Battle/BattleFrontlineGizmoView가 방진선/슬롯
        // 위치를 그리는 용도) - ActiveLines 조회 외에는 쓰이지 않는다. 삭제 시 이 프로퍼티만
        // 지우면 된다.
        public FrontlineFormationCoordinator FrontlineCoordinator => frontlineCoordinator;
        // 디버깅 전용(배틀 테스트 씬의 사기 파동 기즈모 시각화) - FrontlineCoordinator/SurroundCoordinator와
        // 같은 자리. 삭제 시 이 두 프로퍼티만 지우면 된다.
        public MoraleWaveCoordinator AllyWaveCoordinator => allyWaveCoordinator;
        public MoraleWaveCoordinator EnemyWaveCoordinator => enemyWaveCoordinator;

        public bool IsAllyWiped => aliveAllyCount <= 0;
        public bool IsEnemyWiped => aliveEnemyCount <= 0;
        // 패배 = 모든 마차 파괴 또는 전투 가능 아군(캐릭터+시설) 없음(Docs/기획/73번, 설계 74번 §2.3).
        public bool IsDefeated => DefeatCause != BattleDefeatCause.None;
        // 패배 원인(설계 79번 §4.1) - 전투 뒤 정산이 유닛 전멸 패배만 추가 화물 손실을 적용하려고 구분한다.
        public BattleDefeatCause DefeatCause => BattleDefeatRule.ResolveCause(totalWagonCount, aliveWagonCount, aliveAllyCount, aliveFacilityCount);

        public void Tick(float deltaTime)
        {
            // 방진선 재편성은 각 유닛 Tick 전에 실행돼야 한다(Docs/설계/12번 §12.2/§12.4) - 이번 틱
            // BlockingPositioningStrategy가 참조할 슬롯 위치를 유닛이 움직이기 전에 먼저 확정해둔다.
            frontlineCoordinator.Update(deltaTime, allies, protectedUnits);
            // 포위(Surround) 재편성도 같은 이유로 유닛 Tick 전에 실행(Docs/설계/12번 §13.3′) -
            // 반지름 축소/복원(§13.3′-4)이 시간 기반이라 deltaTime이 필요하다.
            rangedSurroundCoordinator.Update(deltaTime, allies);
            // 사기 파동도 유닛 Tick 전에 갱신한다(Docs/설계/14번 §6) - 진영별로 완전히 분리해 상대
            // 진영에는 영향이 없다.
            allyWaveCoordinator.Update(deltaTime, allies);
            enemyWaveCoordinator.Update(deltaTime, enemies);

            // 적의 타겟 후보 = 아군 전투원 + 보호 목표. protectedUnits를 빼먹으면 적이 Wagon/Facility를
            // 절대 공격하지 않아 "보호 목표 파괴 = 패배"(기획 §9)가 죽은 코드가 된다 - 아군에게는
            // 보호할 대상이 있지만 적에게는 없으므로(캐러밴만 Wagon/Facility를 가진다) 이 목록은
            // 적 쪽에만 필요하다. 매 틱 재구성하지만 지금 규모(아군+보호목표 최대 18)에선 무시할 만하다.
            // 잔해(extraEnemyTargets)도 같은 이유로 적 쪽에만 더한다 - 아군은 잔해를 표적으로 삼지 않는다(기획 77번 §4-13).
            var enemyTargets = new List<IDamageable>(allies.Count + protectedUnits.Count + extraEnemyTargets.Count);
            enemyTargets.AddRange(allies);
            enemyTargets.AddRange(protectedUnits);
            enemyTargets.AddRange(extraEnemyTargets);

            foreach (var unit in allies)
            {
                if (unit.IsAlive) unit.Tick(deltaTime, enemies, allies);
            }
            foreach (var unit in enemies)
            {
                if (unit.IsAlive) unit.Tick(deltaTime, enemyTargets, enemies);
            }
            // protectedUnits 자신은 Tick하지 않는다 - 이동/공격하지 않으므로(IDamageable, IBattleCombatant 아님).
        }
    }
}

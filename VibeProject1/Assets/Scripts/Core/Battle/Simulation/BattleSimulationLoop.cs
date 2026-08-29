using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 순수 C# 합성 객체. 아군/적/보호 목표 리스트를 들고 매 틱 각 유닛의 Tick을 돌리고, 전멸·보호
    /// 목표 파괴 여부를 노출한다. 생존 수는 매 프레임 리스트를 다시 스캔하지 않고 OnDied/OnFled
    /// 이벤트로 카운터를 유지한다.
    /// </summary>
    public class BattleSimulationLoop
    {
        private readonly List<IBattleCombatant> allies;
        private readonly List<IBattleCombatant> enemies;
        private readonly List<IDamageable> protectedUnits;
        // 방진 형성 조율자(Docs/설계/12번 §12.2) - PartyMorale과 같은 자리, 전투마다 새로 만들어진다.
        // null 가능성 없음 - tacticsReader가 없어 방향성 지시가 전부 비활성화된 전투에서도 코디네이터
        // 자체는 만들어지지만, Blocking 전열 후보가 하나도 없어(모든 RoleGroup이 null) Update가
        // 아무 것도 하지 않는 것으로 자연히 무해해진다(BattleCharacterUnit의 tacticsBehaviors=null
        // 폴백 패턴과 같은 방향).
        private readonly FrontlineFormationCoordinator frontlineCoordinator;
        // 포위(Surround) 조율자(Docs/설계/12번 §13.3) - frontlineCoordinator와 같은 자리·같은 이유.
        private readonly RangedSurroundCoordinator rangedSurroundCoordinator;
        private int aliveAllyCount;
        private int aliveEnemyCount;
        private int aliveProtectedCount;

        // 이번 전투의 전장 반지름(BattleFieldLayout 기준) - BattleViewPresenter가 전투 뷰 카메라의
        // 시야 경계를 잡을 때 쓴다(Docs/설계/13_전투뷰_월드오브젝트_전환_아키텍처.md). 시뮬레이션
        // 로직 자체는 이 값을 쓰지 않는다 - 렌더링 소비자를 위해 그대로 들고만 있는다.
        public float FieldRadius { get; }
        // 이번 전투의 스폰 반지름(FieldRadius보다 항상 더 바깥, BattleFieldLayout.ComputeSpawnRadius
        // 기준) - BattleBackgroundGridView가 적 스폰 링을 전부 감싸는 배경 타일 그리드 크기를 잡을 때
        // 쓴다(Docs/설계/13번, FieldRadius와 같은 자리·같은 이유로 추가).
        public float SpawnRadius { get; }

        public BattleSimulationLoop(
            List<IBattleCombatant> allies, List<IBattleCombatant> enemies, List<IDamageable> protectedUnits,
            float fieldRadius, float spawnRadius, FrontlineFormationCoordinator frontlineCoordinator, RangedSurroundCoordinator rangedSurroundCoordinator)
        {
            this.allies = allies;
            this.enemies = enemies;
            this.protectedUnits = protectedUnits;
            this.frontlineCoordinator = frontlineCoordinator;
            this.rangedSurroundCoordinator = rangedSurroundCoordinator;
            FieldRadius = fieldRadius;
            SpawnRadius = spawnRadius;
            aliveAllyCount = allies.Count;
            aliveEnemyCount = enemies.Count;
            aliveProtectedCount = protectedUnits.Count;

            // 사망(OnDied)과 도주(OnFled) 둘 다 "전장에서 사라짐"이므로 둘 다 카운트를 줄인다(기획 §7.4).
            foreach (var ally in allies) { ally.OnDied += () => aliveAllyCount--; ally.OnFled += () => aliveAllyCount--; }
            foreach (var enemy in enemies) { enemy.OnDied += () => aliveEnemyCount--; enemy.OnFled += () => aliveEnemyCount--; }
            foreach (var unit in protectedUnits) unit.OnDied += () => aliveProtectedCount--;
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

        public bool IsAllyWiped => aliveAllyCount <= 0;
        public bool IsEnemyWiped => aliveEnemyCount <= 0;
        // 전체가 아니라 "하나라도" 파괴되면 true - 기획 §9. 보호 목표가 0개(빈 슬롯)여도
        // aliveProtectedCount(0) < protectedUnits.Count(0)은 거짓이라 안전하다.
        public bool IsProtectionTargetDestroyed => aliveProtectedCount < protectedUnits.Count;

        public void Tick(float deltaTime)
        {
            // 방진선 재편성은 각 유닛 Tick 전에 실행돼야 한다(Docs/설계/12번 §12.2/§12.4) - 이번 틱
            // BlockingPositioningStrategy가 참조할 슬롯 위치를 유닛이 움직이기 전에 먼저 확정해둔다.
            frontlineCoordinator.Update(deltaTime, allies, protectedUnits);
            // 포위(Surround) 재편성도 같은 이유로 유닛 Tick 전에 실행(Docs/설계/12번 §13.3′) -
            // 반지름 축소/복원(§13.3′-4)이 시간 기반이라 deltaTime이 필요하다.
            rangedSurroundCoordinator.Update(deltaTime, allies);

            // 적의 타겟 후보 = 아군 전투원 + 보호 목표. protectedUnits를 빼먹으면 적이 Wagon/Facility를
            // 절대 공격하지 않아 "보호 목표 파괴 = 패배"(기획 §9)가 죽은 코드가 된다 - 아군에게는
            // 보호할 대상이 있지만 적에게는 없으므로(캐러밴만 Wagon/Facility를 가진다) 이 목록은
            // 적 쪽에만 필요하다. 매 틱 재구성하지만 지금 규모(아군+보호목표 최대 18)에선 무시할 만하다.
            var enemyTargets = new List<IDamageable>(allies.Count + protectedUnits.Count);
            enemyTargets.AddRange(allies);
            enemyTargets.AddRange(protectedUnits);

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

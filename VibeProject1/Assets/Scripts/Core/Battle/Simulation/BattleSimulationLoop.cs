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
        private int aliveAllyCount;
        private int aliveEnemyCount;
        private int aliveProtectedCount;

        public BattleSimulationLoop(
            List<IBattleCombatant> allies, List<IBattleCombatant> enemies, List<IDamageable> protectedUnits)
        {
            this.allies = allies;
            this.enemies = enemies;
            this.protectedUnits = protectedUnits;
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

        public bool IsAllyWiped => aliveAllyCount <= 0;
        public bool IsEnemyWiped => aliveEnemyCount <= 0;
        // 전체가 아니라 "하나라도" 파괴되면 true - 기획 §9. 보호 목표가 0개(빈 슬롯)여도
        // aliveProtectedCount(0) < protectedUnits.Count(0)은 거짓이라 안전하다.
        public bool IsProtectionTargetDestroyed => aliveProtectedCount < protectedUnits.Count;

        public void Tick(float deltaTime)
        {
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

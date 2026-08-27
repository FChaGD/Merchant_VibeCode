using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기획 §4/§9 - Wagon/Facility. 이동/공격하지 않고 피해만 받는다. IBattleCombatant가 아니라
    /// IDamageable만 구현한다 - Tick(이동/공격) 계약을 억지로 채울 필요가 없다(LSP).
    /// PartyMorale을 받지 않는다 - 기획 §7.2는 Character 유닛의 손실만 사기를 깎는다고 정의했다.
    /// Wagon/Facility 파괴는 사기와 무관하게 즉시 Defeat로 직결된다(BattleSimulationLoop).
    /// </summary>
    public class BattleProtectedUnit : IDamageable
    {
        public Vector2 Position { get; }
        public bool IsAlive => currentHp > 0f;
        public float Defense => 0f; // 기획 §4: 방어력 해당 없음(N/A)
        public float Attack => 0f; // 공격력 해당 없음(N/A) - 이동/공격하지 않는 대상.
        public float MaxHp { get; }
        public float CurrentHp => currentHp;
        // 정비창 팔레트에서 이미 쓰던 아이콘(마차=삼각형/시설=원형)을 그대로 - 뷰가 별도 도형을
        // 새로 만들지 않고 이 아이콘을 재사용한다.
        public Sprite Icon { get; }
        public event Action OnDied;
        public event Action<float> OnDamaged;

        private float currentHp;

        public BattleProtectedUnit(Vector2 position, float maxHp, Sprite icon)
        {
            Position = position;
            MaxHp = maxHp;
            Icon = icon;
            currentHp = maxHp;
        }

        // attacker는 쓰지 않는다 - 이동/공격하지 않는 대상이라 "누가 때렸는지"를 활용할 주체가
        // 없다(IDamageable 계약은 지키되 필요 없는 정보는 무시, LSP).
        public void TakeDamage(float amount, IBattleCombatant attacker)
        {
            if (!IsAlive) return;
            currentHp = Mathf.Max(0f, currentHp - amount);
            OnDamaged?.Invoke(amount);
            if (!IsAlive)
            {
                OnDied?.Invoke();
            }
        }
    }
}

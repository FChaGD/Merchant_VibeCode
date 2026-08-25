using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 피격 가능한 대상의 최소 계약. Character(IBattleCombatant로 확장)든 보호 대상
    /// (Wagon/Facility, BattleProtectedUnit)이든 이것만 구현하면 타겟이 될 수 있다 - "이동/공격 능력"
    /// (Tick)과 분리해둬야 이동하지 않는 대상이 억지로 빈 Tick을 구현하지 않아도 된다(LSP).
    /// </summary>
    public interface IDamageable
    {
        Vector2 Position { get; }
        bool IsAlive { get; }
        float Defense { get; }
        float MaxHp { get; }
        // 정비창 팔레트가 이미 쓰고 있는 실제 아이콘을 뷰가 재사용할 수 있도록 하는 힌트 - MaxHp와
        // 같은 성격(뷰 전용 힌트). null이면 뷰가 자체 Placeholder 색상으로 대체한다.
        Sprite Icon { get; }
        event Action OnDied;
        event Action<float> OnDamaged;
        void TakeDamage(float amount);
    }
}

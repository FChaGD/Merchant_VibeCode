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
        // 방향성 지시 타겟 선택자(HighestHpRatio/LowestHp)가 필요로 해서 추가 - MaxHp만으론 "지금
        // 얼마나 남았는지"를 알 수 없다(Docs/설계/12번 §5.4).
        float CurrentHp { get; }
        // 방향성 지시 타겟 선택자(HighestAttack)가 필요로 해서 추가 - 기존엔 BattleCharacterUnit
        // 내부에서만 쓰이던 값이었다(Docs/설계/12번 §5.5).
        float Attack { get; }
        // 방진 형성 로직(Docs/기획/12번 §3.2)이 "인식한 적의 최대 사거리"를 계산해야 해서 추가 -
        // Attack과 같은 이유로 IDamageable에 둔다(인식 목록이 IReadOnlyList&lt;IDamageable&gt;이라
        // IBattleCombatant로 캐스팅하지 않고 바로 읽을 수 있어야 함). 이동/공격하지 않는 보호 대상은
        // Attack과 같은 방식으로 0(N/A)을 반환한다.
        float Range { get; }
        // 정비창 팔레트가 이미 쓰고 있는 실제 아이콘을 뷰가 재사용할 수 있도록 하는 힌트 - MaxHp와
        // 같은 성격(뷰 전용 힌트). null이면 뷰가 자체 Placeholder 색상으로 대체한다.
        Sprite Icon { get; }
        event Action OnDied;
        event Action<float> OnDamaged;
        // attacker는 방향성 지시 피격 인식("근접 또는 피격", Docs/기획/12번 §2.1)이 "누가 때렸는지"를
        // 알아야 해서 추가됐다 - BattleProtectedUnit처럼 이 정보가 필요 없는 구현체는 그냥 무시한다
        // (LSP, Docs/설계/12번 §5.1).
        void TakeDamage(float amount, IBattleCombatant attacker);
    }
}

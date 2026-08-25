using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public interface IBattleCombatant : IDamageable
    {
        bool IsAlly { get; }
        bool IsFleeing { get; }
        // 도주 방향×이동속도 - OnFled 이후 뷰가 시뮬레이션 Tick 없이도 같은 방향으로 계속 이동하는
        // 페이드아웃 연출을 만들 수 있도록 하는 뷰 전용 힌트(Icon/MaxHp와 같은 성격). 도주 시작 전에는
        // 의미 없는 값(0벡터)이다.
        Vector2 FleeVelocity { get; }
        event Action OnFled;
        event Action<IDamageable> OnAttacked;
        void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits);
    }
}

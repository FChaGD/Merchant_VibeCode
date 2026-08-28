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
        // 방진 형성 로직(Docs/기획/12번 §3.2)이 "이 적이 지금 보호대상을 타겟팅 중인지" 판정해야 해서
        // 추가 - IsFleeing과 같은 성격의 전투 행동 상태 노출(IDamageable이 아니라 여기 두는 이유: 오직
        // Tick/공격 능력이 있는 대상만 타겟 개념이 있다). 타겟이 없으면 null.
        IDamageable CurrentTarget { get; }
        event Action OnFled;
        event Action<IDamageable> OnAttacked;
        void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits);
    }
}

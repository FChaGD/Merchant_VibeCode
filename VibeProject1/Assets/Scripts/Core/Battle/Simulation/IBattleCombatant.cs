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
        // 방진 형성 로직(Docs/설계/12번 §12.3)이 "이 아군이 보호대상 후보군(RangedDealer/Supporter)에
        // 속하는지" 판정해야 해서 추가 - 방향성 지시 미적용 유닛(적, 또는 아군이라도 tacticsBehaviors
        // 없음)은 역할군 개념이 없어 null(N/A)을 반환한다.
        RoleGroup? RoleGroup { get; }
        // 방진 형성 로직(Docs/설계/12번 §12.4)이 "LocalPositioning.Blocking을 고른 전열 유닛"을
        // 식별해야 해서 추가 - RoleGroup과 같은 이유로 null 가능(적, 또는 방향성 지시 미적용 유닛).
        LocalPositioning? Positioning { get; }
        // 방진 형성 로직(Docs/설계/12번 §12.4)이 각 유닛 Tick 이전(코디네이터 Update 시점)에 이미
        // 인식된 적 목록을 읽어야 해서 추가 - IEnemyRecognitionTracker.RecognizedSnapshot을 그대로
        // 전달한다. 방향성 지시 미적용 유닛(적)은 빈 컬렉션(N/A) - Attack/Range와 같은 패턴.
        IReadOnlyCollection<IDamageable> RecognizedEnemies { get; }
        event Action OnFled;
        event Action<IDamageable> OnAttacked;
        void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits);
    }
}

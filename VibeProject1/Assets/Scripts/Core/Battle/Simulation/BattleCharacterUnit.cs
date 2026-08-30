using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 중에만 존재하는 순수 데이터+행동 객체 - Unity 생명주기가 필요 없어 MonoBehaviour가 아니다.
    /// Docs/기획/08_전투_해석로직_기획.md §5.1(최근접 타겟팅)·§5.1-1(재탐색 조건 확장)·§5.2(이동-공격
    /// 사이클)·§7.1~§7.3(사기 동기화·도주)을 구현한다. 타겟 고정(살아있는 타겟이 있는 동안 재탐색하지
    /// 않음)은 매 프레임 O(n) 재탐색을 피하기 위한 의도된 선택이라, "가장 가까운 적"이 엄밀히는 "타겟
    /// 획득 시점 기준 최근접"으로 완화되어 적용된다 - 단, 사거리 밖에서(접근 중) 피격당하면 예외적으로
    /// 재탐색한다(retargetRequested, Docs/설계/06번 §10-9). 공간 탐색(최근접 타겟·근접 반발)은
    /// IUnitSpatialQuery에 위임한다 - 전투 규모가 커져 탐색 방식을 공간 분할로 바꿔도 이 클래스는
    /// 무변경이다(OCP).
    ///
    /// 방향성 지시(Docs/설계/12번)는 tacticsBehaviors가 null이 아닐 때만 적용된다 - 이번 설계는
    /// 아군에만 적용되고(§0), 적 유닛은 tacticsBehaviors를 null로 받아 기존 동작(TickEngageWithoutTactics)
    /// 을 그대로 유지한다. 두 경로를 분리한 이유는 적 진영 동작을 절대 건드리지 않기 위함이다.
    /// </summary>
    public class BattleCharacterUnit : IBattleCombatant
    {
        // 같은 진영 유닛끼리 이 거리 안으로 들어오면 서로 밀어낸다(ApplySeparation) - 기획 문서에
        // 없어 새로 제안하는 연출용 테스트 값. §2의 대형 간격(RowSpacing 1.5/ColumnSpacing 1)과
        // 비슷한 크기로 잡았다.
        private const float SeparationRadius = 1f;
        private const float SeparationSpeed = 3f;
        // Returning 상태 종료(배치 위치 도착) 판정 허용 오차.
        private const float ReturnArrivalDistance = 0.5f;

        public Vector2 Position { get; private set; }
        public bool IsAlly { get; }
        // hasLeftBattle이 true가 되면(도주 완료) 더 이상 전투 참가자가 아니다 - HP는 남아있어도 IsAlive는 false.
        public bool IsAlive => currentHp > 0f && !hasLeftBattle;
        public bool IsFleeing => isFleeing;
        public Vector2 FleeVelocity => fleeDirection * stats.MoveSpeed;
        public float Defense => stats.Defense;
        public float Attack => stats.Attack;
        public float Range => stats.Range;
        public float MaxHp => stats.MaxHp;
        public float CurrentHp => currentHp;
        // 방진 형성 로직(Docs/기획/12번 §3.2)이 "인식한 적이 보호대상을 타겟팅 중인지" 판정해야 해서
        // 노출 - 타겟이 없으면 null.
        public IDamageable CurrentTarget => target;
        // 방진 형성 로직(Docs/설계/12번 §12.3)이 보호대상 후보군 판정에 써야 해서 노출 - 방향성
        // 지시 미적용 유닛(적)은 tacticsBehaviors가 null이라 자연히 null(N/A)이 된다.
        public RoleGroup? RoleGroup => tacticsBehaviors?.RoleGroup;
        // 방진 형성 로직(Docs/설계/12번 §12.4)이 "Blocking 전열 유닛"을 식별해야 해서 노출.
        public LocalPositioning? Positioning => tacticsBehaviors?.Positioning;
        // 방진 형성 로직(Docs/설계/12번 §12.4)이 코디네이터 Update 시점(이 유닛의 이번 틱 Tick 전)에
        // 읽어야 해서 노출 - TickAndGetRecognized를 또 호출하지 않도록 스냅샷만 전달한다.
        public IReadOnlyCollection<IDamageable> RecognizedEnemies => tacticsBehaviors?.RecognitionTracker.RecognizedSnapshot ?? Array.Empty<IDamageable>();
        // 디버깅 전용(Assets/Scripts/Core/Debug/Battle/BattleMoveTargetGizmoView) - IBattleCombatant.
        // DebugMoveTarget 참고. 삭제 시 이 필드/프로퍼티와 아래 대입 3곳(MoveTowardTacticalDestination/
        // TickEngageWithoutTactics/TickReturning)만 지우면 된다 - 다른 로직은 이 값을 읽지 않는다.
        public Vector2? DebugMoveTarget => debugMoveTarget;
        private Vector2? debugMoveTarget;
        // Character는 아직 직업별 팔레트 아이콘(사각형/오각형/육각형)을 전투 뷰에 재사용하지 않는다
        // (이번 요청 범위 밖 - 마차/시설만 재사용). 뷰는 null이면 기존 단색 도형으로 대체한다.
        public Sprite Icon => null;
        public event Action OnDied;
        public event Action OnFled;
        public event Action<float> OnDamaged;
        public event Action<IDamageable> OnAttacked;

        private readonly BattleUnitStats stats;
        private readonly IDamageFormula damageFormula;
        private readonly PartyMorale partyMorale;
        private readonly IUnitSpatialQuery spatialQuery;
        // 대형 크기(스폰 반지름)에 연동되어 전투마다 달라진다 - BattleFieldLayout.ComputeFleeTravelDistance
        // 참고. 전투 시작 시점에 한 번 계산해 전달받고, 도주 중에는 값이 바뀌지 않는다.
        private readonly float fleeTravelDistance;
        // null이면 방향성 지시 미적용(적 유닛) - TickEngageWithoutTactics로 분기(Docs/설계/12번 §0).
        private readonly UnitTacticsBehaviors tacticsBehaviors;

        private float currentHp;
        private float unitMorale = MoraleTuning.Initial;
        private bool isFleeing;
        private bool hasLeftBattle;
        private float fledDistance;
        private Vector2 fleeDirection;
        private IDamageable target;
        // 타겟이 사거리 밖(접근 중)인 상태에서 피격당하면 세팅된다 - 다음 Tick 진입 시 재탐색 분기가
        // 소비한다(기획 08번 §5.1-1, 설계 06번 §10-9). "누구로" 재탐색할지는 그대로, "언제"만 이 플래그로
        // 하나 더 열어준다.
        private bool retargetRequested;
        private float attackCooldown;
        // 활동 반경 밖에서 IPursuitPolicy가 트리거해 배치 위치로 복귀 중인 상태(Docs/설계/12번 §4) -
        // Fleeing보다는 낮고 통상 Engaging보다는 높은 우선순위.
        private bool isReturning;

        public BattleCharacterUnit(
            Vector2 startPosition, bool isAlly, BattleUnitStats stats, IDamageFormula damageFormula,
            PartyMorale partyMorale, IUnitSpatialQuery spatialQuery, float fleeTravelDistance,
            UnitTacticsBehaviors tacticsBehaviors = null)
        {
            Position = startPosition;
            IsAlly = isAlly;
            this.stats = stats;
            this.damageFormula = damageFormula;
            this.partyMorale = partyMorale;
            this.spatialQuery = spatialQuery;
            this.fleeTravelDistance = fleeTravelDistance;
            this.tacticsBehaviors = tacticsBehaviors;
            currentHp = stats.MaxHp;
        }

        public void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            if (!IsAlive) return;

            ApplyRegen(deltaTime);
            TickMorale(deltaTime, targets);
            if (isFleeing)
            {
                TickFlee(deltaTime);
                ApplySeparation(sameSideUnits, deltaTime);
                return;
            }

            if (isReturning)
            {
                TickReturning(deltaTime);
                ApplySeparation(sameSideUnits, deltaTime);
                return;
            }

            if (tacticsBehaviors != null)
            {
                TickEngageWithTactics(deltaTime, targets, sameSideUnits);
            }
            else
            {
                TickEngageWithoutTactics(deltaTime, targets);
            }

            ApplySeparation(sameSideUnits, deltaTime);
        }

        // 최근접 스티키 타겟팅, 사거리 밖이면 직진 접근, 안이면 공격 - 단 사거리 밖에서 피격당하면
        // retargetRequested(§5.1-1)로 재탐색한다.
        private void TickEngageWithoutTactics(float deltaTime, IReadOnlyList<IDamageable> targets)
        {
            if (target is not { IsAlive: true } || retargetRequested)
            {
                target = spatialQuery.FindNearest(Position, targets);
                retargetRequested = false;
                if (target == null) return;
            }

            var distance = (target.Position - Position).magnitude;
            if (distance > stats.Range)
            {
                debugMoveTarget = target.Position;
                MoveToward(target.Position, deltaTime);
            }
            else
            {
                debugMoveTarget = null;
                TryAttack(deltaTime, target);
            }
        }

        // 방향성 지시가 적용되는 아군 동작. 스티키 타겟팅은 그대로 유지한다 - 타겟이 죽었거나,
        // IPursuitPolicy가 이탈을 트리거했거나, retargetRequested(사거리 밖에서 피격, §5.1-1)일
        // 때만 재선택한다(매 틱 재선택하지 않음, Docs/설계/12번 §7 점검 이력 - 최적화).
        private void TickEngageWithTactics(float deltaTime, IReadOnlyList<IDamageable> allEnemies, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            var recognized = tacticsBehaviors.RecognitionTracker.TickAndGetRecognized(deltaTime, allEnemies, tacticsBehaviors.RadiusZone);

            if (target is not { IsAlive: true } || retargetRequested)
            {
                target = tacticsBehaviors.TargetSelector.Select(Position, recognized);
                retargetRequested = false;
                if (target == null) return;
            }

            var isOutsideRadius = !tacticsBehaviors.RadiusZone.Contains(Position);
            var justLandedHit = TryAttack(deltaTime, target);
            MoveTowardTacticalDestination(deltaTime, target, sameSideUnits);

            if (tacticsBehaviors.PursuitPolicy.ShouldDisengage(deltaTime, isOutsideRadius, justLandedHit))
            {
                Disengage(recognized);
            }
        }

        // 트리거되면 인식된 적 중 반경 내의 적으로 재타겟하고, 없으면 타겟을 비우고 복귀 상태로
        // 전환한다(Docs/기획/12번 §2.3).
        private void Disengage(IReadOnlyList<IDamageable> recognized)
        {
            var withinRadius = new List<IDamageable>();
            foreach (var candidate in recognized)
            {
                if (tacticsBehaviors.RadiusZone.Contains(candidate.Position))
                {
                    withinRadius.Add(candidate);
                }
            }

            target = withinRadius.Count > 0 ? tacticsBehaviors.TargetSelector.Select(Position, withinRadius) : null;
            isReturning = target == null;
        }

        // 자기보호가 발동 중이면 포지셔닝을 덮어쓴다 - Kiting과 Stationary처럼 서로 반대되는 값이
        // 같은 유닛에 함께 선택될 수 있어, 이 우선순위로 모순을 해소한다(Docs/설계/12번 §4). 최종
        // 목적지는 HoldPosition 전용 clamp(활동 반경) → 전장 경계 clamp(프리셋 무관, §2.2-1) 순으로
        // 한 번씩 더 거친다.
        private void MoveTowardTacticalDestination(float deltaTime, IDamageable currentTarget, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            var desiredDestination = tacticsBehaviors.SelfPreservationModifier.TryGetOverrideMovement(
                deltaTime, Position, currentTarget, stats.Range, out var overrideDestination)
                ? overrideDestination
                : tacticsBehaviors.PositioningStrategy.ComputeMoveTarget(this, Position, currentTarget, stats.Range, tacticsBehaviors.HomePosition, sameSideUnits);

            var clampedDestination = tacticsBehaviors.PursuitPolicy.ClampDestination(desiredDestination, tacticsBehaviors.RadiusZone);
            // 전장 경계 하드 캡(Docs/기획/12번 §2.2-1) - 추적 프리셋이 무엇이든 상관없이 항상 적용된다.
            // 카메라가 못 가는 곳은 캐릭터도 스스로 못 간다는 원칙(사기 도주 TickFlee는 별도 경로라 예외).
            clampedDestination = tacticsBehaviors.FieldBoundaryZone.ClampToZone(clampedDestination);
            debugMoveTarget = clampedDestination;
            MoveToward(clampedDestination, deltaTime);
        }

        // 배치 위치로 복귀한다 - 도착하면 Returning을 해제하고 다음 틱부터 통상 Engaging으로
        // 돌아간다(타겟이 없으면 기존 널 타겟 분기가 자연히 재탐색한다).
        private void TickReturning(float deltaTime)
        {
            debugMoveTarget = tacticsBehaviors.HomePosition;
            MoveToward(tacticsBehaviors.HomePosition, deltaTime);
            if ((Position - tacticsBehaviors.HomePosition).sqrMagnitude <= ReturnArrivalDistance * ReturnArrivalDistance)
            {
                isReturning = false;
            }
        }

        // 사거리 안일 때만 공격하고 쿨다운을 소모한다 - 명중 여부(justLandedHit)를 반환해
        // IPursuitPolicy.ShouldDisengage(OffensiveJudgment)가 참조할 수 있게 한다.
        private bool TryAttack(float deltaTime, IDamageable currentTarget)
        {
            var distance = (currentTarget.Position - Position).magnitude;
            if (distance > stats.Range) return false;

            attackCooldown -= deltaTime;
            if (attackCooldown > 0f) return false;

            currentTarget.TakeDamage(damageFormula.ComputeDamage(stats.Attack, currentTarget.Defense), this);
            OnAttacked?.Invoke(currentTarget);
            attackCooldown = stats.AttackInterval;
            return true;
        }

        private void MoveToward(Vector2 destination, float deltaTime)
        {
            var toDestination = destination - Position;
            if (toDestination.sqrMagnitude > 0.0001f)
            {
                Position += toDestination.normalized * stats.MoveSpeed * deltaTime;
            }
        }

        // 같은 진영 유닛끼리 SeparationRadius 안으로 겹치면 서로 밀어낸다 - 모든 적이 같은 스폰
        // 지점에서 동일한 스탯/타겟팅으로 출발해 좌표가 완전히 겹친 채로 움직이는 문제(육안상 "적이
        // 하나로 뭉쳐 보임")를 해결한다. 실제 공간 탐색(반경 안 이웃 찾기)은 spatialQuery에 위임한다 -
        // BattleCharacterUnit은 "얼마나 빨리 밀려날지"(SeparationSpeed)만 알고, "누가 이웃인지 어떻게
        // 찾을지"는 몰라도 된다(OCP - 전투 규모가 커져 공간 분할 구현체로 교체해도 이 클래스는 무변경).
        private void ApplySeparation(IReadOnlyList<IBattleCombatant> sameSideUnits, float deltaTime)
        {
            var pushOut = spatialQuery.ComputeSeparationPush(this, Position, SeparationRadius, sameSideUnits);
            if (pushOut != Vector2.zero)
            {
                Position += pushOut * SeparationSpeed * deltaTime;
            }
        }

        // 괴수 타입 전용 지속 재생(기획 08번 §13.1) - 교전/도주/복귀 상태와 무관하게 항상 적용한다.
        // 나머지 타입은 HpRegenPerSecond가 0이라 조기 반환되어 비용이 없다.
        private void ApplyRegen(float deltaTime)
        {
            if (stats.HpRegenPerSecond <= 0f) return;
            currentHp = Mathf.Min(stats.MaxHp, currentHp + stats.HpRegenPerSecond * deltaTime);
        }

        // 매초 PartyMorale 쪽으로 다가가고, 임계치 이하로 떨어지면 도주를 시작한다.
        private void TickMorale(float deltaTime, IReadOnlyList<IDamageable> targets)
        {
            unitMorale = Mathf.MoveTowards(unitMorale, partyMorale.CurrentValue, MoraleTuning.SyncRatePerSecond * deltaTime);
            if (!isFleeing && unitMorale <= MoraleTuning.FleeThreshold)
            {
                isFleeing = true;
                fleeDirection = ComputeFleeDirection(targets);
            }
        }

        // "필드 중심(원점)→자기 위치" 방향과 "가장 가까운 적→자기 위치" 방향을 더해 도주 방향을 정한다.
        // 원점 기준 방향만 쓰면, 정작 자신을 위협하는 적이 옆이나 앞쪽에 있어도 그걸 등지지 않고
        // 엉뚱한 방향으로 도망칠 수 있다 - 두 방향을 합치면 "전장 밖으로" + "위협을 등지고"가 함께
        // 반영된다. 두 방향이 서로 상쇄되거나(반대로 정확히 겹침) 애초에 계산할 수 없는(원점 위치+
        // 주변에 적 없음) 예외적 경우만 무작위 방향으로 대체한다(ApplySeparation의 겹침 처리와 동일한 패턴).
        private Vector2 ComputeFleeDirection(IReadOnlyList<IDamageable> targets)
        {
            var awayFromCenter = Position.sqrMagnitude > 0.0001f ? Position.normalized : Vector2.zero;

            var nearestEnemy = spatialQuery.FindNearest(Position, targets);
            var awayFromNearestEnemy = Vector2.zero;
            if (nearestEnemy != null)
            {
                var toSelf = Position - nearestEnemy.Position;
                awayFromNearestEnemy = toSelf.sqrMagnitude > 0.0001f ? toSelf.normalized : Vector2.zero;
            }

            var combined = awayFromCenter + awayFromNearestEnemy;
            return combined.sqrMagnitude > 0.0001f ? combined.normalized : UnityEngine.Random.insideUnitCircle.normalized;
        }

        // fleeDirection으로 이동만 한다 - 타겟팅/공격 없음(기획 §7.3 "이동/공격을 멈추고").
        private void TickFlee(float deltaTime)
        {
            var step = stats.MoveSpeed * deltaTime;
            Position += fleeDirection * step;
            fledDistance += step;

            if (fledDistance >= fleeTravelDistance)
            {
                hasLeftBattle = true;
                partyMorale.NotifyUnitLost(); // §7.2 - 도주도 "전투 불능"에 포함된다
                OnFled?.Invoke();
            }
        }

        public void TakeDamage(float amount, IBattleCombatant attacker)
        {
            if (!IsAlive) return;
            currentHp = Mathf.Max(0f, currentHp - amount);
            OnDamaged?.Invoke(amount);
            // 재탐색 트리거(기획 08번 §5.1-1) - 타겟이 사거리 밖(접근 중)인 상태에서 피격당하면 다음
            // Tick에서 재탐색한다. 이미 사거리 안에서 교전 중이면 이 거리 비교 자체가 성립하지 않아
            // 별도 분기 없이 자동으로 무시된다.
            if (target is { IsAlive: true } && (target.Position - Position).magnitude > stats.Range)
            {
                retargetRequested = true;
            }
            // 피격 인식("근접 또는 피격", 기획 §2.1) - 공격자는 항상 즉시 인식된다. 인식 유형이
            // 시간/거리 기반이라도, 이미 나를 공격한 상대를 모른 척할 이유가 없어 유형과 무관하게
            // 적용한다(EnemyRecognitionTrackerBase.NotifyAttackedBy가 그 개체만 recognized에 추가).
            tacticsBehaviors?.RecognitionTracker.NotifyAttackedBy(attacker);
            // 자기보호(FallBackOnHeavyDamage/RetreatOnHit)가 피격 시점을 알아야 한다 - MaxHp가 0
            // 이하일 일은 없으니(스탯 검증은 범위 밖) 나눗셈 가드는 생략.
            tacticsBehaviors?.SelfPreservationModifier.NotifyDamaged(amount, currentHp / MaxHp);
            if (currentHp <= 0f)
            {
                partyMorale.NotifyUnitLost();
                OnDied?.Invoke();
            }
        }

    }
}

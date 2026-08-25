using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 중에만 존재하는 순수 데이터+행동 객체 - Unity 생명주기가 필요 없어 MonoBehaviour가 아니다.
    /// Docs/기획/08_전투_해석로직_기획.md §5.1(최근접 타겟팅)·§5.2(이동-공격 사이클)·
    /// §7.1~§7.3(사기 동기화·도주)을 구현한다. 타겟 고정(살아있는 타겟이 있는 동안 재탐색하지 않음)은
    /// 매 프레임 O(n) 재탐색을 피하기 위한 의도된 선택이라, "가장 가까운 적"이 엄밀히는 "타겟 획득
    /// 시점 기준 최근접"으로 완화되어 적용된다. 공간 탐색(최근접 타겟·근접 반발)은 IUnitSpatialQuery에
    /// 위임한다 - 전투 규모가 커져 탐색 방식을 공간 분할로 바꿔도 이 클래스는 무변경이다(OCP).
    /// </summary>
    public class BattleCharacterUnit : IBattleCombatant
    {
        // 같은 진영 유닛끼리 이 거리 안으로 들어오면 서로 밀어낸다(ApplySeparation) - 기획 문서에
        // 없어 새로 제안하는 연출용 테스트 값. §2의 대형 간격(RowSpacing 1.5/ColumnSpacing 1)과
        // 비슷한 크기로 잡았다.
        private const float SeparationRadius = 1f;
        private const float SeparationSpeed = 3f;

        public Vector2 Position { get; private set; }
        public bool IsAlly { get; }
        // hasLeftBattle이 true가 되면(도주 완료) 더 이상 전투 참가자가 아니다 - HP는 남아있어도 IsAlive는 false.
        public bool IsAlive => currentHp > 0f && !hasLeftBattle;
        public bool IsFleeing => isFleeing;
        public Vector2 FleeVelocity => fleeDirection * stats.MoveSpeed;
        public float Defense => stats.Defense;
        public float MaxHp => stats.MaxHp;
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

        private float currentHp;
        private float unitMorale = MoraleTuning.Initial;
        private bool isFleeing;
        private bool hasLeftBattle;
        private float fledDistance;
        private Vector2 fleeDirection;
        private IDamageable target;
        private float attackCooldown;

        public BattleCharacterUnit(
            Vector2 startPosition, bool isAlly, BattleUnitStats stats, IDamageFormula damageFormula,
            PartyMorale partyMorale, IUnitSpatialQuery spatialQuery, float fleeTravelDistance)
        {
            Position = startPosition;
            IsAlly = isAlly;
            this.stats = stats;
            this.damageFormula = damageFormula;
            this.partyMorale = partyMorale;
            this.spatialQuery = spatialQuery;
            this.fleeTravelDistance = fleeTravelDistance;
            currentHp = stats.MaxHp;
        }

        public void Tick(float deltaTime, IReadOnlyList<IDamageable> targets, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            if (!IsAlive) return;

            TickMorale(deltaTime, targets);
            if (isFleeing)
            {
                TickFlee(deltaTime);
                ApplySeparation(sameSideUnits, deltaTime);
                return;
            }

            if (target is not { IsAlive: true })
            {
                target = spatialQuery.FindNearest(Position, targets);
                if (target == null)
                {
                    ApplySeparation(sameSideUnits, deltaTime);
                    return;
                }
            }

            var toTarget = target.Position - Position;
            var distance = toTarget.magnitude;

            if (distance > stats.Range)
            {
                Position += toTarget.normalized * stats.MoveSpeed * deltaTime;
            }
            else
            {
                attackCooldown -= deltaTime;
                if (attackCooldown <= 0f)
                {
                    target.TakeDamage(damageFormula.ComputeDamage(stats.Attack, target.Defense));
                    OnAttacked?.Invoke(target);
                    attackCooldown = stats.AttackInterval;
                }
            }

            ApplySeparation(sameSideUnits, deltaTime);
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

        public void TakeDamage(float amount)
        {
            if (!IsAlive) return;
            currentHp = Mathf.Max(0f, currentHp - amount);
            OnDamaged?.Invoke(amount);
            if (currentHp <= 0f)
            {
                partyMorale.NotifyUnitLost();
                OnDied?.Invoke();
            }
        }

    }
}

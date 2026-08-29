using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 포위(Surround) 조율자 - 전투 1회당 1개, BattleSimulationLoop이 필드로 소유한다
    /// (FrontlineFormationCoordinator와 같은 자리, Docs/설계/12번 §13.3′). 1차 설계(그룹화 키 =
    /// 개별 유닛의 CurrentTarget)는 실전투 3차 패치로도 "같은 군집 안에서 다른 개체를 타겟팅하면
    /// 그룹도 갈라져 군집 전체를 못 둘러싼다"는 구조적 한계를 못 풀어 전면 폐기됐다(2026-08-29) -
    /// 그룹화 키를 (적 군집, 사거리 값) 쌍으로 바꿔, 같은 군집을 노리는 원거리딜러가 서로 다른
    /// 개체를 타겟팅해도 하나의 포위망(SurroundRing)을 공유하게 한다.
    /// </summary>
    public class RangedSurroundCoordinator
    {
        // Blocking과 같은 이유로 개별 유닛의 RadiusZone(프리셋별로 다를 수 있음)에 접근할 방법이
        // 없어(IBattleCombatant가 이를 노출하지 않음), FrontlineFormationCoordinator와 동일하게
        // "표준 활동 반경" 하나로 근사한다(Docs/설계/12번 §12.3-1과 같은 판단).
        private readonly StandardActivityRadiusZone standardRadiusZone;
        // 군집화 알고리즘(Union-Find, ClusterMergeDistanceMeters)을 재사용하기 위한 참조 - 완전히
        // 같은 클러스터 인스턴스를 공유하진 않지만(Frontline은 Blocking 유닛 인식 유니온을, 여기는
        // Surround 유닛 인식 유니온을 각각 같은 알고리즘에 넣는 구조), "로직 공유"는 이걸로 충분하다.
        private readonly FrontlineFormationCoordinator frontlineCoordinator;

        private readonly List<SurroundRing> activeRings = new();
        public IReadOnlyList<SurroundRing> ActiveRings => activeRings;

        // 재사용 버퍼(§12.8과 같은 이유) - 매 틱 새 List를 할당하지 않는다.
        private readonly List<IBattleCombatant> removalBuffer = new();
        private readonly List<IBattleCombatant> surroundAlliesBuffer = new();
        private readonly List<IDamageable> globalRecognizedBuffer = new();

        public RangedSurroundCoordinator(float standardActivityRadius, FrontlineFormationCoordinator frontlineCoordinator)
        {
            standardRadiusZone = new StandardActivityRadiusZone(standardActivityRadius);
            this.frontlineCoordinator = frontlineCoordinator;
        }

        /// <summary>이 유닛이 현재 배정된 링을 조회한다(접근 중이든 합류 완료든 상관없음) - 없으면 false.</summary>
        public bool TryGetRing(IBattleCombatant self, out SurroundRing ring)
        {
            foreach (var candidate in activeRings)
            {
                if (candidate.AssignedUnits.Contains(self))
                {
                    ring = candidate;
                    return true;
                }
            }
            ring = null;
            return false;
        }

        /// <summary>
        /// 각 유닛 Tick 이전에 실행된다(§12.2와 같은 타이밍 이유 - 이번 틱 SurroundPositioningStrategy가
        /// 참조할 링 상태를 유닛이 움직이기 전에 먼저 확정해둔다). 기존 링 재조정 → 반지름 축소/복원
        /// → 미배정 풀 신규 합류 → 합류(콜리전 대체) 판정 순서로 처리한다.
        /// </summary>
        public void Update(float deltaTime, IReadOnlyList<IBattleCombatant> allies)
        {
            ReconcileExistingRings();
            UpdateRingRadii(deltaTime);
            AssignUnassignedPool(allies);
            DetectJoins();
        }

        // 사망/도주/타겟 상실 유닛 제거 → 남은 유닛들의 인식 목록으로 RecognizedUnion·ClusterCenter·
        // ClusterBoundingRadius 재계산(FrontlineFormationCoordinator.ComputeRecognizedUnion과 같은
        // 패턴) → 텅 비면 해체(소속 유닛은 다음 틱 미배정 풀에서 재평가).
        private void ReconcileExistingRings()
        {
            for (var i = activeRings.Count - 1; i >= 0; i--)
            {
                var ring = activeRings[i];

                removalBuffer.Clear();
                foreach (var member in ring.AssignedUnits)
                {
                    if (!member.IsAlive || member.IsFleeing || member.CurrentTarget == null)
                    {
                        removalBuffer.Add(member);
                    }
                }
                foreach (var member in removalBuffer)
                {
                    ring.AssignedUnits.Remove(member);
                    ring.Joined.Remove(member);
                }

                RecomputeRingGeometry(ring);

                if (ring.RecognizedUnion.Count == 0)
                {
                    activeRings.RemoveAt(i);
                }
            }
        }

        private static void RecomputeRingGeometry(SurroundRing ring)
        {
            ring.RecognizedUnion.Clear();
            foreach (var member in ring.AssignedUnits)
            {
                foreach (var enemy in member.RecognizedEnemies)
                {
                    if (enemy.IsAlive) AddDistinct(ring.RecognizedUnion, enemy);
                }
            }

            if (ring.RecognizedUnion.Count == 0) return;

            ring.ClusterCenter = ComputeAveragePosition(ring.RecognizedUnion);
            ring.ClusterBoundingRadius = ComputeBoundingRadius(ring.ClusterCenter, ring.RecognizedUnion);
        }

        // §13.3′-4 - 합류(Joined) 멤버 중 공격 불가(타겟 없음 또는 실거리가 사거리 밖) 상태가
        // 하나라도 있으면 이상적 반지름(ClusterBoundingRadius+DealerRange)에서 하한
        // (ClusterBoundingRadius)까지 서서히 줄이고, 없으면 다시 이상적 반지름으로 서서히 되돌린다
        // (양방향 서서히 - 순간 스냅 없음). 이미 공격 가능한 멤버는 이 값과 무관하게 움직이지
        // 않는다(SurroundPositioningStrategy가 Joined+공격가능이면 정지 반환) - 공격 불가 멤버만
        // 매 틱 이 CurrentRadius를 좇아간다.
        private void UpdateRingRadii(float deltaTime)
        {
            foreach (var ring in activeRings)
            {
                var hasUnableToAttack = false;
                foreach (var member in ring.Joined)
                {
                    if (!CanAttack(member)) { hasUnableToAttack = true; break; }
                }

                var idealRadius = ring.ClusterBoundingRadius + ring.DealerRange;
                var targetRadius = hasUnableToAttack ? ring.ClusterBoundingRadius : idealRadius;
                var step = TacticsTuning.SurroundRingShrinkSpeedMetersPerSecond * deltaTime;
                ring.CurrentRadius = Mathf.MoveTowards(ring.CurrentRadius, targetRadius, step);
            }
        }

        private static bool CanAttack(IBattleCombatant member)
        {
            return member.CurrentTarget is { IsAlive: true }
                && (member.CurrentTarget.Position - member.Position).sqrMagnitude <= member.Range * member.Range;
        }

        // 신규 합류(§13.3′ 군집 선택 기준 - 자기 CurrentTarget이 속한 군집). Positioning==Surround
        // 이고 아직 어느 링에도 배정되지 않은 유닛을 순회해, 이번 틱 전역 클러스터링(모든 Surround
        // 유닛의 인식 목록 합집합 - 어떤 캐릭터가 인식하든 클러스터링 대상으로 삼는다는 요구사항)
        // 기준으로 내 타겟이 속한 군집을 먼저 찾고, 그 군집·사거리에 맞는 기존 링을 찾거나 새로
        // 만든다. 클러스터링은 배정이 실제로 필요한 시점에만 계산한다(매 틱 불필요한 계산 방지).
        //
        // 예전 버전은 "내 타겟이 기존 링의 RecognizedUnion(그 링 소속 유닛들만의 인식 목록)에 이미
        // 있는지"로 매칭했다 - 같은 군집·같은 사거리인 궁수 A/B가 서로 다른 개체를 타겟팅하면, B의
        // 타겟이 A의 인식 목록에 없을 수 있어 매칭에 실패하고 별도 링이 생기는 버그가 실전투에서
        // 확인됐다(2026-08-29, 사용자 보고). 전역 클러스터링 결과로 매칭해 이 문제를 없앤다.
        private void AssignUnassignedPool(IReadOnlyList<IBattleCombatant> allies)
        {
            surroundAlliesBuffer.Clear();
            globalRecognizedBuffer.Clear();
            var hasUnassignedCandidate = false;
            foreach (var ally in allies)
            {
                if (!ally.IsAlive || ally.IsFleeing) continue;
                if (ally.Positioning != LocalPositioning.Surround) continue;
                surroundAlliesBuffer.Add(ally);
                if (ally.CurrentTarget != null && !IsAssigned(ally)) hasUnassignedCandidate = true;
                foreach (var enemy in ally.RecognizedEnemies)
                {
                    if (enemy.IsAlive) AddDistinct(globalRecognizedBuffer, enemy);
                }
            }

            if (!hasUnassignedCandidate) return; // 클러스터링을 계산할 이유 자체가 없다.

            var globalClusters = frontlineCoordinator.ClusterEnemies(globalRecognizedBuffer);

            foreach (var ally in surroundAlliesBuffer)
            {
                if (ally.CurrentTarget == null || IsAssigned(ally)) continue;

                var targetCluster = FindClusterContaining(globalClusters, ally.CurrentTarget);
                if (targetCluster == null) continue; // 극단적 경우(자기 타겟이 어느 군집에도 안 잡힘) - 대기.

                var ring = FindMatchingRing(targetCluster, ally.Range);
                if (ring == null)
                {
                    ring = CreateRing(targetCluster, ally.Range);
                    activeRings.Add(ring);
                }

                var approachPoint = ring.ComputeRadialPoint(ally.Position);
                if (!standardRadiusZone.Contains(approachPoint)) continue; // §13.3′ 활동 반경 밖 - 대기.

                ring.AssignedUnits.Add(ally);
            }
        }

        private bool IsAssigned(IBattleCombatant ally)
        {
            foreach (var ring in activeRings)
            {
                if (ring.AssignedUnits.Contains(ally)) return true;
            }
            return false;
        }

        private static IReadOnlyList<IDamageable> FindClusterContaining(IReadOnlyList<IReadOnlyList<IDamageable>> clusters, IDamageable target)
        {
            foreach (var cluster in clusters)
            {
                if (ContainsReference(cluster, target)) return cluster;
            }
            return null;
        }

        // 사거리가 같고, 링이 이미 인식 중인 적 중 하나라도 이번 후보의 (이번 틱 전역 클러스터링
        // 기준) 군집에 속하면 같은 군집으로 판정한다 - 링 자기 멤버만의 좁은 시야가 아니라 전역
        // 클러스터링 결과로 판정해야, 서로 다른 개체를 타겟팅하는 같은 군집 내 유닛들이 하나의
        // 링으로 합쳐진다(위 버그 수정의 핵심).
        private SurroundRing FindMatchingRing(IReadOnlyList<IDamageable> targetCluster, float dealerRange)
        {
            foreach (var ring in activeRings)
            {
                if (!Mathf.Approximately(ring.DealerRange, dealerRange)) continue;

                foreach (var enemy in ring.RecognizedUnion)
                {
                    if (ContainsReference(targetCluster, enemy)) return ring;
                }
            }
            return null;
        }

        // RecognizedUnion을 군집 스냅샷으로 미리 채워둔다 - 안 채우면 같은 틱 안에서 뒤이어 순회하는
        // 다른 후보가 FindMatchingRing에서 이 신생 링을 못 찾는다(다음 틱 ReconcileExistingRings가
        // 실제 배정 멤버 기준으로 다시 좁혀준다 - 여기 값은 임시 시드일 뿐).
        private static SurroundRing CreateRing(IReadOnlyList<IDamageable> cluster, float dealerRange)
        {
            var ring = new SurroundRing(dealerRange);
            ring.ClusterCenter = ComputeAveragePosition(cluster);
            ring.ClusterBoundingRadius = ComputeBoundingRadius(ring.ClusterCenter, cluster);
            ring.CurrentRadius = ring.ClusterBoundingRadius + dealerRange;
            foreach (var enemy in cluster) ring.RecognizedUnion.Add(enemy);
            return ring;
        }

        // 합류(콜리전 대체) 판정 - AssignedUnits 중 아직 Joined가 아닌 유닛의 "이번 틱 시작 시점"
        // 위치가 이미 링 위(허용오차 이내)에 있으면 합류 확정한다. 유닛이 실제로 움직이는 건 이
        // Update 이후(BattleSimulationLoop.Tick)라, 판정에 쓰는 위치는 항상 한 틱 전 결과다 -
        // Blocking/방진선과 같은 타이밍 관례.
        private void DetectJoins()
        {
            foreach (var ring in activeRings)
            {
                foreach (var member in ring.AssignedUnits)
                {
                    if (ring.Joined.Contains(member)) continue;

                    var distanceToCenter = (member.Position - ring.ClusterCenter).magnitude;
                    if (Mathf.Abs(distanceToCenter - ring.CurrentRadius) <= TacticsTuning.SurroundJoinToleranceMeters)
                    {
                        ring.Joined.Add(member);
                    }
                }
            }
        }

        private static void AddDistinct(List<IDamageable> list, IDamageable candidate)
        {
            if (candidate != null && !list.Contains(candidate)) list.Add(candidate);
        }

        // IReadOnlyList<IDamageable>는 Contains가 없어(List<T>와 달리) 수동 순회가 필요하다 -
        // FrontlineFormationCoordinator.ContainsReference와 같은 이유.
        private static bool ContainsReference(IReadOnlyList<IDamageable> list, IDamageable value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], value)) return true;
            }
            return false;
        }

        private static Vector2 ComputeAveragePosition(IReadOnlyList<IDamageable> units)
        {
            if (units.Count == 0) return Vector2.zero;
            var sum = Vector2.zero;
            foreach (var unit in units) sum += unit.Position;
            return sum / units.Count;
        }

        private static float ComputeBoundingRadius(Vector2 center, IReadOnlyList<IDamageable> units)
        {
            var maxSqrDistance = 0f;
            foreach (var unit in units)
            {
                var sqrDistance = (unit.Position - center).sqrMagnitude;
                if (sqrDistance > maxSqrDistance) maxSqrDistance = sqrDistance;
            }
            return Mathf.Sqrt(maxSqrDistance);
        }
    }
}

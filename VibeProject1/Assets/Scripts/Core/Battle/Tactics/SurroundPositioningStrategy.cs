using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Surround(포위) - RangedSurroundCoordinator가 관리하는 포위망(SurroundRing)을
    /// 향해 이동한다(Docs/설계/12번 §13.3′). 1차 설계(개별 각도 배정)는 "타겟이 다르면 그룹도 갈라져
    /// 군집을 못 둘러싼다"는 구조적 한계로 전면 폐기됐다(2026-08-29) - 이 전략은 명시적 각도를 조회
    /// 하지 않고, 코디네이터가 관리하는 링의 중심/반지름/합류 여부만 조회해 그때그때 이동 목적지를
    /// 계산한다(접근 방향이 곧 최종 각도가 되는 구조).
    /// </summary>
    public class SurroundPositioningStrategy : IPositioningStrategy
    {
        private readonly RangedSurroundCoordinator coordinator;
        private readonly IUnitSpatialQuery spatialQuery;
        // 재사용 버퍼(§12.8과 같은 이유) - 매 틱 새 List를 할당하지 않는다.
        private readonly List<IBattleCombatant> sameSideSurroundBuffer = new();
        private readonly List<float> gapAngleBuffer = new();

        public SurroundPositioningStrategy(RangedSurroundCoordinator coordinator, IUnitSpatialQuery spatialQuery)
        {
            this.coordinator = coordinator;
            this.spatialQuery = spatialQuery;
        }

        public Vector2 ComputeMoveTarget(
            IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition,
            IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            if (!coordinator.TryGetRing(self, out var ring))
            {
                // 대기(Docs/설계/12번 §12.11과 같은 처리) - 아직 어느 링에도 배정되지 않은 극단적 경우.
                return selfPosition;
            }

            if (ring.Joined.Contains(self))
            {
                var canAttack = target is { IsAlive: true } && (target.Position - selfPosition).sqrMagnitude <= range * range;
                if (canAttack) return selfPosition; // 이벤트성 고정(§12.6과 같은 철학) - 공격 가능하면 정지.

                // 공격 불가 - 링 반지름 축소(§13.3′-4)를 좇아 반지름 방향으로만 이동(접선 보정/반발 없음,
                // 자기 각도는 그대로 유지한 채 반지름만 좁혀 들어간다).
                return ring.ComputeRadialPoint(selfPosition);
            }

            // 접근 중(미합류) - 반지름 접근 + 아군 반발(2m) + 빈 구간 약한 끌어당김(§13.3′) 세 벡터의 합.
            var radialPoint = ring.ComputeRadialPoint(selfPosition);

            sameSideSurroundBuffer.Clear();
            foreach (var unit in sameSideUnits)
            {
                if (unit != self && unit.Positioning == LocalPositioning.Surround) sameSideSurroundBuffer.Add(unit);
            }
            var separationPush = spatialQuery.ComputeSeparationPush(self, selfPosition, TacticsTuning.SurroundAllySpacingMeters, sameSideSurroundBuffer);

            var gapPull = ComputeGapPull(ring, selfPosition);

            return radialPoint + separationPush + gapPull;
        }

        // 이미 합류(Joined)한 멤버들의 실제 현재 각도(캐시 안 함 - 매 틱 실측)로 가장 넓은 빈 구간을
        // 찾아, 그 방향으로 약한 접선 벡터를 반환한다(Docs/설계/12번 §13.3′). 1차 설계의 각도 배정
        // 회귀(§13.3)와 다른 점: 그때는 "가장 넓은 간격의 중간각"을 유닛당 한 번 계산해 영구 각도로
        // 고정했다(첫 멤버 직후엔 빈 구간이 원 전체라 사실상 무의미했고, 그 결과가 그대로 굳어버림).
        // 이번엔 (a) 하드 배정이 아니라 낮은 가중치(SurroundGapPullWeight)의 방향 힌트일 뿐이라 실제
        // 경로를 완전히 결정하지 않고, (b) 매 틱 다시 계산하므로 한 번의 잘못된 계산이 영구 고정될
        // 수 없다.
        private Vector2 ComputeGapPull(SurroundRing ring, Vector2 selfPosition)
        {
            if (ring.Joined.Count == 0) return Vector2.zero; // 아직 기준 삼을 합류 멤버가 없음.

            gapAngleBuffer.Clear();
            foreach (var member in ring.Joined)
            {
                var toMember = member.Position - ring.ClusterCenter;
                if (toMember.sqrMagnitude > 0.0001f) gapAngleBuffer.Add(Mathf.Atan2(toMember.y, toMember.x));
            }
            if (gapAngleBuffer.Count == 0) return Vector2.zero;
            gapAngleBuffer.Sort();

            var bestGapStart = gapAngleBuffer[0];
            var bestGapSize = 0f;
            for (var i = 0; i < gapAngleBuffer.Count; i++)
            {
                var current = gapAngleBuffer[i];
                // 마지막 각도→처음 각도 랩어라운드 간격(+2π)까지 포함해 원 전체를 훑는다.
                var next = i + 1 < gapAngleBuffer.Count ? gapAngleBuffer[i + 1] : gapAngleBuffer[0] + Mathf.PI * 2f;
                var gap = next - current;
                if (gap > bestGapSize) { bestGapSize = gap; bestGapStart = current; }
            }

            var gapCenterAngle = bestGapStart + bestGapSize * 0.5f;
            var gapDirection = new Vector2(Mathf.Cos(gapCenterAngle), Mathf.Sin(gapCenterAngle));

            // gapDirection 중 접선 성분만 취한다 - 반지름 성분은 이미 radialPoint가 처리하므로
            // 중복 반영하지 않기 위함.
            var toSelf = selfPosition - ring.ClusterCenter;
            var radialDir = toSelf.sqrMagnitude > 0.0001f ? toSelf.normalized : Vector2.right;
            var tangentDir = new Vector2(-radialDir.y, radialDir.x); // radialDir을 90도 회전.
            var sign = Vector2.Dot(gapDirection, tangentDir) >= 0f ? 1f : -1f;

            return tangentDir * sign * TacticsTuning.SurroundGapPullWeight;
        }
    }
}

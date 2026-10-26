using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// waypoints와 progress01(0=출발지, 1=도착지)로 경로 위 보간 위치를 계산한다(설계 25번 §4/§7,
    /// 26번 §2/§10). 구간(슬롯 사이)마다 서로 다른 가중치(소요시간 비율)를 가질 수 있다 - 직선
    /// 이동은 1, 대각선 이동은 √2(사용자 확정, `FormationPathFinder.SegmentCost` 참고), 도착 고스트로
    /// 목적지를 중도 수정한 직후의 "부분 구간"(기획 21번)은 그 기본 비용에 남은 비율만 곱한 값이다.
    /// 이 유틸리티 자신은 그 가중치가 어떻게 정해졌는지 모른다 - 호출자가 좌표계를 아는 채로
    /// (UI 앵커 좌표 vs 전장 월드 좌표) `FormationPathFinder.ComputeSegmentWeights`로 미리 계산해
    /// 배열로 넘긴다(좌표계 변환은 호출자 책임, 이 클래스는 순수 보간 수학만 담당).
    /// </summary>
    internal static class FormationPathInterpolation
    {
        // segmentWeights: waypoints.Count-1개 - null이면 전 구간 가중치 1(기존 균등 보간과 동일하게
        // 동작, 하위 호환).
        public static Vector2 Evaluate(IReadOnlyList<Vector2> waypoints, float progress01, IReadOnlyList<float> segmentWeights = null)
        {
            if (waypoints.Count == 0) return Vector2.zero;
            if (waypoints.Count == 1) return waypoints[0];

            var (segmentIndex, localT) = Locate(waypoints.Count, progress01, segmentWeights);
            return Vector2.Lerp(waypoints[segmentIndex], waypoints[segmentIndex + 1], localT);
        }

        // Evaluate와 같은 가중치 계산을 좌표 없이 "몇 번째 구간의 몇 %인지"만 알고 싶을 때 쓴다 -
        // HandleRedirectMove가 "지금 캐릭터가 정확히 어느 구간의 어느 지점에 있는지" 알아내는 데 쓴다
        // (설계 26번 §3). waypointCount는 좌표 목록이 아니라 PathSlotIndices.Count(슬롯 개수)를
        // 그대로 넘기면 된다 - 구간 수는 그 값-1이므로 좌표 변환이 필요 없다.
        public static (int segmentIndex, float localT) Locate(int waypointCount, float progress01, IReadOnlyList<float> segmentWeights)
        {
            var segmentCount = Mathf.Max(1, waypointCount - 1);
            var totalWeight = 0f;
            for (var i = 0; i < segmentCount; i++)
            {
                totalWeight += Weight(segmentWeights, i);
            }
            if (totalWeight <= 0f) return (segmentCount - 1, 1f);

            var target = Mathf.Clamp01(progress01) * totalWeight;
            var accumulated = 0f;
            for (var i = 0; i < segmentCount; i++)
            {
                var weight = Weight(segmentWeights, i);
                if (target <= accumulated + weight || i == segmentCount - 1)
                {
                    var localT = weight > 0f ? Mathf.Clamp01((target - accumulated) / weight) : 1f;
                    return (i, localT);
                }
                accumulated += weight;
            }

            return (segmentCount - 1, 1f); // 도달 불가 - 방어적 폴백
        }

        private static float Weight(IReadOnlyList<float> segmentWeights, int index)
            => segmentWeights != null && index < segmentWeights.Count ? segmentWeights[index] : 1f;

        // waypoints[partialSegmentIndex] 좌표를 "그 구간을 partialSegmentWeight만큼 남긴 지점"의
        // 정확한 연속 좌표로 치환한다(그리드 노드가 아닌 실제 위치) - 호출자가 슬롯 좌표로 채운
        // waypoints 리스트를 그대로 넘기면 이 지점 하나만 덮어쓴다. partialSegmentIndex<0이면
        // 아무 것도 하지 않는다(일반 이동).
        public static void ApplyPartialSegment(IList<Vector2> waypoints, int partialSegmentIndex, float partialSegmentWeight)
        {
            if (partialSegmentIndex < 0 || partialSegmentIndex + 1 >= waypoints.Count)
            {
                return;
            }

            var localT = 1f - partialSegmentWeight;
            waypoints[partialSegmentIndex] = Vector2.Lerp(waypoints[partialSegmentIndex], waypoints[partialSegmentIndex + 1], localT);
        }
    }
}

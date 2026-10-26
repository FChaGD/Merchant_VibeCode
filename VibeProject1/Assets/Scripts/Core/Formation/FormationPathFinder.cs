using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 정비창 그리드(열×행) 위에서 점유 슬롯을 피하는 최단(가중치 기준) 경로를 찾는다(Docs/설계/25번
    /// §4, 26번 §10 - 대각선 확장). 대각선 이동을 허용하면서 대각선 1칸이 직선보다 √2배 더 오래
    /// 걸리는(사용자 확정) 가중치 그래프가 돼, 단순 BFS(무가중치 최단경로)로는 정확한 최단경로를
    /// 보장할 수 없다 - 다익스트라로 교체했다. 그리드 규모가 작아(최대 8×2=16칸) 우선순위 큐 없이
    /// 매번 미방문 최소값을 선형 탐색해도 성능 문제가 없다.
    /// </summary>
    internal static class FormationPathFinder
    {
        // 대각선 1칸의 소요 비용 - 직선 1칸을 1로 뒀을 때 실제 거리 비례(√2배, 사용자 확정 2026-09-07).
        public const float DiagonalCost = 1.41421356f;

        // blocked: 점유된 슬롯 판정(FormationLayout에 실제 기록된 슬롯 + 다른 진행 중 활동의
        // TargetSlotIndex 합집합, 호출자가 계산해 넘긴다). origin/target 자신은 blocked여도 항상
        // 통과 가능한 것으로 본다 - 출발점은 자기 자신이 비우는 중인 슬롯, 도착점은 이 이동이
        // 예약한 슬롯이기 때문이다. 대각선 이동은 양옆 두 직선 이웃이 모두 점유돼 있으면 차단한다
        // (모서리 컷팅 방지, 사용자 확정) - 점유된 두 타일 사이를 비집고 지나가는 것처럼 보이는 것을 막는다.
        public static IReadOnlyList<int> FindPath(int origin, int target, int columnCount, int rowCount, IReadOnlyCollection<int> blocked)
        {
            if (origin == target) return new[] { origin };

            var blockedSet = new HashSet<int>(blocked);
            var dist = new Dictionary<int, float> { [origin] = 0f };
            var cameFrom = new Dictionary<int, int> { [origin] = origin };
            var visited = new HashSet<int>();

            while (true)
            {
                var current = -1;
                var currentDist = float.MaxValue;
                foreach (var kv in dist)
                {
                    if (visited.Contains(kv.Key)) continue;
                    if (kv.Value < currentDist)
                    {
                        currentDist = kv.Value;
                        current = kv.Key;
                    }
                }

                if (current < 0) break; // 더 갈 곳이 없음(사방이 막힘) - 아래 폴백으로.
                if (current == target) return ReconstructPath(cameFrom, origin, target);
                visited.Add(current);

                foreach (var (next, cost) in GetNeighbors(current, columnCount, rowCount, blockedSet, target))
                {
                    if (visited.Contains(next)) continue;
                    var newDist = currentDist + cost;
                    if (!dist.TryGetValue(next, out var existing) || newDist < existing)
                    {
                        dist[next] = newDist;
                        cameFrom[next] = current;
                    }
                }
            }

            // 경로가 없으면(사방이 막힘) 직선(2점) 폴백 - 시각적으로 다른 유닛과 겹쳐 보일 수 있으나
            // 배치 자체를 막지는 않는다(설계 25번 §4).
            return new[] { origin, target };
        }

        // 이미 확정된 경로(FindPath의 결과 등) 위 인접한 두 슬롯 사이의 비용을 그대로 재계산한다 -
        // 소요시간 계산(FieldFormationPanel)과 보간 가중치(FormationPathInterpolation 소비자) 둘 다
        // 이 값을 공유해야 서로 어긋나지 않는다.
        public static float SegmentCost(int fromSlot, int toSlot, int columnCount)
        {
            var colDiff = Mathf.Abs(fromSlot % columnCount - toSlot % columnCount);
            var rowDiff = Mathf.Abs(fromSlot / columnCount - toSlot / columnCount);
            return (colDiff == 1 && rowDiff == 1) ? DiagonalCost : 1f;
        }

        // path 전체의 총 비용(구간 비용의 합) - 소요시간 계산에 쓴다(FormationTiming.MoveSecondsPerSlot
        // 곱해서 사용).
        public static float TotalCost(IReadOnlyList<int> path, int columnCount)
        {
            var total = 0f;
            for (var i = 0; i < path.Count - 1; i++)
            {
                total += SegmentCost(path[i], path[i + 1], columnCount);
            }
            return total;
        }

        // path의 각 구간 비용을 배열로 뽑아낸다(FormationPathInterpolation.Locate/Evaluate가 쓰는
        // 형태, 설계 26번 §10) - partialSegmentIndex 구간만 partialSegmentWeight(0..1, 남은 비율)를
        // 곱해 부분 구간(리다이렉트 직후 연속 좌표, 기획 21번)을 반영한다. partialSegmentIndex<0이면
        // 전부 기본 비용 그대로.
        public static float[] ComputeSegmentWeights(IReadOnlyList<int> pathSlotIndices, int columnCount, int partialSegmentIndex, float partialSegmentWeight)
        {
            var count = Mathf.Max(0, pathSlotIndices.Count - 1);
            var weights = new float[count];
            for (var i = 0; i < count; i++)
            {
                var baseCost = SegmentCost(pathSlotIndices[i], pathSlotIndices[i + 1], columnCount);
                weights[i] = (i == partialSegmentIndex) ? baseCost * partialSegmentWeight : baseCost;
            }
            return weights;
        }

        private static IEnumerable<(int next, float cost)> GetNeighbors(int index, int columnCount, int rowCount, HashSet<int> blockedSet, int target)
        {
            var col = index % columnCount;
            var row = index / columnCount;

            bool InBounds(int c, int r) => c >= 0 && c < columnCount && r >= 0 && r < rowCount;
            bool IsBlocked(int slot) => slot != target && blockedSet.Contains(slot);

            var hasLeft = InBounds(col - 1, row);
            var hasRight = InBounds(col + 1, row);
            var hasUp = InBounds(col, row - 1);
            var hasDown = InBounds(col, row + 1);
            var left = hasLeft ? row * columnCount + (col - 1) : -1;
            var right = hasRight ? row * columnCount + (col + 1) : -1;
            var up = hasUp ? (row - 1) * columnCount + col : -1;
            var down = hasDown ? (row + 1) * columnCount + col : -1;

            if (hasLeft && !IsBlocked(left)) yield return (left, 1f);
            if (hasRight && !IsBlocked(right)) yield return (right, 1f);
            if (hasUp && !IsBlocked(up)) yield return (up, 1f);
            if (hasDown && !IsBlocked(down)) yield return (down, 1f);

            // 대각선 4방향 - 모서리 컷팅 차단: 양옆 두 직선 이웃이 모두 점유면 그 사이를 비집고
            // 지나갈 수 없다.
            if (InBounds(col - 1, row - 1))
            {
                var diag = (row - 1) * columnCount + (col - 1);
                if (!IsBlocked(diag) && !(IsBlocked(left) && IsBlocked(up))) yield return (diag, DiagonalCost);
            }
            if (InBounds(col + 1, row - 1))
            {
                var diag = (row - 1) * columnCount + (col + 1);
                if (!IsBlocked(diag) && !(IsBlocked(right) && IsBlocked(up))) yield return (diag, DiagonalCost);
            }
            if (InBounds(col - 1, row + 1))
            {
                var diag = (row + 1) * columnCount + (col - 1);
                if (!IsBlocked(diag) && !(IsBlocked(left) && IsBlocked(down))) yield return (diag, DiagonalCost);
            }
            if (InBounds(col + 1, row + 1))
            {
                var diag = (row + 1) * columnCount + (col + 1);
                if (!IsBlocked(diag) && !(IsBlocked(right) && IsBlocked(down))) yield return (diag, DiagonalCost);
            }
        }

        private static IReadOnlyList<int> ReconstructPath(Dictionary<int, int> cameFrom, int origin, int target)
        {
            var path = new List<int> { target };
            var current = target;
            while (current != origin)
            {
                current = cameFrom[current];
                path.Add(current);
            }
            path.Reverse();
            return path;
        }
    }
}

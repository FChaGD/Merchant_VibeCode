using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 선형 탐색(O(n)) 구현체 - 지금 규모(아군 최대 16, 적 3~5)에서는 진영당 매 틱 최대 약 280쌍
    /// 비교 수준이라 충분하다. 유닛 수가 크게 늘어나면 IUnitSpatialQuery의 다른 구현체(공간 분할)로
    /// 교체 대상.
    /// </summary>
    public class LinearScanUnitSpatialQuery : IUnitSpatialQuery
    {
        public IDamageable FindNearest(Vector2 position, IReadOnlyList<IDamageable> candidates)
        {
            IDamageable nearest = null;
            var nearestSqrDist = float.MaxValue;
            foreach (var candidate in candidates)
            {
                if (!candidate.IsAlive) continue;
                var sqrDist = (candidate.Position - position).sqrMagnitude;
                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        public Vector2 ComputeSeparationPush(IBattleCombatant self, Vector2 position, float radius, IReadOnlyList<IDamageable> candidates)
        {
            var radiusSqr = radius * radius;
            var pushOut = Vector2.zero;

            foreach (var other in candidates)
            {
                if (other == self || !other.IsAlive) continue;

                var offset = position - other.Position;
                var sqrDist = offset.sqrMagnitude;
                // 대부분의 쌍은 반경 밖이다 - 제곱거리(sqrt 없음)로 먼저 걸러내고, 실제로 밀어내야
                // 하는 소수의 쌍에서만 sqrt(magnitude)를 계산한다.
                if (sqrDist >= radiusSqr) continue;

                var dist = Mathf.Sqrt(sqrDist);
                var direction = dist > 0.0001f ? offset / dist : Random.insideUnitCircle.normalized;
                pushOut += direction * (radius - dist);
            }

            return pushOut;
        }
    }
}

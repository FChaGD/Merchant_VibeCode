using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// TargetPriority.DeepestPenetration(전열) - "아군 대형 깊이 침투한 적 우선"을 대형 중심(원점)
    /// 기준 근접도로 근사한다 - 원점에 가까울수록 대형 안쪽까지 들어온 것이다(Docs/설계/12번 §3.3).
    /// </summary>
    public class DeepestPenetrationTargetSelector : ITargetSelector
    {
        public IDamageable Select(Vector2 selfPosition, IReadOnlyList<IDamageable> recognizedCandidates)
        {
            IDamageable best = null;
            var bestSqrDistanceToCenter = float.MaxValue;

            foreach (var candidate in recognizedCandidates)
            {
                if (!candidate.IsAlive) continue;

                var sqrDistanceToCenter = candidate.Position.sqrMagnitude;
                if (sqrDistanceToCenter < bestSqrDistanceToCenter)
                {
                    bestSqrDistanceToCenter = sqrDistanceToCenter;
                    best = candidate;
                }
            }

            return best;
        }
    }
}

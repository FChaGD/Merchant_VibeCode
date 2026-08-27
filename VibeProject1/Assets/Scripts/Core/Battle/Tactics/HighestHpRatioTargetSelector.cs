using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// TargetPriority.HighestHpRatio(전열, 구 "미피격 적") - 현재HP/최대HP 비율이 가장 높은 적.
    /// 공격을 안 받은 적일수록 비율이 100%에 가까우므로 "아직 안 맞은 적 우선"을 근사한다 -
    /// 진영 공유 상태(누가 이미 타겟 중인지) 없이 후보 목록만으로 계산 가능하다(Docs/설계/12번 §3.3-1).
    /// </summary>
    public class HighestHpRatioTargetSelector : ITargetSelector
    {
        public IDamageable Select(Vector2 selfPosition, IReadOnlyList<IDamageable> recognizedCandidates)
        {
            IDamageable best = null;
            var bestRatio = float.NegativeInfinity;

            foreach (var candidate in recognizedCandidates)
            {
                if (!candidate.IsAlive) continue;

                var ratio = candidate.MaxHp > 0f ? candidate.CurrentHp / candidate.MaxHp : 0f;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    best = candidate;
                }
            }

            return best;
        }
    }
}

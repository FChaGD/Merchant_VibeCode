using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>TargetPriority.LowestHp(원거리딜러, 마무리) - 체력이 가장 낮은 적 우선.</summary>
    public class LowestHpTargetSelector : ITargetSelector
    {
        public IDamageable Select(Vector2 selfPosition, IReadOnlyList<IDamageable> recognizedCandidates)
        {
            IDamageable best = null;
            var bestHp = float.PositiveInfinity;

            foreach (var candidate in recognizedCandidates)
            {
                if (!candidate.IsAlive) continue;

                if (candidate.CurrentHp < bestHp)
                {
                    bestHp = candidate.CurrentHp;
                    best = candidate;
                }
            }

            return best;
        }
    }
}

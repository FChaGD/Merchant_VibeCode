using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>TargetPriority.HighestAttack(원거리딜러, 저격) - 공격력이 가장 높은 적 우선.</summary>
    public class HighestAttackTargetSelector : ITargetSelector
    {
        public IDamageable Select(Vector2 selfPosition, IReadOnlyList<IDamageable> recognizedCandidates)
        {
            IDamageable best = null;
            var bestAttack = float.NegativeInfinity;

            foreach (var candidate in recognizedCandidates)
            {
                if (!candidate.IsAlive) continue;

                if (candidate.Attack > bestAttack)
                {
                    bestAttack = candidate.Attack;
                    best = candidate;
                }
            }

            return best;
        }
    }
}

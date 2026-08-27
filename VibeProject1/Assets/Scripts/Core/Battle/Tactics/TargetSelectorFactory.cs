using System;

namespace Game.Core
{
    /// <summary>TargetPriority→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class TargetSelectorFactory
    {
        public static ITargetSelector Create(TargetPriority priority, IUnitSpatialQuery spatialQuery)
        {
            return priority switch
            {
                TargetPriority.Nearest => new NearestTargetSelector(spatialQuery),
                TargetPriority.DeepestPenetration => new DeepestPenetrationTargetSelector(),
                TargetPriority.HighestHpRatio => new HighestHpRatioTargetSelector(),
                TargetPriority.HighestAttack => new HighestAttackTargetSelector(),
                TargetPriority.LowestHp => new LowestHpTargetSelector(),
                _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
            };
        }
    }
}

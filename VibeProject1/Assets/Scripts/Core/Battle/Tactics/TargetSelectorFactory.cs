using System;

namespace Game.Core
{
    /// <summary>TargetPriority→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class TargetSelectorFactory
    {
        public static ITargetSelector Create(string priority, IUnitSpatialQuery spatialQuery)
        {
            return priority switch
            {
                "Nearest" => new NearestTargetSelector(spatialQuery),
                "DeepestPenetration" => new DeepestPenetrationTargetSelector(),
                "HighestHpRatio" => new HighestHpRatioTargetSelector(),
                "HighestAttack" => new HighestAttackTargetSelector(),
                "LowestHp" => new LowestHpTargetSelector(),
                _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null),
            };
        }
    }
}

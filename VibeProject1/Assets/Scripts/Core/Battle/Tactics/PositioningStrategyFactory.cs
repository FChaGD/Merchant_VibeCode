using System;

namespace Game.Core
{
    /// <summary>LocalPositioning→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class PositioningStrategyFactory
    {
        // frontlineCoordinator는 Blocking 전용, rangedSurroundCoordinator는 Surround 전용,
        // spatialQuery는 Disperse·Surround 공용(둘 다 아군 간격 유지에 ComputeSeparationPush를
        // 재사용) - 나머지는 필요 없는 인자를 무시한다(OCP).
        public static IPositioningStrategy Create(
            LocalPositioning positioning, FrontlineFormationCoordinator frontlineCoordinator,
            RangedSurroundCoordinator rangedSurroundCoordinator, IUnitSpatialQuery spatialQuery)
        {
            return positioning switch
            {
                LocalPositioning.ApproachAttack => new ChargePositioningStrategy(), // 원거리딜러 접근 공격 - 전열 Charge와 로직 공유(Docs/설계/22번 §3)
                LocalPositioning.Charge => new ChargePositioningStrategy(),
                LocalPositioning.Blocking => new BlockingPositioningStrategy(frontlineCoordinator),
                LocalPositioning.Stationary => new StationaryPositioningStrategy(),
                LocalPositioning.Surround => new SurroundPositioningStrategy(rangedSurroundCoordinator, spatialQuery),
                LocalPositioning.Disperse => new DispersePositioningStrategy(spatialQuery),
                _ => throw new ArgumentOutOfRangeException(nameof(positioning), positioning, null),
            };
        }
    }
}

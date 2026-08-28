using System;

namespace Game.Core
{
    /// <summary>LocalPositioning→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class PositioningStrategyFactory
    {
        // frontlineCoordinator는 Blocking 전용, rangedSurroundCoordinator는 Surround 전용,
        // spatialQuery는 Disperse 전용 - 나머지는 필요 없는 인자를 무시한다(OCP).
        public static IPositioningStrategy Create(
            LocalPositioning positioning, FrontlineFormationCoordinator frontlineCoordinator,
            RangedSurroundCoordinator rangedSurroundCoordinator, IUnitSpatialQuery spatialQuery)
        {
            return positioning switch
            {
                LocalPositioning.Charge => new ChargePositioningStrategy(),
                LocalPositioning.Blocking => new BlockingPositioningStrategy(frontlineCoordinator),
                LocalPositioning.Stationary => new StationaryPositioningStrategy(),
                LocalPositioning.Surround => new SurroundPositioningStrategy(rangedSurroundCoordinator),
                LocalPositioning.Disperse => new DispersePositioningStrategy(spatialQuery),
                _ => throw new ArgumentOutOfRangeException(nameof(positioning), positioning, null),
            };
        }
    }
}

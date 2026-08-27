using System;

namespace Game.Core
{
    /// <summary>LocalPositioning→구현체 매핑(OCP, Docs/설계/12번 §7).</summary>
    public static class PositioningStrategyFactory
    {
        public static IPositioningStrategy Create(LocalPositioning positioning)
        {
            return positioning switch
            {
                LocalPositioning.Charge => new ChargePositioningStrategy(),
                LocalPositioning.Blocking => new BlockingPositioningStrategy(),
                LocalPositioning.MaintainRange => new MaintainRangePositioningStrategy(),
                LocalPositioning.Stationary => new StationaryPositioningStrategy(),
                _ => throw new ArgumentOutOfRangeException(nameof(positioning), positioning, null),
            };
        }
    }
}

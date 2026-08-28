using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>LocalPositioning.Stationary(원거리딜러) - 제자리에서 사격, 이동하지 않는다.</summary>
    public class StationaryPositioningStrategy : IPositioningStrategy
    {
        public Vector2 ComputeMoveTarget(IBattleCombatant self, Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition, IReadOnlyList<IBattleCombatant> sameSideUnits)
        {
            return selfPosition;
        }
    }
}

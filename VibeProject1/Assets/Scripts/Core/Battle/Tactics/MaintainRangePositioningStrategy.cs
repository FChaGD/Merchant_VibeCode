using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.MaintainRange(원거리딜러) - 사거리의 일정 비율(TacticsTuning.
    /// KitingProximityRangeRatio) 이내로 적이 들어오면 뒤로, 사거리를 벗어나면 앞으로, 그 사이
    /// 구간이면 제자리(Docs/기획/12번 §3.2).
    /// </summary>
    public class MaintainRangePositioningStrategy : IPositioningStrategy
    {
        public Vector2 ComputeMoveTarget(Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition)
        {
            var toTarget = target.Position - selfPosition;
            var distance = toTarget.magnitude;
            var tooCloseDistance = range * TacticsTuning.KitingProximityRangeRatio;

            if (distance < tooCloseDistance)
            {
                var awayDirection = distance > 0.0001f ? -toTarget / distance : Vector2.zero;
                return selfPosition + awayDirection * range;
            }

            if (distance > range)
            {
                return target.Position;
            }

            return selfPosition;
        }
    }
}

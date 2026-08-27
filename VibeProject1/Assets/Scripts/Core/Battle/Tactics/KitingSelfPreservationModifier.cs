using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// SelfPreservation.Kiting(원거리딜러) - 적이 사거리의 근접 구간(TacticsTuning.
    /// KitingProximityRangeRatio) 이내로 접근하면 유지사격하며 반대 방향으로 물러난다
    /// (Docs/기획/12번 §3.3). HP와 무관하게 항상 반응하는 상태 없는 판정이라 NotifyDamaged는 no-op.
    /// </summary>
    public class KitingSelfPreservationModifier : ISelfPreservationModifier
    {
        public void NotifyDamaged(float amount, float currentHpRatio)
        {
        }

        public bool TryGetOverrideMovement(float deltaTime, Vector2 selfPosition, IDamageable target, float range, out Vector2 destination)
        {
            var toTarget = target.Position - selfPosition;
            var distance = toTarget.magnitude;
            var tooCloseDistance = range * TacticsTuning.KitingProximityRangeRatio;

            if (distance >= tooCloseDistance)
            {
                destination = default;
                return false;
            }

            var awayDirection = distance > 0.0001f ? -toTarget / distance : Vector2.zero;
            destination = selfPosition + awayDirection * range;
            return true;
        }
    }
}

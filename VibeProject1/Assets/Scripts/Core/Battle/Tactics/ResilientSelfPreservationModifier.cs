using UnityEngine;

namespace Game.Core
{
    /// <summary>SelfPreservation.Resilient(전열) - 후퇴 없이 버팀, 항상 no-op.</summary>
    public class ResilientSelfPreservationModifier : ISelfPreservationModifier
    {
        public void NotifyDamaged(float amount, float currentHpRatio)
        {
        }

        public bool TryGetOverrideMovement(float deltaTime, Vector2 selfPosition, IDamageable target, float range, out Vector2 destination)
        {
            destination = default;
            return false;
        }
    }
}

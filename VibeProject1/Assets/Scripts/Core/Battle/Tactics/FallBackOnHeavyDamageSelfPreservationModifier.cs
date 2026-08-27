using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// SelfPreservation.FallBackOnHeavyDamage(전열) - HP 비율이 임계치 이하로 떨어지면 전투당 1회만
    /// 일시 후퇴(Docs/기획/12번 §3.3) - 이후 같은 전투에서 임계치를 다시 충족해도 발동하지 않는다
    /// (hasTriggered로 1회 제한).
    /// </summary>
    public class FallBackOnHeavyDamageSelfPreservationModifier : ISelfPreservationModifier
    {
        private bool hasTriggered;
        private float retreatSecondsRemaining;

        public void NotifyDamaged(float amount, float currentHpRatio)
        {
            if (hasTriggered) return;
            if (currentHpRatio > TacticsTuning.FallBackOnHeavyDamageHpRatioThreshold) return;

            hasTriggered = true;
            retreatSecondsRemaining = TacticsTuning.FallBackOnHeavyDamageRetreatSeconds;
        }

        public bool TryGetOverrideMovement(float deltaTime, Vector2 selfPosition, IDamageable target, float range, out Vector2 destination)
        {
            if (retreatSecondsRemaining <= 0f)
            {
                destination = default;
                return false;
            }

            retreatSecondsRemaining -= deltaTime;

            var toTarget = target.Position - selfPosition;
            var awayFromTarget = toTarget.sqrMagnitude > 0.0001f ? -toTarget.normalized : Vector2.zero;
            destination = selfPosition + awayFromTarget * range;
            return true;
        }
    }
}

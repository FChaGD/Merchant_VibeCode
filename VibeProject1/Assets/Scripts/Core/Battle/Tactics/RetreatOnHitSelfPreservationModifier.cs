using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// SelfPreservation.RetreatOnHit(원거리딜러) - 피격 시 일정 거리(TacticsTuning.
    /// RetreatOnHitDistanceMeters) 후퇴한 뒤 공격을 재개한다(Docs/기획/12번 §3.3). 후퇴 시작 지점을
    /// 첫 TryGetOverrideMovement 호출 시점에 기록해 두고, 그로부터 이동한 거리로 종료를 판정한다.
    /// </summary>
    public class RetreatOnHitSelfPreservationModifier : ISelfPreservationModifier
    {
        private bool isRetreating;
        private bool hasRetreatOrigin;
        private Vector2 retreatOrigin;

        public void NotifyDamaged(float amount, float currentHpRatio)
        {
            // 이미 후퇴 중이면 origin을 다시 잡지 않는다 - 재피격마다 리셋하면 누적 후퇴 거리가 항상
            // 0에서 다시 시작해 임계값(TacticsTuning.RetreatOnHitDistanceMeters)에 영원히 도달하지 못한다.
            if (isRetreating) return;
            isRetreating = true;
            hasRetreatOrigin = false;
        }

        public bool TryGetOverrideMovement(float deltaTime, Vector2 selfPosition, IDamageable target, float range, out Vector2 destination)
        {
            if (!isRetreating)
            {
                destination = default;
                return false;
            }

            if (!hasRetreatOrigin)
            {
                retreatOrigin = selfPosition;
                hasRetreatOrigin = true;
            }

            if ((selfPosition - retreatOrigin).magnitude >= TacticsTuning.RetreatOnHitDistanceMeters)
            {
                isRetreating = false;
                destination = default;
                return false;
            }

            var toTarget = target.Position - selfPosition;
            var awayFromTarget = toTarget.sqrMagnitude > 0.0001f ? -toTarget.normalized : Vector2.zero;
            destination = selfPosition + awayFromTarget * range;
            return true;
        }
    }
}

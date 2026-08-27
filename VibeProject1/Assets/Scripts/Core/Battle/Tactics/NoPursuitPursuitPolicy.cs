using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PursuitPreset.NoPursuit - 반경 밖에 3초 이상 머물면(명중 여부 무관) 복귀·재타겟. 반경 안으로
    /// 돌아오면 누적 시간이 리셋된다(Docs/기획/12번 §2.3).
    /// </summary>
    public class NoPursuitPursuitPolicy : IPursuitPolicy
    {
        private float outsideSeconds;

        public bool ShouldDisengage(float deltaTime, bool isOutsideRadius, bool justLandedHit)
        {
            if (!isOutsideRadius)
            {
                outsideSeconds = 0f;
                return false;
            }

            outsideSeconds += deltaTime;
            return outsideSeconds >= TacticsTuning.NoPursuitTimeoutSeconds;
        }

        public Vector2 ClampDestination(Vector2 desiredDestination, IActivityRadiusZone zone) => desiredDestination;
    }
}

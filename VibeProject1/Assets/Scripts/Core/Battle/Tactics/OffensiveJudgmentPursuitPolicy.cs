using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PursuitPreset.OffensiveJudgment(기본값) - 반경 밖에서 5초 이상 공격을 못 맞히면 복귀·재타겟.
    /// 명중하거나 반경 안으로 돌아오면 누적 시간이 리셋된다(Docs/기획/12번 §2.3).
    /// </summary>
    public class OffensiveJudgmentPursuitPolicy : IPursuitPolicy
    {
        private float outsideAndMissingSeconds;

        public bool ShouldDisengage(float deltaTime, bool isOutsideRadius, bool justLandedHit)
        {
            if (!isOutsideRadius || justLandedHit)
            {
                outsideAndMissingSeconds = 0f;
                return false;
            }

            outsideAndMissingSeconds += deltaTime;
            return outsideAndMissingSeconds >= TacticsTuning.OffensiveJudgmentTimeoutSeconds;
        }

        public Vector2 ClampDestination(Vector2 desiredDestination, IActivityRadiusZone zone) => desiredDestination;
    }
}

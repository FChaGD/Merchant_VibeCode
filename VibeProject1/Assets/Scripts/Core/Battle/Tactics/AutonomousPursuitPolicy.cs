using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PursuitPreset.Autonomous - 활동 반경을 이유로는 절대 이탈하지 않는다. 인식(§2.1)이나 다른
    /// 축엔 영향이 없다 - "반경 무시"는 이 정책 자신의 트리거 무력화로 한정된다(Docs/설계/12번 §6-2).
    /// </summary>
    public class AutonomousPursuitPolicy : IPursuitPolicy
    {
        public bool ShouldDisengage(float deltaTime, bool isOutsideRadius, bool justLandedHit) => false;

        public Vector2 ClampDestination(Vector2 desiredDestination, IActivityRadiusZone zone) => desiredDestination;
    }
}

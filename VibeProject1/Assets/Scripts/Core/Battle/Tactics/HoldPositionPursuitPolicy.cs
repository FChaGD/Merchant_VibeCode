using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PursuitPreset.HoldPosition(가장 수비적) - 활동 반경을 아예 벗어나지 않는다. "반경 밖으로
    /// 나간 그 즉시" 복귀·재타겟이 트리거되고(ShouldDisengage), 이동 목적지도 매 틱 반경 경계
    /// 안으로 clamp된다(ClampDestination) - 둘이 함께여야 "애초에 안 나감"이 실제로 성립한다
    /// (Docs/기획/12번 §2.3).
    /// </summary>
    public class HoldPositionPursuitPolicy : IPursuitPolicy
    {
        public bool ShouldDisengage(float deltaTime, bool isOutsideRadius, bool justLandedHit) => isOutsideRadius;

        public Vector2 ClampDestination(Vector2 desiredDestination, IActivityRadiusZone zone) => zone.ClampToZone(desiredDestination);
    }
}

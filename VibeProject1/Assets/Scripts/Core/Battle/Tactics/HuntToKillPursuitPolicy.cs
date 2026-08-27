using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// PursuitPreset.HuntToKill - 타겟이 죽거나 도주할 때까지 무제한 추적. ShouldDisengage는 살아있는
    /// 타겟을 추적 중일 때만 호출되므로(IPursuitPolicy 문서 참고) 항상 false를 반환하는 것만으로
    /// "죽을 때까지 안 그만둠"이 자동으로 성립한다 - 별도 로직이 필요 없다.
    /// </summary>
    public class HuntToKillPursuitPolicy : IPursuitPolicy
    {
        public bool ShouldDisengage(float deltaTime, bool isOutsideRadius, bool justLandedHit) => false;

        public Vector2 ClampDestination(Vector2 desiredDestination, IActivityRadiusZone zone) => desiredDestination;
    }
}

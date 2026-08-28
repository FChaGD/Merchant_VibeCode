using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Blocking(전열) - 유닛 각자의 배치 슬롯(homePosition)과 타겟 사이, 사거리만큼
    /// 못 미친 지점을 목적지로 삼는다(Docs/기획/12번 §3.2). 유닛마다 homePosition이 달라 목적지도
    /// 각자 다른 지점이 되고, 그 결과 타겟을 둘러싼 저지선이 형성된다 - selfPosition을 기준으로 삼으면
    /// 매 프레임 목적지가 흔들리므로, 전투 내내 고정인 homePosition을 기준점으로 쓴다.
    /// </summary>
    public class BlockingPositioningStrategy : IPositioningStrategy
    {
        public Vector2 ComputeMoveTarget(Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition)
        {
            var toTarget = target.Position - homePosition;
            var direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
            return target.Position - direction * range;
        }
    }
}

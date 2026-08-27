using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// LocalPositioning.Blocking(전열) - 아군 대형 중심(원점)과 타겟 사이, 사거리만큼 못 미친 지점을
    /// 목적지로 삼는다 - 대형 중심에서 타겟 쪽으로 뻗은 직선 위에 정렬시켜 전열을 형성한다
    /// (Docs/기획/12번 §3.2). 대형 중심이 원점이라는 가정은 StandardActivityRadiusZone과 동일.
    /// </summary>
    public class BlockingPositioningStrategy : IPositioningStrategy
    {
        public Vector2 ComputeMoveTarget(Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition)
        {
            var toTarget = target.Position;
            var direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.zero;
            return target.Position - direction * range;
        }
    }
}

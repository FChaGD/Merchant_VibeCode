using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 활동 반경 안에서 전투 중 실제로 이동하는 목적지를 계산한다(Docs/기획/12번 §3.2). 반환값은
    /// "이 지점을 향해 이동하라"는 목적지 좌표다 - selfPosition을 그대로 반환하면 "이동 없음"을
    /// 뜻한다. ISelfPreservationModifier가 덮어쓰지 않을 때만 이 결과를 쓴다(Docs/설계/12번 §4).
    /// </summary>
    public interface IPositioningStrategy
    {
        Vector2 ComputeMoveTarget(Vector2 selfPosition, IDamageable target, float range, Vector2 homePosition);
    }
}

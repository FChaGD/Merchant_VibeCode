using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ActivityRadiusPreset.Standard - 대형 중심 기준 반경(대형 중심→모서리 사선거리 + 마진,
    /// Docs/기획/12번 §2.2). 대형 중심은 원점(0,0)이다 - ComputeAllyPosition이 이미 그렇게
    /// 배치하고, 사기 도주 방향 계산(BattleCharacterUnit.ComputeFleeDirection)도 같은 가정을 쓴다.
    /// </summary>
    public class StandardActivityRadiusZone : IActivityRadiusZone
    {
        private readonly float radius;

        public StandardActivityRadiusZone(float radius)
        {
            this.radius = radius;
        }

        public bool Contains(Vector2 worldPosition)
        {
            return worldPosition.sqrMagnitude <= radius * radius;
        }

        public Vector2 ClampToZone(Vector2 desiredPosition)
        {
            if (desiredPosition.sqrMagnitude <= radius * radius) return desiredPosition;
            return desiredPosition.normalized * radius;
        }
    }
}

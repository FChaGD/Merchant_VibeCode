using UnityEngine;

namespace Game.Core
{
    /// <summary>ActivityRadiusPreset.Fixed - 유닛 각자의 배치 슬롯 위치 기준 반경(Docs/기획/12번 §2.2).</summary>
    public class FixedActivityRadiusZone : IActivityRadiusZone
    {
        private readonly Vector2 homePosition;

        public FixedActivityRadiusZone(Vector2 homePosition)
        {
            this.homePosition = homePosition;
        }

        public bool Contains(Vector2 worldPosition)
        {
            return (worldPosition - homePosition).sqrMagnitude <= TacticsTuning.FixedRadiusMeters * TacticsTuning.FixedRadiusMeters;
        }

        public Vector2 ClampToZone(Vector2 desiredPosition)
        {
            var offset = desiredPosition - homePosition;
            var radius = TacticsTuning.FixedRadiusMeters;
            if (offset.sqrMagnitude <= radius * radius) return desiredPosition;
            return homePosition + offset.normalized * radius;
        }
    }
}

using UnityEngine;

namespace Game.Core
{
    /// <summary>ActivityRadiusPreset.FieldWide(전장 전체) - 전장 경계까지(Docs/기획/12번 §2.2, §2.2-1에서 실제 하드 캡으로 확정).</summary>
    public class WideActivityRadiusZone : IActivityRadiusZone
    {
        public bool Contains(Vector2 worldPosition) => true;

        public Vector2 ClampToZone(Vector2 desiredPosition) => desiredPosition;
    }
}

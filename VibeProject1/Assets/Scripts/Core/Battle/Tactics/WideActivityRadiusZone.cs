using UnityEngine;

namespace Game.Core
{
    /// <summary>ActivityRadiusPreset.Wide - 전장 전체(사실상 무제한, Docs/기획/12번 §2.2).</summary>
    public class WideActivityRadiusZone : IActivityRadiusZone
    {
        public bool Contains(Vector2 worldPosition) => true;

        public Vector2 ClampToZone(Vector2 desiredPosition) => desiredPosition;
    }
}

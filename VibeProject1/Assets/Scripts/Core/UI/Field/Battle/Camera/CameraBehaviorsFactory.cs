using System;

namespace Game.Core
{
    /// <summary>CameraPreset→정책 조합 매핑. 줌 배율 상수를 이 한 곳에 모은다.</summary>
    internal static class CameraBehaviorsFactory
    {
        private const float FieldMaxZoomRatio = 2.5f; // 기획 09번 §3.2 확정값
        private const float BattleTestZoomInRatio = 2f;
        private const float BattleTestZoomOutRatio = 3f;

        public static CameraBehaviors Build(CameraPreset preset)
        {
            return preset switch
            {
                CameraPreset.Field => new CameraBehaviors(
                    new RatioZoomRangePolicy(FieldMaxZoomRatio, 1f), new FieldClampedPanBoundsPolicy()),
                CameraPreset.BattleTest => new CameraBehaviors(
                    new RatioZoomRangePolicy(BattleTestZoomInRatio, BattleTestZoomOutRatio), new UnboundedPanBoundsPolicy()),
                _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
            };
        }
    }
}

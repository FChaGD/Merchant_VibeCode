using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 기준 size에 배율을 곱/나눠 줌 한계를 정한다. orthographicSize는 작을수록 확대라, UGUI 버전의
    /// "최소 줌×배율"과 달리 확대 한계는 나누기, 축소 한계는 곱하기다. Field/배틀 테스트는 계산식이
    /// 같고 배율만 달라 구현체를 나누지 않고 인자로 구분한다.
    /// </summary>
    internal class RatioZoomRangePolicy : ICameraZoomRangePolicy
    {
        private readonly float zoomInRatio;
        private readonly float zoomOutRatio;

        public RatioZoomRangePolicy(float zoomInRatio, float zoomOutRatio)
        {
            this.zoomInRatio = zoomInRatio;
            this.zoomOutRatio = Mathf.Max(1f, zoomOutRatio);
        }

        public ZoomSizeRange Compute(in CameraViewState state)
            => new ZoomSizeRange(state.BaselineSize / zoomInRatio, state.BaselineSize * zoomOutRatio);
    }
}

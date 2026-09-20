using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 카메라 시야가 전장 정사각형(중심 원점, 한 변 fieldRadius*2) 밖으로 나가지 않게 위치를 클램프한다.
    /// UGUI 버전과 달리 콘텐츠가 아니라 카메라 시야 자체를 경계 안에 가두는 반대 방향 계산이다.
    /// </summary>
    internal class FieldClampedPanBoundsPolicy : ICameraPanBoundsPolicy
    {
        public Vector2 ClampPosition(Vector2 position, in CameraViewState state)
        {
            var halfHeight = state.CurrentSize;
            var halfWidth = state.CurrentSize * state.Aspect;
            var maxOffsetX = Mathf.Max(state.FieldRadius - halfWidth, 0f);
            var maxOffsetY = Mathf.Max(state.FieldRadius - halfHeight, 0f);

            return new Vector2(
                Mathf.Clamp(position.x, -maxOffsetX, maxOffsetX),
                Mathf.Clamp(position.y, -maxOffsetY, maxOffsetY));
        }
    }
}

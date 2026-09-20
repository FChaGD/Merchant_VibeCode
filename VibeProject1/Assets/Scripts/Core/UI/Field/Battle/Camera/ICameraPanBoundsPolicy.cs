using UnityEngine;

namespace Game.Core
{
    /// <summary>카메라 위치 보정 축. 줌 범위 축(ICameraZoomRangePolicy)과 서로 독립이다.</summary>
    internal interface ICameraPanBoundsPolicy
    {
        Vector2 ClampPosition(Vector2 position, in CameraViewState state);
    }
}

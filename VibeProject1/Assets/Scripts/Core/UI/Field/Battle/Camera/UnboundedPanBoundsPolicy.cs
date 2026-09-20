using UnityEngine;

namespace Game.Core
{
    /// <summary>위치를 보정하지 않는다 - 전장 밖 자유 이동이 필요한 화면(배틀 테스트)용.</summary>
    internal class UnboundedPanBoundsPolicy : ICameraPanBoundsPolicy
    {
        public Vector2 ClampPosition(Vector2 position, in CameraViewState state) => position;
    }
}

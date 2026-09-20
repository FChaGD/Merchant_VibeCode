namespace Game.Core
{
    /// <summary>줌 한계(확대/축소 size) 계산 축. 이동 범위 축(ICameraPanBoundsPolicy)과 서로 독립이다.</summary>
    internal interface ICameraZoomRangePolicy
    {
        ZoomSizeRange Compute(in CameraViewState state);
    }
}

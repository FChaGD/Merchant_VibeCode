namespace Game.Core
{
    /// <summary>축별 카메라 정책 조합(UnitTacticsBehaviors와 같은 역할).</summary>
    internal class CameraBehaviors
    {
        public ICameraZoomRangePolicy ZoomRange { get; }
        public ICameraPanBoundsPolicy PanBounds { get; }

        public CameraBehaviors(ICameraZoomRangePolicy zoomRange, ICameraPanBoundsPolicy panBounds)
        {
            ZoomRange = zoomRange;
            PanBounds = panBounds;
        }
    }
}

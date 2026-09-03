namespace Game.Core
{
    /// <summary>
    /// 상행 준비 UI에서 UIElementMarker.Id로 사용하는 문자열 상수.
    /// </summary>
    public static class TripUIElementIds
    {
        public const string PanelRoot = "Trip.PanelRoot";
        public const string MapRoot = "Trip.MapRoot";
        public const string OriginInfoRoot = "Trip.OriginInfoRoot";
        public const string DestinationInfoRoot = "Trip.DestinationInfoRoot";
        public const string SummaryRoot = "Trip.SummaryRoot";
        public const string CloseButton = "Trip.CloseButton";
        public const string OpenFormationButton = "Trip.OpenFormationButton";
        public const string StartButton = "Trip.StartButton";

        // 지도 위 디버그 도시 배치/경로 연결 기능(03/04번 기획 문서) - 정식 콘텐츠가 아니다.
        public const string DebugCityPaletteRoot = "Trip.DebugCityPaletteRoot";
        public const string DebugRoadToggleButton = "Trip.DebugRoadToggleButton";
        public const string DebugCityBulkDeleteButton = "Trip.DebugCityBulkDeleteButton";
        public const string DebugRoadBulkDeleteButton = "Trip.DebugRoadBulkDeleteButton";
        public const string DebugMapSaveButton = "Trip.DebugMapSaveButton";
    }
}

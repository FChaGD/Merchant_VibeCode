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
        // 출발 조건·경고(Docs/설계/79번 §9) - 끊어진 대열 안내 라벨(TMP_Text)과 배치 유닛 0 출발 확인 대화상자(ConfirmDialogView).
        public const string DisconnectedNotice = "Trip.DisconnectedNotice";
        public const string DepartureConfirmDialog = "Trip.DepartureConfirmDialog";

        // 지도 위 디버그 도시 배치/경로 연결 기능(03/04번 기획 문서) - 정식 콘텐츠가 아니다.
        public const string DebugCityPaletteRoot = "Trip.DebugCityPaletteRoot";
        public const string DebugRoadToggleButton = "Trip.DebugRoadToggleButton";
        public const string DebugCityBulkDeleteButton = "Trip.DebugCityBulkDeleteButton";
        public const string DebugRoadBulkDeleteButton = "Trip.DebugRoadBulkDeleteButton";
        public const string DebugMapSaveButton = "Trip.DebugMapSaveButton";
        // 지역 시스템(Docs/설계/69번 §5·§6)
        public const string RegionDropdown = "Trip.RegionDropdown";
        public const string DebugGatePaletteRoot = "Trip.DebugGatePaletteRoot";
        public const string DebugGateTargetDropdown = "Trip.DebugGateTargetDropdown";
        public const string DebugAddRegionButton = "Trip.DebugAddRegionButton";
        public const string DebugRemoveRegionButton = "Trip.DebugRemoveRegionButton";
        public const string DebugConfirmPanel = "Trip.DebugConfirmPanel";
        public const string DebugConfirmMessage = "Trip.DebugConfirmMessage";
        public const string DebugConfirmOkButton = "Trip.DebugConfirmOkButton";
        public const string DebugConfirmCancelButton = "Trip.DebugConfirmCancelButton";
    }
}

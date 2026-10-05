namespace Game.Core
{
    /// <summary>
    /// Hub 씬에서 배치(Formation) UI 요소를 UIElementMarker.Id로 조회하기 위한 문자열 상수.
    /// </summary>
    public static class FormationUIElementIds
    {
        public const string PanelRoot = "Formation.PanelRoot";
        public const string PaletteRoot = "Formation.PaletteRoot";
        public const string GridRoot = "Formation.GridRoot";
        public const string InfoPanelRoot = "Formation.InfoPanelRoot";
        public const string ApplyButton = "Formation.ApplyButton";
        public const string CloseButton = "Formation.CloseButton";
        public const string DebugPanelRoot = "Formation.DebugPanelRoot";
        // 상행 중 정비창 정리 모드(설계 79번 §8) 전용 - 마을 정비창에도 같은 빌더로 만들어지지만 쓰지 않아 비활성으로 남는다.
        public const string RepairGuideLabel = "Formation.RepairGuideLabel";
        public const string RepairDoneButton = "Formation.RepairDoneButton";
    }
}

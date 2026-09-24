namespace Game.Core
{
    /// <summary>
    /// Hub 씬에서 UIElementMarker.Id로 사용하는 문자열 상수.
    /// </summary>
    public static class HubUIElementIds
    {
        public const string DepartureButton = "Hub.DepartureButton";
        public const string FormationButton = "Hub.FormationButton";
        public const string TacticsButton = "Hub.TacticsButton";
        public const string Background = "Hub.Background";
        public const string ContentRoot = "Hub.ContentRoot";

        // ContentRoot 하위 레이어(Docs/설계/37번 §3). DepthLayer = 화면 depth에 따라 바뀌는 UI,
        // RootDepth = 루트 depth 그룹(CanvasGroup), PersistentLayer = depth와 무관하게 항상 활성인 UI.
        public const string DepthLayer = "Hub.DepthLayer";
        public const string RootDepth = "Hub.RootDepth";
        public const string PersistentLayer = "Hub.PersistentLayer";

        public const string TownCategoryColumn = "Hub.TownCategoryColumn";
        public static string TownCategoryButton(string categoryId) => $"Hub.TownCategory.{categoryId}";

        public const string CurrencyPanelRoot = "Hub.CurrencyPanelRoot";
        public const string CurrencyIcon = "Hub.CurrencyIcon";
        public const string CurrencyAmountText = "Hub.CurrencyAmountText";
        public const string CurrencyCapacityTooltip = "Hub.CurrencyCapacityTooltip"; // 프레임(표시/숨김 토글 대상)
        public const string CurrencyCapacityTooltipText = "Hub.CurrencyCapacityTooltipText"; // 텍스트 내용
    }
}

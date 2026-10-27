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

        // ContentRoot 하위 레이어(Docs/설계/37번 §3, 38번 §5). 두 관리 축:
        //   depth 축  - DepthLayer(depth 의존) / PersistentLayer·PopupExemptLayer(depth 무관)
        //   팝업 축   - DepthLayer·PersistentLayer(모달 팝업 시 숨김) / PopupExemptLayer(유지)
        // RootDepth = 루트 depth 그룹, PopupLayer = 팝업 자체(ModalPopups → ModelessPopups 순).
        public const string DepthLayer = "Hub.DepthLayer";
        public const string RootDepth = "Hub.RootDepth";
        public const string PersistentLayer = "Hub.PersistentLayer";
        public const string PopupLayer = "Hub.PopupLayer";
        // 시설 화면 묶음(Docs/Refactor/2026-10-26_공통.md B-1) - 모달 묶음보다 아래에 그려져 골드 변환 같은 모달이 시설 화면 위에 뜬다.
        public const string Screens = "Hub.Screens";
        public const string ModalPopups = "Hub.ModalPopups";
        public const string ModelessPopups = "Hub.ModelessPopups";
        public const string PopupExemptLayer = "Hub.PopupExemptLayer";

        public const string TownCategoryColumn = "Hub.TownCategoryColumn";
        public static string TownCategoryButton(string categoryId) => $"Hub.TownCategory.{categoryId}";

        // 인벤토리 상시 호출 버튼(PersistentLayer) - 팝업 Id(InventoryPopupIds)로 조립한다.
        public const string InventoryShortcutRoot = "Hub.InventoryShortcuts";
        public static string InventoryShortcutButton(string popupId) => $"Hub.InventoryShortcut.{popupId}";
        public static string InventoryShortcutKeyLabel(string popupId) => $"Hub.InventoryShortcutKey.{popupId}";

        public const string CurrencyPanelRoot = "Hub.CurrencyPanelRoot";
        public const string CurrencyIcon = "Hub.CurrencyIcon";
        public const string CurrencyAmountText = "Hub.CurrencyAmountText";
        public const string CurrencyCapacityTooltip = "Hub.CurrencyCapacityTooltip"; // 프레임(표시/숨김 토글 대상)
        public const string CurrencyCapacityTooltipText = "Hub.CurrencyCapacityTooltipText"; // 텍스트 내용
        // 재화 HUD 보유 현황(설계 101번 §5.1) - 입력은 골드 구역만 받는다.
        public const string CurrencyGoldSection = "Hub.CurrencyGoldSection";
        public const string CurrencyWagonIcon = "Hub.CurrencyWagonIcon";
        public const string CurrencyWagonCount = "Hub.CurrencyWagonCount";
        public const string CurrencyUnitIcon = "Hub.CurrencyUnitIcon";
        public const string CurrencyUnitCount = "Hub.CurrencyUnitCount";
        // 평판 칸(Docs/설계/107번 §10).
        public const string CurrencyReputation = "Hub.CurrencyReputation";

        // 게임 날짜 HUD(Docs/설계/85번 §4.1) - 상단 중앙, PopupExemptLayer.
        public const string DatePanelRoot = "Hub.DatePanelRoot";
        public const string DateText = "Hub.DateText";
    }
}

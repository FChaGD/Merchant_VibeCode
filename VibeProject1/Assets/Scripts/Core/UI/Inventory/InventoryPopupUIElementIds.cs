namespace Game.Core
{
    /// <summary>
    /// 인벤토리 편집 화면 요소 ID. 같은 구조를 여러 곳이 쓰므로 접두사로 구분한다 - 접두사는 팝업 Id(InventoryPopupIds)
    /// 또는 고정 패널 Id(무역품 구매 화면의 상단 물류품 패널 등, Docs/설계/50번 §6.4)다. 인스톨러(InventoryPopupUIBuilder)와
    /// 런타임 바인더(InventoryArrangementElements/InventoryPopupElements)가 공유한다.
    /// </summary>
    public static class InventoryPopupUIElementIds
    {
        public static string Root(string popupId) => $"{popupId}.Root";
        public static string TitleBar(string popupId) => $"{popupId}.TitleBar";
        public static string CloseButton(string popupId) => $"{popupId}.CloseButton";
        public static string GridArea(string popupId) => $"{popupId}.GridArea";
        public static string GridCells(string popupId) => $"{popupId}.GridCells";
        public static string GridItems(string popupId) => $"{popupId}.GridItems";
        public static string StagingArea(string popupId) => $"{popupId}.StagingArea";
        public static string StagingContent(string popupId) => $"{popupId}.StagingContent";
        public static string SortButton(string popupId) => $"{popupId}.SortButton";
        public static string InfoLabel(string popupId) => $"{popupId}.InfoLabel";
        public static string DragLayer(string popupId) => $"{popupId}.DragLayer";
        public static string ItemTemplate(string popupId) => $"{popupId}.ItemTemplate";
        public static string CellTemplate(string popupId) => $"{popupId}.CellTemplate";
    }
}

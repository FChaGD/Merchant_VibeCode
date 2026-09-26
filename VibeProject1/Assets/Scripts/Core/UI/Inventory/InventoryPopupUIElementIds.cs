namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 화면 요소 ID. 팝업 4종이 같은 구조를 쓰므로 popupId(InventoryPopupIds)를 접두사로 붙여
    /// 구분한다 - 인스톨러(InventoryPopupUIBuilder)와 런타임 바인더(InventoryPopupElements)가 공유한다.
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

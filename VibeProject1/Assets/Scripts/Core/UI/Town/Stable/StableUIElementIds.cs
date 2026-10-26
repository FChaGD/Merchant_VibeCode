namespace Game.Core
{
    /// <summary>
    /// 마구간 보유 영역 요소 Id(Docs/설계/91번 §5.2). 구매 후보 쪽은 목록형 구매 화면 공용 Id(RosterShopUIElementIds)를 그대로 쓰고,
    /// 마구간에만 있는 보유 목록·개별 정보·수리 요소만 여기 둔다. 인스톨러(StableOwnedUIBuilder)와 바인더(StableOwnedElements)가 공유한다.
    /// </summary>
    public static class StableUIElementIds
    {
        public const string ListRoot = "Stable.Owned.ListRoot";
        public const string ListViewport = "Stable.Owned.ListViewport";
        public const string ListContent = "Stable.Owned.ListContent";
        public const string RowTemplate = "Stable.Owned.RowTemplate";
        public const string RepairAllButton = "Stable.Owned.RepairAllButton";
        public const string RepairAllLabel = "Stable.Owned.RepairAllLabel";
        public const string RepairAllReason = "Stable.Owned.RepairAllReason";
        public const string DetailRoot = "Stable.Owned.DetailRoot";
        public const string DetailName = "Stable.Owned.DetailName";
        public const string DetailKind = "Stable.Owned.DetailKind";
        public const string DetailDurability = "Stable.Owned.DetailDurability";
        public const string DetailCost = "Stable.Owned.DetailCost";
        public const string DetailReason = "Stable.Owned.DetailReason";
        public const string RepairButton = "Stable.Owned.RepairButton";
        public const string BackButton = "Stable.Owned.BackButton";
        // 매각(설계 103번 §5.1, 2026-10-26 좌측 개체 정보로 이동) - 수리 오른쪽의 매각가·사유·매각 버튼, 마구간 전체를 덮는 확인 대화상자.
        public const string SalePrice = "Stable.Sale.Price";
        public const string SaleReason = "Stable.Sale.Reason";
        public const string SellButton = "Stable.Sale.SellButton";
        public const string SaleConfirmDialog = "Stable.Sale.ConfirmDialog";
    }
}

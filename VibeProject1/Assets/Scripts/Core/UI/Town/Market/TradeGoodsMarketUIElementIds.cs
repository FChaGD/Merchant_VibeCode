namespace Game.Core
{
    /// <summary>
    /// 무역품 구매 화면(Docs/설계/50번 §6.5)에서 UIElementMarker.Id로 쓰는 문자열. 인스톨러(TradeGoodsMarketUIBuilder)와
    /// 런타임 바인더(TradeGoodsMarketElements)가 공유한다. 좌측 상단 물류품 고정 패널의 요소는 InventoryPopupUIElementIds에
    /// InventoryPrefix를 붙여 찾는다(인벤토리 팝업과 같은 편집 본문, 설계 50번 §6.4).
    /// </summary>
    public static class TradeGoodsMarketUIElementIds
    {
        public const string Root = "TradeGoodsMarket.Root";
        public const string ExitButton = "TradeGoodsMarket.ExitButton";
        public const string StockViewport = "TradeGoodsMarket.StockViewport";
        public const string StockContent = "TradeGoodsMarket.StockContent";
        public const string StockRowTemplate = "TradeGoodsMarket.StockRowTemplate";
        public const string EmptyStockLabel = "TradeGoodsMarket.EmptyStockLabel";
        public const string InfoName = "TradeGoodsMarket.InfoName";
        public const string InfoPreview = "TradeGoodsMarket.InfoPreview";
        public const string InfoPreviewCellTemplate = "TradeGoodsMarket.InfoPreviewCellTemplate";
        public const string InfoPrice = "TradeGoodsMarket.InfoPrice";
        public const string InfoDescription = "TradeGoodsMarket.InfoDescription";
        public const string ReasonLabel = "TradeGoodsMarket.ReasonLabel";
        public const string BuyButton = "TradeGoodsMarket.BuyButton";

        public const string InventoryPrefix = "TradeGoodsMarket.Inventory";
    }
}

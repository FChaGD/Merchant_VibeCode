namespace Game.Core
{
    /// <summary>
    /// 판매 목록 한 줄(Docs/설계/50번 §5.3). 가격을 아이템 정의에 두지 않고 여기 두는 이유: 판매가는 판매 맥락(나중에는
    /// 마을별 판매가, 기획 49번 §3.2)에 따라 달라지는 값이라 아이템 고유 정보와 분리한다(ISP).
    /// 남은 수량은 마을 재고(Docs/설계/81번 §3.5) - 0이면 품절이고 행은 그대로 남는다(기획 80번 §3-5).
    /// </summary>
    public readonly struct ShopStockEntry
    {
        public IInventoryItemDefinition Definition { get; }
        public int Price { get; }
        public int Remaining { get; }

        public ShopStockEntry(IInventoryItemDefinition definition, int price, int remaining)
        {
            Definition = definition;
            Price = price;
            Remaining = remaining;
        }
    }
}

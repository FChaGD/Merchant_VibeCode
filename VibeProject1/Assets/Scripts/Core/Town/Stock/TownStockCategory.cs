namespace Game.Core
{
    public enum TownStockCategory
    {
        Wagon,
        Facility,
        TradeGoods,
    }

    /// <summary>
    /// Category → 판매 시설 매핑의 단일 소스(Docs/설계/81번 §3.3). 재고 테이블에 시설 열을 두지 않는 이유는 한 Category를 파는 시설이
    /// 하나로 정해져 있기 때문이다 - 특산물 구매처럼 같은 아이템 테이블을 다른 시설이 팔게 되면 Category를 추가한다.
    /// </summary>
    public static class TownStockCategories
    {
        public static string FacilityOf(TownStockCategory category) => category switch
        {
            TownStockCategory.Wagon => TownFacilityIds.Stable,
            TownStockCategory.Facility => TownFacilityIds.Stable,
            TownStockCategory.TradeGoods => TownFacilityIds.TradeGoodsMarket,
            _ => null,
        };

        public static TownStockCategory Of(FormationUnitKind kind) => kind == FormationUnitKind.Facility ? TownStockCategory.Facility : TownStockCategory.Wagon;
    }
}

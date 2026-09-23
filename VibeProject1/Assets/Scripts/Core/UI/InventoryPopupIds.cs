namespace Game.Core
{
    /// <summary>
    /// IInventoryPopupCoordinator.Toggle에 전달하는 팝업 식별자 상수(Docs/기획/31번 §3.5 시설 연계
    /// 진입점 및 상시 호출 버튼이 공통으로 참조한다).
    /// </summary>
    public static class InventoryPopupIds
    {
        public const string TradeGoods = "InventoryTradeGoods";
        public const string Equipment = "InventoryEquipment";
        public const string Consumable = "InventoryConsumable";
        public const string PersonalItem = "InventoryPersonalItem";
    }
}

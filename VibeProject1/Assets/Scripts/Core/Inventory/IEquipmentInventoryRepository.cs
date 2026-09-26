namespace Game.Core
{
    /// <summary>
    /// 장비 인벤토리 카테고리 마커(ITradeGoodsInventoryRepository와 같은 이유로 타입을 나눈다). IInventoryArrangement는
    /// 전투 장비 팝업의 정리 조작 계약(Docs/설계/42번 §5.1).
    /// </summary>
    public interface IEquipmentInventoryRepository : IInventoryRepository, IItemCatalogReader, IInventoryArrangement
    {
    }
}

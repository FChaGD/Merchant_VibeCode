namespace Game.Core
{
    /// <summary>
    /// 카테고리 구분용 마커 - Register&lt;T&gt;/TryResolve&lt;T&gt;가 타입당 단일 인스턴스라, 카테고리 4개
    /// 저장소를 구분해 조회하려면 타입 자체를 나눠야 한다(Docs/설계/32번 §3). IItemCatalogReader는
    /// 35번 §6에서 추가된 카탈로그 열람 계약 - 배치 상태(IInventoryRepository)와 책임이 달라
    /// 별도 인터페이스지만, 소비자 편의상 이 마커에서 함께 노출한다. IInventoryArrangement는 40번 §3.3에서
    /// 추가된 팝업 정리 조작 계약 - 나머지 카테고리 3종은 해당 팝업을 제작할 때 같은 방식으로 추가한다.
    /// </summary>
    public interface ITradeGoodsInventoryRepository : IInventoryRepository, IItemCatalogReader, IInventoryArrangement
    {
    }
}

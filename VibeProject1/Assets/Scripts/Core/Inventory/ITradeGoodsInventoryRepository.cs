namespace Game.Core
{
    /// <summary>
    /// 카테고리 구분용 마커 - Register&lt;T&gt;/TryResolve&lt;T&gt;가 타입당 단일 인스턴스라, 카테고리 4개
    /// 저장소를 구분해 조회하려면 타입 자체를 나눠야 한다(Docs/설계/32번 §3).
    /// </summary>
    public interface ITradeGoodsInventoryRepository : IInventoryRepository
    {
    }
}

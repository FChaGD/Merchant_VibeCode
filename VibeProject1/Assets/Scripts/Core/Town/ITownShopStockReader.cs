using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마을 시설의 판매 목록 조회(Docs/설계/50번 §5.3). 마을 Id를 받는 이유: 마을별 판매 품목·판매가 차별화
    /// (기획 49번 §3.2)가 구현되면 소비자 코드는 그대로 두고 구현체만 교체하기 위함이다.
    /// </summary>
    public interface ITownShopStockReader
    {
        IReadOnlyList<ShopStockEntry> GetStock(int cityId, string facilityId);
    }
}

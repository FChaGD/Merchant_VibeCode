namespace Game.Core
{
    /// <summary>
    /// 상단 물류품 조회가 필요한 IBattleResultRule 구현체만 선택적으로 구현하는 마커 인터페이스(설계 79번 §3.2).
    /// IRequiresCaravanRoster와 같은 이유·패턴이다(OCP). 화물 원장은 읽기만 하므로 저장소 전체가 아니라 IInventoryReader로
    /// 받는다(ISP) - 반영은 전투 뒤 정산이 한다.
    /// </summary>
    public interface IRequiresTradeGoodsReader
    {
        void SetTradeGoodsReader(IInventoryReader reader);
    }
}

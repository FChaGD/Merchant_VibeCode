namespace Game.Core
{
    /// <summary>
    /// 마차·시설 내구도가 필요한 IBattleResultRule 구현체만 구현하는 마커(Docs/설계/91번 §4.1). IRequiresTradeGoodsReader와 같은 패턴(OCP) -
    /// 전투는 시작값만 읽고, 저장은 전투 뒤 정산이 한다.
    /// </summary>
    public interface IRequiresCaravanAssetDurability
    {
        void SetCaravanAssetDurabilityReader(ICaravanAssetDurabilityReader reader);
    }
}

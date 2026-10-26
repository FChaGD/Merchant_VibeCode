namespace Game.Core
{
    /// <summary>
    /// 마구간 매각 창구(Docs/설계/103번 §4.1). 화면은 판정·매각가를 묻고 매각만 요청한다 - 지급·보유 제거는 구현체(system 브랜치)가 맡는다.
    /// </summary>
    public interface ICaravanAssetSaleService
    {
        CaravanAssetSaleCheck Evaluate(string instanceId);
        int SalePriceOf(string instanceId);
        bool TrySell(string instanceId);
    }
}

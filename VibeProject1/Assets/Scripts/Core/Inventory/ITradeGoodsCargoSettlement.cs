namespace Game.Core
{
    /// <summary>
    /// 전투 정산 전용 상단 물류품 조작(Docs/설계/79번 §5.1). 구매·매각 경로(IInventoryRepository.TryPlaceItem/RemoveItem)는
    /// 골드 상자를 지갑과 연동(배치=차감, 제거=환급)하지만, 전투로 잃은 물품은 환급되면 안 되고 회수 물품은 새로 사들인 것이
    /// 아니므로 차감되면 안 된다(기획 77번 §4-16·§4-19). 같은 메서드에 플래그를 붙이지 않고 계약을 따로 둔 이유는 구매·정리
    /// 소비자에게 환급·차감 없는 경로를 노출하지 않기 위해서다(ISP). 구현체는 이 타입으로 따로 DI 등록한다.
    /// </summary>
    public interface ITradeGoodsCargoSettlement : IInventoryStagingReader
    {
        /// <summary>배치·임시 보관 어디에 있든 제거한다. 골드 상자여도 환급하지 않는다. 없는 Id면 false.</summary>
        bool RemoveWithoutRefund(string instanceId);

        /// <summary>새 인스턴스를 임시 보관에 넣는다. 골드 상자여도 차감하지 않는다.</summary>
        InventoryItemInstance StageWithoutCharge(IInventoryItemDefinition definition);

        /// <summary>임시 보관을 전부 버린다(환급 없음) - 회수 적재 단계의 [넘어가기].</summary>
        void DiscardStaged();
    }
}

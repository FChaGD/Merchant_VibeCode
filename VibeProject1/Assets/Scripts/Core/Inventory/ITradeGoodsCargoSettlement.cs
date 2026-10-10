namespace Game.Core
{
    /// <summary>
    /// 전투 정산 전용 상단 물류품 조작(Docs/설계/79번 §5.1). 예전엔 일반 경로(TryPlaceItem/RemoveItem)가 골드 상자를 지갑과 연동해
    /// 따로 두었지만, 연동이 골드 보유 서비스로 옮겨져(설계 83번 §3.4) 지금은 일반 경로와 동작이 같다. battle 브랜치 정산 코드가
    /// 이 계약을 쓰므로 통합은 후속 리팩토링으로 남긴다(설계 83번 §10). 구현체는 이 타입으로 따로 DI 등록한다.
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

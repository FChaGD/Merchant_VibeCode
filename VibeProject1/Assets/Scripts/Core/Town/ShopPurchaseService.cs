namespace Game.Core
{
    /// <summary>
    /// 상점 구매 1건의 판정과 처리(Docs/설계/50번 §5.4). 재화 차감과 그리드 배치는 한쪽만 성공하면 재화가 사라지거나
    /// 복제되므로 여기서 한 연산으로 묶는다 - 차감 후 배치가 실패하면 환급한다(교역품 저장소의 골드 상자 처리와 같은 방식).
    /// 빈 자리는 그리드에 놓인 아이템만 기준으로 찾는다 - 임시 보관은 구매품을 받는 공간이 아니다(기획 48번 §3.6).
    /// </summary>
    public class ShopPurchaseService
    {
        private readonly IPlayerCurrencyWallet wallet;
        private readonly IInventoryRepository inventory;
        private readonly bool allowRotation;

        public ShopPurchaseService(IPlayerCurrencyWallet wallet, IInventoryRepository inventory, bool allowRotation)
        {
            this.wallet = wallet;
            this.inventory = inventory;
            this.allowRotation = allowRotation;
        }

        // 재화 부족과 공간 부족이 동시면 재화 부족을 먼저 알린다(설계 50번 §11-2).
        public ShopPurchaseCheck Evaluate(ShopStockEntry entry)
        {
            if (wallet.CurrentAmount < entry.Price) return ShopPurchaseCheck.InsufficientFunds;
            return TryFindSlot(entry, out _, out _) ? ShopPurchaseCheck.Available : ShopPurchaseCheck.NoSpace;
        }

        public bool TryPurchase(ShopStockEntry entry)
        {
            if (Evaluate(entry) != ShopPurchaseCheck.Available) return false;
            if (!TryFindSlot(entry, out var position, out var quarterTurns)) return false;
            if (!wallet.TrySpend(entry.Price)) return false;

            if (!inventory.TryPlaceItem(entry.Definition, position, out _, quarterTurns))
            {
                wallet.Add(entry.Price); // 배치 실패 롤백
                return false;
            }

            return true;
        }

        private bool TryFindSlot(ShopStockEntry entry, out GridPosition position, out int quarterTurns)
            => InventoryAutoSorter.TryFindSlot(inventory.GridWidth, inventory.GridHeight, inventory.Items, entry.Definition, allowRotation, out position, out quarterTurns);
    }
}

namespace Game.Core
{
    public enum CaravanAssetPurchaseCheck
    {
        Available,
        InsufficientFunds,
        OwnedFull,
        AlreadyOwned,
    }

    /// <summary>
    /// 마차·시설 구매 1건의 판정과 처리(Docs/설계/56번 §6). 재화 차감과 로스터 추가를 한 연산으로 묶고, 추가가 실패하면 환급한다
    /// (MercenaryHiringService와 같은 방식).
    /// </summary>
    public class CaravanAssetPurchaseService
    {
        // 종류별 보유 상한(기획 55번 §3) - 정비창 카테고리당 배치 상한(기획 11번)과 같은 값.
        public const int MaxOwnedPerKind = 5;

        private readonly IPlayerCurrencyWallet wallet;
        private readonly IOwnedCaravanAssetRoster roster;

        public CaravanAssetPurchaseService(IPlayerCurrencyWallet wallet, IOwnedCaravanAssetRoster roster)
        {
            this.wallet = wallet;
            this.roster = roster;
        }

        // 재화 부족을 먼저 알린다(다른 시설과 같은 순서).
        public CaravanAssetPurchaseCheck Evaluate(CaravanAssetProfile candidate)
        {
            if (wallet.CurrentAmount < candidate.Price) return CaravanAssetPurchaseCheck.InsufficientFunds;
            if (roster.CountOwnedOfKind(candidate.Kind) >= MaxOwnedPerKind) return CaravanAssetPurchaseCheck.OwnedFull;
            return CaravanAssetPurchaseCheck.Available;
        }

        public bool TryPurchase(CaravanAssetProfile candidate)
        {
            if (Evaluate(candidate) != CaravanAssetPurchaseCheck.Available) return false;
            if (!wallet.TrySpend(candidate.Price)) return false;

            if (!roster.TryAddOwned(candidate.Id, out _))
            {
                wallet.Add(candidate.Price); // 로스터 추가 실패 롤백
                return false;
            }

            return true;
        }
    }
}

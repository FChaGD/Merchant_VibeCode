using UnityEngine;

namespace Game.Core
{
    public enum CaravanAssetPurchaseCheck
    {
        Available,
        SoldOut,
        InsufficientFunds,
    }

    /// <summary>
    /// 마차·시설 구매 1건의 판정과 처리(Docs/설계/81번 §4.3). 보유 상한·"이미 보유"는 없다(기획 80번 §3-10, §3-3) - 같은 종류도 재고만큼 산다.
    /// 재화 차감 → 개체 발급 → 재고 차감 순이고, 발급이 실패하면 환급한다. 재고 차감은 판정에서 남은 수량을 확인한 같은 프레임이라
    /// 실패 경로가 없다 - 실패해도 지급은 유지한다(§4.1).
    /// </summary>
    public class CaravanAssetPurchaseService
    {
        private readonly IPlayerCurrencyWallet wallet;
        private readonly IOwnedCaravanAssetRoster roster;
        private readonly ITownStockReader stockReader;
        private readonly ITownStockConsumer stockConsumer;

        public CaravanAssetPurchaseService(IPlayerCurrencyWallet wallet, IOwnedCaravanAssetRoster roster, ITownStockReader stockReader, ITownStockConsumer stockConsumer)
        {
            this.wallet = wallet;
            this.roster = roster;
            this.stockReader = stockReader;
            this.stockConsumer = stockConsumer;
        }

        // 품절 → 재화 부족 순(설계 81번 §4.1) - 품절은 기다려도 풀리지 않으므로 먼저 알린다.
        public CaravanAssetPurchaseCheck Evaluate(CaravanAssetProfile candidate, int cityId)
        {
            if (RemainingOf(candidate, cityId) <= 0) return CaravanAssetPurchaseCheck.SoldOut;
            if (wallet.CurrentAmount < candidate.Price) return CaravanAssetPurchaseCheck.InsufficientFunds;
            return CaravanAssetPurchaseCheck.Available;
        }

        public bool TryPurchase(CaravanAssetProfile candidate, int cityId)
        {
            if (Evaluate(candidate, cityId) != CaravanAssetPurchaseCheck.Available) return false;
            if (!wallet.TrySpend(candidate.Price)) return false;
            if (!roster.TryAddOwned(candidate.Id, out _))
            {
                wallet.Add(candidate.Price); // 발급 실패 롤백
                return false;
            }

            if (!stockConsumer.TryConsume(cityId, TownStockCategories.Of(candidate.Kind), candidate.Id))
            {
                Debug.LogWarning($"{nameof(CaravanAssetPurchaseService)}: 마을 {cityId} 재고에서 '{candidate.Id}'를 차감하지 못했다(지급은 유지).");
            }
            return true;
        }

        private int RemainingOf(CaravanAssetProfile candidate, int cityId)
        {
            foreach (var line in stockReader.GetLines(cityId, TownStockCategories.Of(candidate.Kind)))
            {
                if (line.ItemId == candidate.Id) return line.Remaining;
            }
            return 0;
        }
    }
}

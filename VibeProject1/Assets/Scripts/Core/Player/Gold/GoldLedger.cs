using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 골드 보유 장부(Docs/설계/83번 §3.2·§4). MonoBehaviour와 분리한 이유는 Unity 없이 테스트하기 위해서다 - GoldHoldingsService가
    /// DI 배선만 맡고 이 클래스를 감싼다. 지갑과 교역품 저장소는 서로를 모르고, 둘을 함께 읽고 쓰는 곳은 이 클래스뿐이다.
    /// 교역품 저장소가 없으면 개인 골드만 다룬다(인스톨러 미실행 등). 골드 아이템 정의가 카탈로그에 없으면 여유 공간 환산·변환이 0이다.
    /// 지출·변환·버림은 지갑과 저장소를 여러 번 바꾸지만 변경 이벤트는 연산이 끝난 뒤 한 번만 낸다 - 구독자(HUD·시설 화면·변환 모달)가
    /// 중간 상태를 읽고 매번 다시 그리는 비용(무역품 화면은 구매 판정까지 다시 계산한다)을 없애기 위함이다.
    /// </summary>
    public sealed class GoldLedger : IGoldHoldingsReader, IGoldSpender, IGoldBoxConverter, IGoldDepartureSettlement, IDisposable
    {
        private readonly IPlayerCurrencyWallet wallet;
        private readonly ITradeGoodsInventoryRepository inventory;
        private readonly string goldItemId;
        private readonly int goldValue;
        private int batchDepth;
        private bool changedInBatch;

        public event Action Changed;

        public GoldLedger(IPlayerCurrencyWallet wallet, ITradeGoodsInventoryRepository inventory, string goldItemId, int goldValue)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.inventory = inventory;
            this.goldItemId = goldItemId;
            this.goldValue = goldValue;

            wallet.OnAmountChanged += HandleWalletChanged;
            // 교역품 저장소 계약이 조회·임시 보관 조회 두 인터페이스에서 같은 이벤트를 물려받아 이름이 모호하다 - 조회 계약으로 지정한다.
            if (inventory != null) ((IInventoryReader)inventory).OnChanged += HandleSourceChanged;
        }

        public void Dispose()
        {
            wallet.OnAmountChanged -= HandleWalletChanged;
            if (inventory != null) ((IInventoryReader)inventory).OnChanged -= HandleSourceChanged;
        }

        public int PersonalGold => wallet.CurrentAmount;
        public int PersonalLimit => wallet.PersonalLimit;
        public int GoldBoxValue => goldValue;

        public int LoadedGold => inventory == null ? 0 : GoldHoldingsCalculator.CountItems(inventory.Items, inventory.StagedItems, goldItemId) * goldValue;
        public int FreeCells => inventory == null ? 0 : GoldHoldingsCalculator.CountFreeCells(inventory.Sections, inventory.Items);
        public int FreeSpaceGold => FreeCells * PerCellValue();
        public int OwnedGold => PersonalGold + LoadedGold;
        public int CarryCapacity => PersonalLimit + FreeSpaceGold + LoadedGold;
        public int ExcessGold => Math.Max(0, PersonalGold - PersonalLimit);

        public int MaxConvertibleBoxes => IsSingleCellGold(out _) ? GoldHoldingsCalculator.MaxConvertible(PersonalGold, goldValue, FreeCells) : 0;
        public int ExcessBoxes => GoldHoldingsCalculator.ExcessBoxes(PersonalGold, PersonalLimit, goldValue, MaxConvertibleBoxes);

        public bool CanAfford(int amount) => amount <= OwnedGold;

        public IReadOnlyList<string> PreviewWithdrawal(int amount)
        {
            var shortfall = amount - PersonalGold;
            if (shortfall <= 0 || inventory == null || !CanAfford(amount)) return Array.Empty<string>();
            return GoldHoldingsCalculator.SelectWithdrawal(inventory.Sections, inventory.Items, inventory.StagedItems, goldItemId, GoldHoldingsCalculator.CeilDiv(shortfall, goldValue));
        }

        public bool TrySpend(int amount) => Batch(() => SpendCore(amount));

        private bool SpendCore(int amount)
        {
            if (amount <= 0) return true;
            if (!CanAfford(amount)) return false;

            var withdrawn = 0;
            foreach (var instanceId in PreviewWithdrawal(amount))
            {
                if (inventory.RemoveItem(instanceId)) withdrawn++;
            }
            if (withdrawn > 0) wallet.Add(withdrawn * goldValue);
            return wallet.TrySpend(amount);
        }

        public void Refund(int amount) => wallet.Add(amount);

        public int Convert(int boxCount) => Batch(() => ConvertCore(boxCount));

        private int ConvertCore(int boxCount)
        {
            var target = Math.Min(boxCount, MaxConvertibleBoxes);
            if (target <= 0 || !IsSingleCellGold(out var definition)) return 0;

            var converted = 0;
            for (var i = 0; i < target; i++)
            {
                // 보유 목록 순서(앞쪽 마차부터, 위 행·왼쪽부터)로 첫 빈자리 - 구매 자동 배치와 같은 규칙(§4.4).
                if (!InventoryAutoSorter.TryFindSlot(inventory.Sections, inventory.Items, definition, allowRotation: false, out var sectionId, out var position, out var turns)) break;
                if (!wallet.TrySpend(goldValue)) break;
                if (!inventory.TryPlaceItem(definition, position, out _, turns, sectionId))
                {
                    wallet.Add(goldValue);
                    break;
                }
                converted++;
            }
            return converted;
        }

        public int DiscardExcess()
        {
            var excess = ExcessGold;
            if (excess > 0) wallet.TrySpend(excess);
            return excess;
        }

        // 칸당 최대 효율(기획 82번 A2). 골드 아이템이 한 종류라 그 아이템의 칸당 가치다.
        private int PerCellValue()
        {
            if (inventory == null || !inventory.TryGetDefinition(goldItemId, out var definition)) return 0;
            var area = definition.FootprintWidth * definition.FootprintHeight;
            return area > 0 ? goldValue / area : 0;
        }

        // 변환 개수 계산이 1×1 전제라(빈 칸 수 = 자리 수) 다른 크기면 변환을 막는다(§10 후속 과제).
        private bool IsSingleCellGold(out IInventoryItemDefinition definition)
        {
            definition = null;
            return inventory != null && inventory.TryGetDefinition(goldItemId, out definition)
                && definition.FootprintWidth == 1 && definition.FootprintHeight == 1;
        }

        private void HandleWalletChanged(int _) => HandleSourceChanged();

        private void HandleSourceChanged()
        {
            if (batchDepth > 0) changedInBatch = true;
            else Changed?.Invoke();
        }

        // 연산 중 바뀐 게 있으면 끝난 뒤(최종 상태에서) 한 번만 알린다. 아무것도 바뀌지 않았으면 알리지 않는다.
        private T Batch<T>(Func<T> operation)
        {
            batchDepth++;
            try
            {
                return operation();
            }
            finally
            {
                batchDepth--;
                if (batchDepth == 0 && changedInBatch)
                {
                    changedInBatch = false;
                    Changed?.Invoke();
                }
            }
        }
    }
}

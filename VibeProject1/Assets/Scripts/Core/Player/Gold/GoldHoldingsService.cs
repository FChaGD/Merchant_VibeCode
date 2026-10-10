using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 골드 보유 장부(GoldLedger)의 Bootstrap 상주 래퍼(Docs/설계/83번 §3.2). DI 배선만 맡는다. 계약 4종을 각 타입으로 따로 등록한다 -
    /// 상위·하위 타입 등록으로는 조회되지 않는다. ResolveDependencies가 다시 불리면 장부를 새로 만들지만, 소비자는 이 컴포넌트의
    /// Changed를 구독하므로 구독이 끊기지 않는다.
    /// </summary>
    public class GoldHoldingsService : MonoBehaviour, IGoldHoldingsReader, IGoldSpender, IGoldBoxConverter, IGoldDepartureSettlement, IManagedComponent
    {
        // 골드 아이템(기획 82번 B1 - 기존 gold-box Placeholder 재사용)과 그 가치. 골드 아이템 종류가 늘면 이 두 값을 목록으로 바꾼다(§10).
        [SerializeField] private string goldItemId = "gold-box";
        [SerializeField] private int goldBoxValue = 500;

        private GoldLedger ledger;

        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IGoldHoldingsReader>(this);
            registrar.Register<IGoldSpender>(this);
            registrar.Register<IGoldBoxConverter>(this);
            registrar.Register<IGoldDepartureSettlement>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            DisposeLedger();
            if (registrar == null || !registrar.TryResolve(out IPlayerCurrencyWallet wallet))
            {
                Debug.LogWarning($"{nameof(GoldHoldingsService)}: {nameof(IPlayerCurrencyWallet)}가 없어 골드 보유 계산을 하지 않는다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            if (!registrar.TryResolve(out ITradeGoodsInventoryRepository inventory))
            {
                Debug.LogWarning($"{nameof(GoldHoldingsService)}: {nameof(ITradeGoodsInventoryRepository)}가 없어 개인 골드만 다룬다(Tools > Game > Build Bootstrap Scene).");
            }

            ledger = new GoldLedger(wallet, inventory, goldItemId, goldBoxValue);
            ledger.Changed += RaiseChanged;
        }

        private void OnDestroy() => DisposeLedger();

        public int PersonalGold => ledger?.PersonalGold ?? 0;
        public int PersonalLimit => ledger?.PersonalLimit ?? 0;
        public int LoadedGold => ledger?.LoadedGold ?? 0;
        public int FreeCells => ledger?.FreeCells ?? 0;
        public int FreeSpaceGold => ledger?.FreeSpaceGold ?? 0;
        public int OwnedGold => ledger?.OwnedGold ?? 0;
        public int CarryCapacity => ledger?.CarryCapacity ?? 0;
        public int ExcessGold => ledger?.ExcessGold ?? 0;
        public int GoldBoxValue => goldBoxValue;
        public int MaxConvertibleBoxes => ledger?.MaxConvertibleBoxes ?? 0;
        public int ExcessBoxes => ledger?.ExcessBoxes ?? 0;

        public bool CanAfford(int amount) => ledger != null && ledger.CanAfford(amount);
        public IReadOnlyList<string> PreviewWithdrawal(int amount) => ledger?.PreviewWithdrawal(amount) ?? Array.Empty<string>();
        public bool TrySpend(int amount) => ledger != null && ledger.TrySpend(amount);
        public void Refund(int amount) => ledger?.Refund(amount);
        public int Convert(int boxCount) => ledger?.Convert(boxCount) ?? 0;
        public int DiscardExcess() => ledger?.DiscardExcess() ?? 0;

        private void DisposeLedger()
        {
            if (ledger == null) return;
            ledger.Changed -= RaiseChanged;
            ledger.Dispose();
            ledger = null;
        }

        private void RaiseChanged() => Changed?.Invoke();
    }
}

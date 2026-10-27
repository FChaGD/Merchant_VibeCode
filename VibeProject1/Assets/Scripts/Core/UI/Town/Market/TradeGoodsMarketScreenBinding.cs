using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 무역품 구매 화면 바인딩(Docs/설계/50번 §6.5, Docs/Refactor/2026-10-26_씬전용.md F-1). 시설 화면 채널에 등록한다(공통 B-1) - 재화 패널 외 UI 숨김은
    /// 레이어 숨김 규칙이, 복귀는 채널 스택이 처리하고, 모달(골드 변환 등)이 열려도 닫히지 않는다.
    /// 의존성: 무역품 인벤토리·골드 지출·시설 판매 목록(필수), 마을 재고 차감(선택).
    /// </summary>
    internal sealed class TradeGoodsMarketScreenBinding : HubScreenBinding<TradeGoodsMarketPanel>
    {
        protected override TradeGoodsMarketPanel Create(HubScreenContext context)
        {
            var inventory = context.Optional<ITradeGoodsInventoryRepository>();
            var gold = context.Optional<IGoldSpender>();
            var stockReader = context.Optional<ITownShopStockReader>();
            if (inventory == null || gold == null || stockReader == null)
            {
                Debug.LogWarning($"무역품 구매 화면에 필요한 {nameof(ITradeGoodsInventoryRepository)}/{nameof(IGoldSpender)}/{nameof(ITownShopStockReader)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return null;
            }

            var sceneUIRoot = context.SceneUIRoot;
            if (!TradeGoodsMarketElements.TryBind(sceneUIRoot, InventoryPopupSpecs.TradeGoods.HasStaging, InventoryPopupSpecs.TradeGoods.HasSections, out var elements)) return null;

            // 구매 버튼 비활성 사유 툴팁(Docs/Refactor/2026-10-26_공통.md D-1) - 요소가 없으면 사유만 빠진다.
            var buyTooltip = ButtonTooltip.TryRegister(context.Tooltips, elements.BuyButton, sceneUIRoot, TradeGoodsMarketUIElementIds.BuyTooltip, TradeGoodsMarketUIElementIds.BuyTooltipText);
            var panel = new TradeGoodsMarketPanel(elements, inventory, gold, stockReader, context.Optional<ITownStockConsumer>(), context.CurrentLocation, context.UIManager, buyTooltip, context.Input, context.GoldItemInfo,
                context.Optional<IQuestCargoWarnings>());
            context.Panels.RegisterScreenPanel(panel);
            return panel;
        }
    }
}

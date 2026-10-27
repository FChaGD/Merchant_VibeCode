using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마구간 화면 바인딩(Docs/설계/56번 §7.2) - 용병단 접촉 화면과 같은 등록 방식(시설 화면 채널, 목록형 구매 화면 요소 공유).
    /// 의존성: 골드 지출·구매 후보·보유 명단·카탈로그·마을 재고 조회/차감(필수), 종류 아이콘·내구도 조회/기록·매각 서비스·무역품 인벤토리(선택 -
    /// 내구도가 없으면 "값 없음"+수리 막음, 매각 서비스가 없으면 매각 막음, 무역품이 없으면 화물 판정 구독 없음).
    /// </summary>
    internal sealed class StableScreenBinding : HubScreenBinding<StablePanel>
    {
        protected override StablePanel Create(HubScreenContext context)
        {
            var gold = context.Optional<IGoldSpender>();
            var candidateReader = context.Optional<ICaravanAssetCandidateReader>();
            var roster = context.Optional<IOwnedCaravanAssetRoster>();
            var catalog = context.Optional<ICaravanAssetCatalogReader>();
            var stockReader = context.Optional<ITownStockReader>();
            var stockConsumer = context.Optional<ITownStockConsumer>();
            if (gold == null || candidateReader == null || roster == null || catalog == null || stockReader == null || stockConsumer == null)
            {
                Debug.LogWarning($"마구간 화면에 필요한 {nameof(IGoldSpender)}/{nameof(ICaravanAssetCandidateReader)}/{nameof(IOwnedCaravanAssetRoster)}/{nameof(ICaravanAssetCatalogReader)}/{nameof(ITownStockReader)}/{nameof(ITownStockConsumer)}가 연결되어 있지 않아 등록하지 못했다(Tools > Game > Build Bootstrap Scene).");
                return null;
            }

            if (!RosterShopElements.TryBind(context.SceneUIRoot, RosterShopUIElementIds.StablePrefix, out var elements, requireOwnedListLabel: false)) return null;

            var ownedSection = CreateOwnedSection(context, gold, roster, catalog);
            var purchaseTooltip = RosterShopTooltips.Register(context, elements, RosterShopUIElementIds.StablePrefix);
            var panel = new StablePanel(elements, gold, candidateReader, roster, ownedSection, context.Optional<ICaravanAssetIconReader>(), stockReader, stockConsumer, context.CurrentLocation, context.UIManager, purchaseTooltip);
            context.Panels.RegisterScreenPanel(panel);
            return panel;
        }

        // 좌측 보유 개체 정보(수리·매각). 요소가 없으면(인스톨러 미실행) 보유 영역 없이 구매만.
        private static StableOwnedSection CreateOwnedSection(HubScreenContext context, IGoldSpender gold, IOwnedCaravanAssetRoster roster, ICaravanAssetCatalogReader catalog)
        {
            var sceneUIRoot = context.SceneUIRoot;
            if (!StableOwnedElements.TryBind(sceneUIRoot, out var ownedElements)) return null;

            // 매각(설계 103번 §4.6, 2026-10-26 좌측 개체 정보로 이동) - 요소가 없으면(인스톨러 미실행) 수리만.
            StableSaleController saleController = null;
            if (sceneUIRoot.TryGetElement<TMPro.TMP_Text>(StableUIElementIds.SalePrice, out var salePrice)
                && sceneUIRoot.TryGetElement<UnityEngine.UI.Button>(StableUIElementIds.SellButton, out var sellButton)
                && sceneUIRoot.TryGetElement<ConfirmDialogView>(StableUIElementIds.SaleConfirmDialog, out var saleDialog))
            {
                var saleService = context.Optional<ICaravanAssetSaleService>();
                if (saleService == null) Debug.LogWarning($"{nameof(ICaravanAssetSaleService)}가 없어 마구간 매각 버튼을 막는다(Tools > Game > Build Bootstrap Scene).");
                var sellTooltip = ButtonTooltip.TryRegister(context.Tooltips, sellButton, sceneUIRoot, StableUIElementIds.SellTooltip, StableUIElementIds.SellTooltipText);
                saleController = new StableSaleController(salePrice, sellTooltip, sellButton, saleDialog, saleService, roster, context.Optional<ITradeGoodsInventoryRepository>());
            }
            else
            {
                Debug.LogWarning($"Hub UI에서 마구간 매각 요소('{StableUIElementIds.SalePrice}'/'{StableUIElementIds.SellButton}'/'{StableUIElementIds.SaleConfirmDialog}')를 찾을 수 없어 매각 없이 등록한다(Tools > Game > Build Hub Scene).");
            }

            var durabilityReader = context.Optional<ICaravanAssetDurabilityReader>();
            var durabilityWriter = context.Optional<ICaravanAssetDurabilityWriter>();
            CaravanAssetRepairService repair = null;
            if (durabilityReader != null && durabilityWriter != null) repair = new CaravanAssetRepairService(gold, roster, durabilityReader, durabilityWriter);
            else Debug.LogWarning($"마구간 수리에 필요한 {nameof(ICaravanAssetDurabilityReader)}/{nameof(ICaravanAssetDurabilityWriter)}가 없어 수리를 막는다(Tools > Game > Build Bootstrap Scene).");
            var repairTooltip = ButtonTooltip.TryRegister(context.Tooltips, ownedElements.RepairButton, sceneUIRoot, StableUIElementIds.RepairTooltip, StableUIElementIds.RepairTooltipText);
            var repairAllTooltip = ButtonTooltip.TryRegister(context.Tooltips, ownedElements.RepairAllButton, sceneUIRoot, StableUIElementIds.RepairAllTooltip, StableUIElementIds.RepairAllTooltipText);
            return new StableOwnedSection(ownedElements, roster, catalog, durabilityReader, repair, gold, saleController, repairTooltip, repairAllTooltip);
        }
    }
}

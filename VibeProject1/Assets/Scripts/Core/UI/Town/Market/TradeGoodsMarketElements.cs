using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 무역품 구매 화면 요소 묶음(TownCategoryDepthElements와 같은 방식). 씬 로드 시 한 번만 찾아 둔다. 상점 쪽 요소나
    /// 좌측 상단 물류품 고정 패널 요소가 하나라도 없으면 화면 전체를 등록하지 않는다 - 요소가 빠진 화면은 열 수는 있어도
    /// 구매나 정리가 깨지기 때문이다.
    /// </summary>
    public sealed class TradeGoodsMarketElements
    {
        public GameObject Root { get; private set; }
        public Button ExitButton { get; private set; }
        public RectTransform StockViewport { get; private set; }
        public RectTransform StockContent { get; private set; }
        public ShopStockRowView StockRowTemplate { get; private set; }
        public TMP_Text EmptyStockLabel { get; private set; }
        public TMP_Text InfoName { get; private set; }
        public RectTransform InfoPreview { get; private set; }
        public Image InfoPreviewCellTemplate { get; private set; }
        public TMP_Text InfoPrice { get; private set; }
        public TMP_Text InfoDescription { get; private set; }
        public TMP_Text ReasonLabel { get; private set; }
        public Button BuyButton { get; private set; }
        public InventoryArrangementElements Inventory { get; private set; }

        public static bool TryBind(SceneUIRoot sceneUIRoot, bool inventoryHasStaging, bool inventoryHasSections, out TradeGoodsMarketElements elements)
        {
            elements = null;
            var ok = InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.Root, out RectTransform root)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.ExitButton, out Button exitButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.StockViewport, out RectTransform stockViewport)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.StockContent, out RectTransform stockContent)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.StockRowTemplate, out ShopStockRowView stockRowTemplate)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.EmptyStockLabel, out TMP_Text emptyStockLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.InfoName, out TMP_Text infoName)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.InfoPreview, out RectTransform infoPreview)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.InfoPreviewCellTemplate, out Image infoPreviewCellTemplate)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.InfoPrice, out TMP_Text infoPrice)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.InfoDescription, out TMP_Text infoDescription)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.ReasonLabel, out TMP_Text reasonLabel)
                & InventoryArrangementElements.TryGet(sceneUIRoot, TradeGoodsMarketUIElementIds.BuyButton, out Button buyButton)
                & InventoryArrangementElements.TryBind(sceneUIRoot, TradeGoodsMarketUIElementIds.InventoryPrefix, inventoryHasStaging, inventoryHasSections, out var inventory);
            if (!ok) return false;

            elements = new TradeGoodsMarketElements
            {
                Root = root.gameObject,
                ExitButton = exitButton,
                StockViewport = stockViewport,
                StockContent = stockContent,
                StockRowTemplate = stockRowTemplate,
                EmptyStockLabel = emptyStockLabel,
                InfoName = infoName,
                InfoPreview = infoPreview,
                InfoPreviewCellTemplate = infoPreviewCellTemplate,
                InfoPrice = infoPrice,
                InfoDescription = infoDescription,
                ReasonLabel = reasonLabel,
                BuyButton = buyButton,
                Inventory = inventory,
            };
            return true;
        }
    }
}

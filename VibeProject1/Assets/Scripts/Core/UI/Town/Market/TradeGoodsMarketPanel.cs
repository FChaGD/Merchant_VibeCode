using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 무역품 구매 화면(Docs/기획/48번, 설계 50번 §6.5). 모달 팝업 채널에 등록된다 - 모달 팝업이 열리면 재화 패널 외 Hub UI가
    /// 숨고, 닫으면 카테고리 depth가 그대로 남아 있어 "재화 패널만 남김"과 "나가면 시장 지구로 복귀"가 새 코드 없이 성립한다
    /// (설계 50번 §2.3). MonoBehaviour가 아닌 plain C#이며 HubUIWiring이 Hub 로드마다 새로 만든다 - 골드 보유·저장소(Bootstrap
    /// 상주) 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// 좌측 상단 물류품은 인벤토리 팝업과 같은 편집 본문(InventoryArrangementController)을 고정 패널로 쓴다. 임시 보관 상태는
    /// 저장소에 있으므로 팝업에서 남겨 둔 임시 보관 아이템이 그대로 이어진다(설계 50번 §6.2).
    /// </summary>
    public sealed class TradeGoodsMarketPanel : IUIPanel, IPanelCloseGuard, IDisposable
    {
        private const string EmptyStockText = "판매 품목 없음";
        private const string SoldOutText = "품절";
        private const string InsufficientFundsText = "재화가 부족합니다.";
        private const string NoSpaceText = "상단 물류품에 놓을 공간이 없습니다. 마차 공간을 정리해주세요.";
        private const string ExitBlockedText = "임시 보관 물품을 넣을 공간이 부족해 나갈 수 없습니다. 물건을 팔거나, 마차에 실어주세요.";
        private const float PreviewCellGap = 4f;
        private const int VisibleStockRows = 6; // 목록 영역에 한 번에 보이는 줄 수 - 줄 높이를 영역 높이 비율로 정한다(잠정)

        private readonly TradeGoodsMarketElements elements;
        private readonly ITradeGoodsInventoryRepository inventory;
        private readonly IGoldSpender gold;
        private readonly ITownShopStockReader stockReader;
        private readonly ITripCurrentLocationReader currentLocation; // 선택적 - 없으면 마을 Id 0(지금은 무시되는 값)
        private readonly InventoryArrangementController inventoryController;
        private readonly ShopPurchaseService purchaseService;
        private readonly List<ShopStockRowView> rowViews = new();
        private readonly List<UnityEngine.UI.Image> previewCells = new();

        private IReadOnlyList<ShopStockEntry> stock = Array.Empty<ShopStockEntry>();
        private int selectedIndex = -1;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.TradeGoodsMarket);

        public TradeGoodsMarketPanel(TradeGoodsMarketElements elements, ITradeGoodsInventoryRepository inventory, IGoldSpender gold, ITownShopStockReader stockReader, ITownStockConsumer stockConsumer, ITripCurrentLocationReader currentLocation, IUIManager uiManager)
        {
            this.elements = elements;
            this.inventory = inventory;
            this.gold = gold;
            this.stockReader = stockReader;
            this.currentLocation = currentLocation;

            // 회전·임시 보관 여부는 상단 물류품 팝업과 같은 스펙을 따른다 - 같은 인벤토리를 두 화면이 다른 규칙으로 다루지 않게.
            var spec = InventoryPopupSpecs.TradeGoods;
            inventoryController = new InventoryArrangementController(elements.Inventory, inventory, inventory, spec.AllowsRotation, spec.HasStaging, spec.HasSections);
            purchaseService = new ShopPurchaseService(gold, inventory, inventory, spec.AllowsRotation, stockConsumer);

            elements.StockRowTemplate.gameObject.SetActive(false);
            elements.InfoPreviewCellTemplate.gameObject.SetActive(false);

            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 닫기 차단(TryPrepareClose)과 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.BuyButton.onClick.RemoveAllListeners();
            elements.BuyButton.onClick.AddListener(Purchase);

            gold.Changed += HandleCurrencyChanged;
            stockReader.OnStockChanged += HandleStockChanged;
            // 교역품 저장소 계약이 조회·임시 보관 조회 두 인터페이스에서 같은 이벤트를 물려받아 이름이 모호하다 - 조회 계약으로 지정한다.
            ((IInventoryReader)inventory).OnChanged += HandleInventoryChanged;
            elements.Root.SetActive(false);
        }

        public void Open()
        {
            isOpen = true;
            var cityId = currentLocation?.CurrentCityId ?? 0;
            stock = stockReader.GetStock(cityId, TownFacilityIds.TradeGoodsMarket);
            selectedIndex = -1;

            elements.Root.SetActive(true);
            RenderStock();
            inventoryController.Show();
            UpdateInfo();
        }

        // Close()는 표시/숨김만 한다. 나가기 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        public void Close()
        {
            isOpen = false;
            inventoryController.Hide();
            elements.Root.SetActive(false);
        }

        /// <summary>
        /// 임시 보관 아이템을 빈 자리에 자동 배치하고, 전부 넣지 못하면 나가기를 막는다(Docs/기획/48번 §4.5).
        /// </summary>
        public bool TryPrepareClose()
        {
            if (inventoryController.TryFlushStaging()) return true;

            inventoryController.ShowMessage(ExitBlockedText);
            return false;
        }

        public void Dispose()
        {
            gold.Changed -= HandleCurrencyChanged;
            stockReader.OnStockChanged -= HandleStockChanged;
            ((IInventoryReader)inventory).OnChanged -= HandleInventoryChanged;
            inventoryController.Dispose();
        }

        private void HandleCurrencyChanged()
        {
            if (isOpen) UpdateInfo();
        }

        // 행이 사라지지 않으므로 선택을 유지한 채 수량만 다시 그린다(설계 81번 §4.2).
        private void HandleStockChanged()
        {
            if (!isOpen) return;
            stock = stockReader.GetStock(currentLocation?.CurrentCityId ?? 0, TownFacilityIds.TradeGoodsMarket);
            RenderStock();
            UpdateInfo();
        }

        private void HandleInventoryChanged()
        {
            if (isOpen) UpdateInfo();
        }

        private void Purchase()
        {
            if (!TryGetSelected(out var entry)) return;

            // 보이는 마차부터 자리를 찾고, 다른 마차에 들어가면 그 마차를 보여 준다(기획 63번 §3.5). 재화·저장소 이벤트로 화면이 갱신된다.
            if (purchaseService.TryPurchase(entry, currentLocation?.CurrentCityId ?? 0, inventoryController.CurrentSection?.Id, out var placedSectionId))
            {
                inventoryController.ShowSection(placedSectionId);
            }
            UpdateInfo();
        }

        private void RenderStock()
        {
            var viewportHeight = Mathf.Max(elements.StockViewport.rect.height, 1f);
            var rowHeight = viewportHeight / VisibleStockRows;

            for (var i = 0; i < stock.Count; i++)
            {
                if (i >= rowViews.Count) rowViews.Add(UnityEngine.Object.Instantiate(elements.StockRowTemplate, elements.StockContent));

                var row = rowViews[i];
                row.gameObject.SetActive(true);
                row.Bind(i, stock[i], Select);
                var rect = row.RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
                rect.sizeDelta = new Vector2(0f, rowHeight);
            }

            for (var i = stock.Count; i < rowViews.Count; i++) rowViews[i].gameObject.SetActive(false);

            elements.StockContent.sizeDelta = new Vector2(elements.StockContent.sizeDelta.x, stock.Count * rowHeight);
            elements.EmptyStockLabel.gameObject.SetActive(stock.Count == 0);
            elements.EmptyStockLabel.text = EmptyStockText;
        }

        private void Select(ShopStockRowView row)
        {
            selectedIndex = row.Index;
            UpdateInfo();
        }

        private bool TryGetSelected(out ShopStockEntry entry)
        {
            var valid = selectedIndex >= 0 && selectedIndex < stock.Count;
            entry = valid ? stock[selectedIndex] : default;
            return valid;
        }

        // 선택한 상품의 정보와 구매 가능 여부를 다시 그린다. 선택·재화·그리드가 바뀔 때마다 호출된다(기획 48번 §4.1).
        private void UpdateInfo()
        {
            for (var i = 0; i < rowViews.Count; i++) rowViews[i].SetSelected(i == selectedIndex);

            if (!TryGetSelected(out var entry))
            {
                elements.InfoName.text = string.Empty;
                elements.InfoPrice.text = string.Empty;
                elements.InfoDescription.text = string.Empty;
                elements.ReasonLabel.text = string.Empty;
                elements.BuyButton.interactable = false;
                RenderPreview(null);
                return;
            }

            elements.InfoName.text = entry.Definition.DisplayName;
            elements.InfoPrice.text = $"가격 {entry.Price:N0}";
            elements.InfoDescription.text = entry.Definition.Description;
            RenderPreview(entry.Definition);

            var check = purchaseService.Evaluate(entry);
            elements.BuyButton.interactable = check == ShopPurchaseCheck.Available;
            elements.ReasonLabel.text = check switch
            {
                ShopPurchaseCheck.SoldOut => SoldOutText,
                ShopPurchaseCheck.InsufficientFunds => InsufficientFundsText,
                ShopPurchaseCheck.NoSpace => NoSpaceText,
                _ => string.Empty,
            };
        }

        // 차지하는 칸 모양 미리보기 - 영역 안에 정사각형 칸으로 가운데 정렬한다. 칸 오브젝트는 재사용한다.
        private void RenderPreview(IInventoryItemDefinition definition)
        {
            var columns = definition?.FootprintWidth ?? 0;
            var rows = definition?.FootprintHeight ?? 0;
            var cellCount = columns * rows;
            var area = elements.InfoPreview.rect;
            var cellSize = cellCount == 0 ? 0f : Mathf.Min(area.width / columns, area.height / rows);
            var originX = (area.width - cellSize * columns) * 0.5f;
            var originY = (area.height - cellSize * rows) * 0.5f;
            var color = definition != null ? InventoryItemColorPalette.ColorFor(definition.Id) : Color.clear;

            for (var i = 0; i < cellCount; i++)
            {
                if (i >= previewCells.Count)
                {
                    previewCells.Add(UnityEngine.Object.Instantiate(elements.InfoPreviewCellTemplate, elements.InfoPreview));
                }

                var cell = previewCells[i];
                cell.gameObject.SetActive(true);
                cell.color = color;
                var x = i % columns;
                var y = i / columns;
                // 칸 사이에 틈을 둬 그리드 칸 모양이 보이게 한다.
                InventoryGridView.PlaceTopLeft(cell.rectTransform, originX + x * cellSize + PreviewCellGap * 0.5f, originY + y * cellSize + PreviewCellGap * 0.5f, cellSize - PreviewCellGap, cellSize - PreviewCellGap);
            }

            for (var i = cellCount; i < previewCells.Count; i++) previewCells[i].gameObject.SetActive(false);
        }
    }
}

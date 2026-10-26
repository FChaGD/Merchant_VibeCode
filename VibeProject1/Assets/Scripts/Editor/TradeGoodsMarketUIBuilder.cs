using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 무역품 구매 화면(Docs/설계/50번 §6.5·§6.6)을 get-or-create로 조립한다. 시설 화면은 앞으로 늘어나므로 HubSceneInstaller
    /// 내부 메서드로 두지 않고 별도 유틸리티로 뽑았다(InventoryPopupUIBuilder와 같은 이유). 저수준 조립은 EditorUIBuilder,
    /// 좌측 상단 물류품 고정 패널의 편집 본문은 InventoryPopupUIBuilder.BuildArrangementBody에 위임한다.
    /// 앵커 수치는 잠정값이다 - 사용자 실전 확인 후 조정한다(설계 50번 §11-3).
    /// </summary>
    internal static class TradeGoodsMarketUIBuilder
    {

        public static void Build(Transform screens)
        {
            var rootRect = EditorFacilityScreenBuilder.EnsureRoot(screens, EditorFacilityScreenBuilder.TradeGoodsMarketRoot, TradeGoodsMarketUIElementIds.Root);
            var root = rootRect.gameObject;

            EditorFacilityScreenBuilder.EnsureExitButton(rootRect, TradeGoodsMarketUIElementIds.ExitButton);
            BuildInventoryPanel(rootRect);
            BuildShopPanel(rootRect);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        // 떠 있는 팝업이 아니라 고정 패널이라 제목 줄은 드래그하지 않는 머리글이다(기획 48번 §4.4 - 창 드래그·닫기 버튼 제거).
        private static void BuildInventoryPanel(RectTransform root)
        {
            var panelRect = EditorFacilityScreenBuilder.EnsurePanel(root, "InventoryPanel", InventoryPopupSpecs.TradeGoods.Title, 0.02f, 0.55f);
            InventoryPopupUIBuilder.BuildArrangementBody(panelRect, TradeGoodsMarketUIElementIds.InventoryPrefix, InventoryPopupSpecs.TradeGoods.HasStaging, InventoryPopupSpecs.TradeGoods.HasSections);
        }

        private static void BuildShopPanel(RectTransform root)
        {
            var panelRect = EditorFacilityScreenBuilder.EnsurePanel(root, "ShopPanel", "무역품 구매", 0.58f, 0.98f);
            BuildStockList(panelRect);
            BuildItemInfo(panelRect);
        }

        // 판매 목록 - 패널 상단 약 55%. 줄 높이는 런타임(TradeGoodsMarketPanel)이 목록 영역 높이 비율로 정한다.
        private static void BuildStockList(RectTransform panel)
        {
            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "StockList");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.47f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, EditorUIColors.List);

            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(areaRect);
            EditorUIBuilder.EnsureMarker(viewport.gameObject, TradeGoodsMarketUIElementIds.StockViewport);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            EditorUIBuilder.EnsureMarker(content, TradeGoodsMarketUIElementIds.StockContent);
            EditorUIBuilder.ConfigureScrollRect(area, viewport, contentRect, horizontal: false, vertical: true);

            var empty = EditorUIBuilder.GetOrCreateUIObject(areaRect, "EmptyStock");
            EditorUIBuilder.SetStretch(empty.GetComponent<RectTransform>());
            var emptyLabel = EditorUIBuilder.EnsureLabel(empty.transform, "판매품 없음", autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            EditorUIBuilder.EnsureMarker(emptyLabel.gameObject, TradeGoodsMarketUIElementIds.EmptyStockLabel);

            BuildStockRowTemplate(areaRect);
        }

        // 런타임이 복제해 재사용하는 원본(비활성). 목록 콘텐츠 밖에 둬 복제본과 섞이지 않게 한다.
        private static void BuildStockRowTemplate(RectTransform area)
        {
            var templates = EditorUIBuilder.GetOrCreateUIObject(area, "Templates");
            EditorUIBuilder.SetStretch(templates.GetComponent<RectTransform>());

            var row = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "StockRowTemplate");
            EditorUIBuilder.EnsureImage(row, EditorUIColors.Row);
            var button = EditorUIBuilder.EnsureButton(row);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(row);
            outline.effectColor = EditorUIColors.SelectionOutline;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var swatch = EditorUIBuilder.GetOrCreateUIObject(row.transform, "Swatch");
            EditorUIBuilder.SetAnchors(swatch.GetComponent<RectTransform>(), new Vector2(0.02f, 0.15f), new Vector2(0.1f, 0.85f));
            var swatchImage = EditorUIBuilder.EnsureImage(swatch, Color.white);
            swatchImage.raycastTarget = false;

            var nameLabel = BuildRowLabel(row.transform, "Name", 0.12f, 0.45f, TextAlignmentOptions.MidlineLeft);
            // 남은 수량/품절(설계 81번 §4.2) - 이름 칸을 줄여 자리를 만든다.
            // 최대 크기는 "남은 수량 10"이 한 줄로 들어가는 값 - 22면 칸을 넘쳐 자동 축소되고 "품절"만 커져 크기가 어긋난다.
            var remainingLabel = BuildRowLabel(row.transform, "Remaining", 0.45f, 0.6f, TextAlignmentOptions.Center, RemainingFontSizeMax);
            remainingLabel.textWrappingMode = TextWrappingModes.NoWrap;
            var sizeLabel = BuildRowLabel(row.transform, "Size", 0.6f, 0.75f, TextAlignmentOptions.Center);
            var priceLabel = BuildRowLabel(row.transform, "Price", 0.75f, 0.97f, TextAlignmentOptions.MidlineRight);

            var view = EditorUIBuilder.GetOrAddComponent<ShopStockRowView>(row);
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("swatch").objectReferenceValue = swatchImage;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("sizeLabel").objectReferenceValue = sizeLabel;
            so.FindProperty("priceLabel").objectReferenceValue = priceLabel;
            so.FindProperty("remainingLabel").objectReferenceValue = remainingLabel;
            so.FindProperty("selectionOutline").objectReferenceValue = outline;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(row, TradeGoodsMarketUIElementIds.StockRowTemplate);
            row.SetActive(false);
        }

        private const float RowFontSizeMax = 22f;
        private const float RemainingFontSizeMax = 20f;

        private static TMP_Text BuildRowLabel(Transform row, string name, float minX, float maxX, TextAlignmentOptions alignment, float maxFontSize = RowFontSizeMax)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(row, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(minX, 0f), new Vector2(maxX, 1f));
            var label = EditorUIBuilder.EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: maxFontSize);
            label.alignment = alignment;
            return label;
        }

        // 상품 정보 - 패널 하단 약 45%(기획 48번 §4.3): 이름 / 칸 모양 미리보기 / 가격 / 설명 / 사유 문구 / 구매 버튼.
        private static void BuildItemInfo(RectTransform panel)
        {
            var info = EditorUIBuilder.GetOrCreateUIObject(panel, "ItemInfo");
            var infoRect = info.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(infoRect, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.45f));

            BuildInfoLabel(infoRect, "Name", new Vector2(0f, 0.84f), new Vector2(1f, 1f), TradeGoodsMarketUIElementIds.InfoName, TextAlignmentOptions.MidlineLeft, 28f);

            var preview = EditorUIBuilder.GetOrCreateUIObject(infoRect, "Preview");
            EditorUIBuilder.SetAnchors(preview.GetComponent<RectTransform>(), new Vector2(0f, 0.36f), new Vector2(0.3f, 0.82f));
            EditorUIBuilder.EnsureMarker(preview, TradeGoodsMarketUIElementIds.InfoPreview);
            var cell = EditorUIBuilder.GetOrCreateUIObject(preview.transform, "CellTemplate");
            EditorUIBuilder.EnsureImage(cell, Color.white).raycastTarget = false;
            EditorUIBuilder.EnsureMarker(cell, TradeGoodsMarketUIElementIds.InfoPreviewCellTemplate);
            cell.SetActive(false);

            BuildInfoLabel(infoRect, "Price", new Vector2(0.33f, 0.66f), new Vector2(1f, 0.82f), TradeGoodsMarketUIElementIds.InfoPrice, TextAlignmentOptions.MidlineLeft, 24f);
            // 사유 문구 줄은 구매 버튼 툴팁으로 옮겼다(Docs/Refactor/2026-10-26_공통.md D-1) - 빈 자리는 설명 칸이 아래로 늘어 메운다.
            EditorUIBuilder.DestroyChildIfExists(infoRect, "Reason");
            BuildInfoLabel(infoRect, "Description", new Vector2(0.33f, 0.2f), new Vector2(1f, 0.64f), TradeGoodsMarketUIElementIds.InfoDescription, TextAlignmentOptions.TopLeft, 20f);

            var buy = EditorUIBuilder.GetOrCreateUIObject(infoRect, "BuyButton");
            EditorUIBuilder.SetAnchors(buy.GetComponent<RectTransform>(), new Vector2(0.65f, 0f), new Vector2(1f, 0.17f));
            EditorUIBuilder.EnsureImage(buy, EditorUIColors.Button);
            EditorUIBuilder.EnsureButton(buy);
            EditorUIBuilder.EnsureLabel(buy.transform, "구매", autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(buy, TradeGoodsMarketUIElementIds.BuyButton);
            EditorUIBuilder.EnsureButtonTooltip(infoRect, buy, "BuyTooltip", TradeGoodsMarketUIElementIds.BuyTooltip, TradeGoodsMarketUIElementIds.BuyTooltipText, 0.65f, 1f, 0.18f);
        }

        private static TMP_Text BuildInfoLabel(RectTransform parent, string name, Vector2 min, Vector2 max, string id, TextAlignmentOptions alignment, float maxFontSize)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), min, max);
            var label = EditorUIBuilder.EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: maxFontSize);
            label.alignment = alignment;
            EditorUIBuilder.EnsureMarker(label.gameObject, id);
            return label;
        }
    }
}

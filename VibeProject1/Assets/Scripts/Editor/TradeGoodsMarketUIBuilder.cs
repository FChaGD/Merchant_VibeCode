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
        private static readonly Color PanelColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color ListColor = new(0.9f, 0.88f, 0.84f, 1f);
        private static readonly Color RowColor = new(1f, 1f, 1f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color SelectionOutlineColor = new(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color ReasonColor = new(0.75f, 0.15f, 0.1f, 1f);

        // 우상단 재화 패널(PopupExemptLayer)과 겹치지 않도록 두 패널 윗변을 여기까지로 제한한다.
        private const float PanelTop = 0.86f;

        public static void Build(Transform modalPopups)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "TradeGoodsMarket");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureMarker(root, TradeGoodsMarketUIElementIds.Root);

            BuildExitButton(rootRect);
            BuildInventoryPanel(rootRect);
            BuildShopPanel(rootRect);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        private static void BuildExitButton(RectTransform root)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(root, "ExitButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.02f, 0.88f), new Vector2(0.14f, 0.95f));
            EditorUIBuilder.EnsureImage(go, ButtonColor);
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "나가기", autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(go, TradeGoodsMarketUIElementIds.ExitButton);
        }

        // 떠 있는 팝업이 아니라 고정 패널이라 제목 줄은 드래그하지 않는 머리글이다(기획 48번 §4.4 - 창 드래그·닫기 버튼 제거).
        private static void BuildInventoryPanel(RectTransform root)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "InventoryPanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.02f, 0.06f), new Vector2(0.55f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildHeader(panelRect, InventoryPopupSpecs.TradeGoods.Title);
            InventoryPopupUIBuilder.BuildArrangementBody(panelRect, TradeGoodsMarketUIElementIds.InventoryPrefix, InventoryPopupSpecs.TradeGoods.HasStaging);
        }

        private static void BuildShopPanel(RectTransform root)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "ShopPanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.58f, 0.06f), new Vector2(0.98f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildHeader(panelRect, "무역품 구매");
            BuildStockList(panelRect);
            BuildItemInfo(panelRect);
        }

        private static void BuildHeader(RectTransform panel, string title)
        {
            var header = EditorUIBuilder.GetOrCreateUIObject(panel, "Header");
            EditorUIBuilder.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one);
            EditorUIBuilder.EnsureImage(header, HeaderColor).raycastTarget = false;
            EditorUIBuilder.EnsureLabel(header.transform, title, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
        }

        // 판매 목록 - 패널 상단 약 55%. 줄 높이는 런타임(TradeGoodsMarketPanel)이 목록 영역 높이 비율로 정한다.
        private static void BuildStockList(RectTransform panel)
        {
            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "StockList");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.47f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, ListColor);

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
            EditorUIBuilder.EnsureImage(row, RowColor);
            var button = EditorUIBuilder.EnsureButton(row);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(row);
            outline.effectColor = SelectionOutlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var swatch = EditorUIBuilder.GetOrCreateUIObject(row.transform, "Swatch");
            EditorUIBuilder.SetAnchors(swatch.GetComponent<RectTransform>(), new Vector2(0.02f, 0.15f), new Vector2(0.1f, 0.85f));
            var swatchImage = EditorUIBuilder.EnsureImage(swatch, Color.white);
            swatchImage.raycastTarget = false;

            var nameLabel = BuildRowLabel(row.transform, "Name", 0.12f, 0.6f, TextAlignmentOptions.MidlineLeft);
            var sizeLabel = BuildRowLabel(row.transform, "Size", 0.6f, 0.75f, TextAlignmentOptions.Center);
            var priceLabel = BuildRowLabel(row.transform, "Price", 0.75f, 0.97f, TextAlignmentOptions.MidlineRight);

            var view = EditorUIBuilder.GetOrAddComponent<ShopStockRowView>(row);
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("swatch").objectReferenceValue = swatchImage;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("sizeLabel").objectReferenceValue = sizeLabel;
            so.FindProperty("priceLabel").objectReferenceValue = priceLabel;
            so.FindProperty("selectionOutline").objectReferenceValue = outline;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(row, TradeGoodsMarketUIElementIds.StockRowTemplate);
            row.SetActive(false);
        }

        private static TMP_Text BuildRowLabel(Transform row, string name, float minX, float maxX, TextAlignmentOptions alignment)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(row, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(minX, 0f), new Vector2(maxX, 1f));
            var label = EditorUIBuilder.EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: 22f);
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
            BuildInfoLabel(infoRect, "Description", new Vector2(0.33f, 0.36f), new Vector2(1f, 0.64f), TradeGoodsMarketUIElementIds.InfoDescription, TextAlignmentOptions.TopLeft, 20f);
            var reason = BuildInfoLabel(infoRect, "Reason", new Vector2(0f, 0.2f), new Vector2(1f, 0.34f), TradeGoodsMarketUIElementIds.ReasonLabel, TextAlignmentOptions.MidlineLeft, 20f);
            reason.color = ReasonColor;

            var buy = EditorUIBuilder.GetOrCreateUIObject(infoRect, "BuyButton");
            EditorUIBuilder.SetAnchors(buy.GetComponent<RectTransform>(), new Vector2(0.65f, 0f), new Vector2(1f, 0.17f));
            EditorUIBuilder.EnsureImage(buy, ButtonColor);
            EditorUIBuilder.EnsureButton(buy);
            EditorUIBuilder.EnsureLabel(buy.transform, "구매", autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(buy, TradeGoodsMarketUIElementIds.BuyButton);
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

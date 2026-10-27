using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 마구간 보유 영역(목록 화면 + 개별 정보 화면, Docs/설계/91번 §5.2)을 목록형 구매 화면의 OwnedPanel 안에 get-or-create로 조립한다.
    /// 공용 조립(RosterShopUIBuilder)에 넣지 않고 마구간 전용 단계로 둔 이유: 용병 화면은 텍스트 목록 그대로라 공용 빌더에 분기를 늘리지 않기 위해서다.
    /// 앵커 수치는 잠정값이다 - 사용자 실전 확인 후 조정한다.
    /// </summary>
    internal static class StableOwnedUIBuilder
    {

        public static void Build(RectTransform stableRoot)
        {
            // RosterShopUIBuilder가 만든 OwnedPanel을 그대로 받는다(이미 있으면 get).
            var ownedPanel = EditorUIBuilder.GetOrCreateUIObject(stableRoot, "OwnedPanel").GetComponent<RectTransform>();
            BuildList(ownedPanel);
            BuildDetail(ownedPanel);

            // 매각 확인(설계 103번 §4.6) - 마구간 화면 전체를 덮는다. BuildConfirmDialog가 마지막 형제로 올려 다른 요소 위에 그린다.
            EditorUIBuilder.BuildConfirmDialog(stableRoot, "SaleConfirmDialog", StableUIElementIds.SaleConfirmDialog);
        }

        // 헤더(위 10%) 아래 전체. 목록 / 전체 수리 사유 / 전체 수리 버튼.
        private static void BuildList(RectTransform panel)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(panel, "OwnedListRoot");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(rootRect, Vector2.zero, new Vector2(1f, 0.9f));
            EditorUIBuilder.EnsureMarker(root, StableUIElementIds.ListRoot);

            var area = EditorUIBuilder.GetOrCreateUIObject(rootRect, "List");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.05f, 0.11f), new Vector2(0.95f, 0.98f));
            EditorUIBuilder.EnsureImage(area, EditorUIColors.List);

            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(areaRect);
            EditorUIBuilder.EnsureMarker(viewport.gameObject, StableUIElementIds.ListViewport);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            EditorUIBuilder.EnsureMarker(content, StableUIElementIds.ListContent);
            EditorUIBuilder.ConfigureScrollRect(area, viewport, contentRect, horizontal: false, vertical: true);

            BuildRowTemplate(areaRect);

            // 사유 문구 줄은 버튼 툴팁으로 옮겼다(Docs/Refactor/2026-10-26_공통.md D-1) - 빈 자리는 목록이 아래로 늘어 메운다.
            EditorUIBuilder.DestroyChildIfExists(rootRect, "RepairAllReason");
            var (repairAll, repairAllLabel) = BuildButton(rootRect, "RepairAllButton", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.09f), "전체 수리", StableUIElementIds.RepairAllButton);
            EditorUIBuilder.EnsureButtonTooltip(rootRect, repairAll.gameObject, "RepairAllTooltip", StableUIElementIds.RepairAllTooltip, StableUIElementIds.RepairAllTooltipText, 0.05f, 0.95f, 0.1f);
            EditorUIBuilder.EnsureMarker(repairAllLabel.gameObject, StableUIElementIds.RepairAllLabel);
            root.SetActive(true);
        }

        // 런타임이 복제해 재사용하는 원본(비활성). 목록 콘텐츠 밖에 둬 복제본과 섞이지 않게 한다.
        private static void BuildRowTemplate(RectTransform area)
        {
            var templates = EditorUIBuilder.GetOrCreateUIObject(area, "Templates");
            EditorUIBuilder.SetStretch(templates.GetComponent<RectTransform>());

            var row = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "OwnedRowTemplate");
            var background = EditorUIBuilder.EnsureImage(row, EditorUIColors.Row);
            var button = EditorUIBuilder.EnsureButton(row);
            var nameLabel = EditorUIBuilder.EnsureRowLabel(row.transform, "Name", 0.03f, 0.65f, TextAlignmentOptions.MidlineLeft);
            var durabilityLabel = EditorUIBuilder.EnsureRowLabel(row.transform, "Durability", 0.65f, 0.97f, TextAlignmentOptions.MidlineRight);

            var view = EditorUIBuilder.GetOrAddComponent<StableOwnedRowView>(row);
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("background").objectReferenceValue = background;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("durabilityLabel").objectReferenceValue = durabilityLabel;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(row, StableUIElementIds.RowTemplate);
            row.SetActive(false);
        }

        // 개별 정보 - 이름 / 종류 / 내구도 / 수리비 / 매각가 / 사유(좌 수리·우 매각) / [수리][매각] / [목록으로]. 저장 상태는 비활성(목록 화면이 기본).
        // 매각은 우측 정보 영역에 있다가 좌측으로 옮겼다(2026-10-26 사용자 지시) - 수리와 매각을 좌우로 나란히 둔다.
        private static void BuildDetail(RectTransform panel)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(panel, "OwnedDetailRoot");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(rootRect, Vector2.zero, new Vector2(1f, 0.9f));
            EditorUIBuilder.EnsureMarker(root, StableUIElementIds.DetailRoot);

            BuildLabel(rootRect, "Name", new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.97f), StableUIElementIds.DetailName, TextAlignmentOptions.MidlineLeft, 28f);
            BuildLabel(rootRect, "Kind", new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.85f), StableUIElementIds.DetailKind, TextAlignmentOptions.MidlineLeft, 24f);
            BuildLabel(rootRect, "Durability", new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.75f), StableUIElementIds.DetailDurability, TextAlignmentOptions.MidlineLeft, 24f);
            BuildLabel(rootRect, "Cost", new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.65f), StableUIElementIds.DetailCost, TextAlignmentOptions.MidlineLeft, 24f);
            BuildLabel(rootRect, "SalePrice", new Vector2(0.05f, 0.46f), new Vector2(0.95f, 0.55f), StableUIElementIds.SalePrice, TextAlignmentOptions.MidlineLeft, 24f);
            // 사유 문구 줄은 수리·매각 버튼 툴팁으로 옮겼다(Docs/Refactor/2026-10-26_공통.md D-1) - 버튼을 그 자리로 올리고, 툴팁은 버튼 바로 위에 뜬다.
            EditorUIBuilder.DestroyChildIfExists(rootRect, "Reason");
            EditorUIBuilder.DestroyChildIfExists(rootRect, "SaleReason");
            var (repair, _) = BuildButton(rootRect, "RepairButton", new Vector2(0.05f, 0.24f), new Vector2(0.48f, 0.38f), "수리", StableUIElementIds.RepairButton);
            var (sell, _) = BuildButton(rootRect, "SellButton", new Vector2(0.52f, 0.24f), new Vector2(0.95f, 0.38f), "매각", StableUIElementIds.SellButton);
            EditorUIBuilder.EnsureButtonTooltip(rootRect, repair.gameObject, "RepairTooltip", StableUIElementIds.RepairTooltip, StableUIElementIds.RepairTooltipText, 0.05f, 0.48f, 0.39f);
            EditorUIBuilder.EnsureButtonTooltip(rootRect, sell.gameObject, "SellTooltip", StableUIElementIds.SellTooltip, StableUIElementIds.SellTooltipText, 0.52f, 0.95f, 0.39f);
            BuildButton(rootRect, "BackButton", new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.16f), "목록으로", StableUIElementIds.BackButton);
            root.SetActive(false);
        }

        private static TMP_Text BuildLabel(RectTransform parent, string name, Vector2 min, Vector2 max, string id, TextAlignmentOptions alignment, float maxFontSize)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), min, max);
            var label = EditorUIBuilder.EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: maxFontSize);
            label.alignment = alignment;
            EditorUIBuilder.EnsureMarker(label.gameObject, id);
            return label;
        }

        private static (Button button, TMP_Text label) BuildButton(RectTransform parent, string name, Vector2 min, Vector2 max, string text, string id)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), min, max);
            EditorUIBuilder.EnsureImage(go, EditorUIColors.Button);
            var button = EditorUIBuilder.EnsureButton(go);
            var label = EditorUIBuilder.EnsureLabel(go.transform, text, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(go, id);
            return (button, label);
        }

    }
}

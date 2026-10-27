using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 상인조합 화면(Docs/설계/103번 §5.2)을 get-or-create로 조립한다 - 좌측 의뢰 목록 + 우측 상세·수락. 좌우 비율은 마구간·용병 화면과 같다.
    /// 행은 구매 행(아이콘·가격)과 칸 구성이 달라 전용 템플릿(GuildQuestRowView)을 만든다.
    /// </summary>
    internal static class MerchantGuildUIBuilder
    {

        public static void Build(Transform screens)
        {
            var rootRect = EditorFacilityScreenBuilder.EnsureRoot(screens, EditorFacilityScreenBuilder.MerchantGuildRoot, MerchantGuildUIElementIds.Root);
            var root = rootRect.gameObject;

            EditorFacilityScreenBuilder.EnsureExitButton(rootRect, MerchantGuildUIElementIds.ExitButton);
            BuildListPanel(rootRect);
            BuildDetailPanel(rootRect);
            // 포기 확인(설계 107번 §11.2) - 화면 전체를 덮어야 하므로 루트 아래 마지막 형제로 둔다.
            EditorUIBuilder.BuildConfirmDialog(rootRect, "ConfirmDialog", MerchantGuildUIElementIds.ConfirmDialog);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        private static void BuildListPanel(RectTransform root)
        {
            var panel = EditorFacilityScreenBuilder.EnsurePanel(root, "ListPanel", "의뢰 목록", 0.02f, 0.4f);

            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "List");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, EditorUIColors.List);

            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(areaRect);
            EditorUIBuilder.EnsureMarker(viewport.gameObject, MerchantGuildUIElementIds.ListViewport);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            EditorUIBuilder.EnsureMarker(content, MerchantGuildUIElementIds.ListContent);
            EditorUIBuilder.ConfigureScrollRect(area, viewport, contentRect, horizontal: false, vertical: true);

            var empty = EditorUIBuilder.GetOrCreateUIObject(areaRect, "Empty");
            EditorUIBuilder.SetStretch(empty.GetComponent<RectTransform>());
            var emptyLabel = EditorUIBuilder.EnsureLabel(empty.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            emptyLabel.raycastTarget = false;
            EditorUIBuilder.EnsureMarker(emptyLabel.gameObject, MerchantGuildUIElementIds.EmptyLabel);

            BuildRowTemplate(areaRect);
        }

        // 런타임이 복제해 재사용하는 원본(비활성). 목록 콘텐츠 밖에 둬 복제본과 섞이지 않게 한다.
        private static void BuildRowTemplate(RectTransform area)
        {
            var templates = EditorUIBuilder.GetOrCreateUIObject(area, "Templates");
            EditorUIBuilder.SetStretch(templates.GetComponent<RectTransform>());

            var row = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "QuestRowTemplate");
            EditorUIBuilder.EnsureImage(row, EditorUIColors.Row);
            var button = EditorUIBuilder.EnsureButton(row);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(row);
            outline.effectColor = EditorUIColors.SelectionOutline;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var title = EditorUIBuilder.EnsureRowLabel(row.transform, "Title", 0.03f, 0.55f, TextAlignmentOptions.MidlineLeft, raycastTarget: false);
            var requester = EditorUIBuilder.EnsureRowLabel(row.transform, "Requester", 0.55f, 0.8f, TextAlignmentOptions.MidlineLeft, raycastTarget: false);
            var status = EditorUIBuilder.EnsureRowLabel(row.transform, "Status", 0.8f, 0.97f, TextAlignmentOptions.MidlineRight, raycastTarget: false);

            var view = EditorUIBuilder.GetOrAddComponent<GuildQuestRowView>(row);
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("titleLabel").objectReferenceValue = title;
            so.FindProperty("requesterLabel").objectReferenceValue = requester;
            so.FindProperty("statusLabel").objectReferenceValue = status;
            so.FindProperty("selectionOutline").objectReferenceValue = outline;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(row, MerchantGuildUIElementIds.RowTemplate);
            row.SetActive(false);
        }

        private static void BuildDetailPanel(RectTransform root)
        {
            var panel = EditorFacilityScreenBuilder.EnsurePanel(root, "DetailPanel", "의뢰 상세", 0.43f, 0.98f);

            // 목표·기한이 추가되며(설계 107번 §11.2) 위에서부터 제목 / 설명 / 목표 / 기한 / 보상 / 상태 / 버튼 줄로 나눈다.
            BuildText(panel, "Title", MerchantGuildUIElementIds.DetailTitle, new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.88f), TextAlignmentOptions.MidlineLeft, 28f);
            BuildText(panel, "Description", MerchantGuildUIElementIds.DetailDescription, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.78f), TextAlignmentOptions.TopLeft, 20f);
            BuildText(panel, "Objectives", MerchantGuildUIElementIds.DetailObjectives, new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.61f), TextAlignmentOptions.TopLeft, 20f);
            BuildText(panel, "Deadline", MerchantGuildUIElementIds.DetailDeadline, new Vector2(0.05f, 0.33f), new Vector2(0.95f, 0.39f), TextAlignmentOptions.MidlineLeft, 20f);
            BuildText(panel, "Reward", MerchantGuildUIElementIds.DetailReward, new Vector2(0.05f, 0.20f), new Vector2(0.95f, 0.32f), TextAlignmentOptions.TopLeft, 20f);
            BuildText(panel, "Status", MerchantGuildUIElementIds.StatusLabel, new Vector2(0.05f, 0.13f), new Vector2(0.95f, 0.19f), TextAlignmentOptions.MidlineLeft, 22f);
            var accept = BuildButton(panel, "AcceptButton", "수락", MerchantGuildUIElementIds.AcceptButton, new Vector2(0.05f, 0.03f), new Vector2(0.33f, 0.11f));
            var abandon = BuildButton(panel, "AbandonButton", "포기", MerchantGuildUIElementIds.AbandonButton, new Vector2(0.36f, 0.03f), new Vector2(0.64f, 0.11f));
            var report = BuildButton(panel, "ReportButton", "보고", MerchantGuildUIElementIds.ReportButton, new Vector2(0.67f, 0.03f), new Vector2(0.95f, 0.11f));
            EditorUIBuilder.EnsureButtonTooltip(panel, accept, "AcceptTooltip", MerchantGuildUIElementIds.AcceptTooltip, MerchantGuildUIElementIds.AcceptTooltipText, 0.05f, 0.33f, 0.12f);
            EditorUIBuilder.EnsureButtonTooltip(panel, abandon, "AbandonTooltip", MerchantGuildUIElementIds.AbandonTooltip, MerchantGuildUIElementIds.AbandonTooltipText, 0.36f, 0.64f, 0.12f);
            EditorUIBuilder.EnsureButtonTooltip(panel, report, "ReportTooltip", MerchantGuildUIElementIds.ReportTooltip, MerchantGuildUIElementIds.ReportTooltipText, 0.67f, 0.95f, 0.12f);
        }

        // 저수준 조립은 공용 EditorUIBuilder에 있다 - 여기서는 이 화면의 색만 정해 넘긴다.

        private static GameObject BuildButton(RectTransform parent, string name, string text, string id, Vector2 min, Vector2 max)
            => EditorUIBuilder.EnsureLabeledButton(parent, name, text, id, min, max, EditorUIColors.Button);

        private static void BuildText(RectTransform parent, string name, string id, Vector2 min, Vector2 max, TextAlignmentOptions alignment, float maxFontSize)
            => EditorUIBuilder.EnsureAnchoredLabel(parent, name, id, min, max, alignment, maxFontSize);

    }
}

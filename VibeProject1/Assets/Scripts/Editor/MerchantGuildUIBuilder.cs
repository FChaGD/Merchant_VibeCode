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
        private static readonly Color PanelColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color ListColor = new(0.9f, 0.88f, 0.84f, 1f);
        private static readonly Color RowColor = new(1f, 1f, 1f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color SelectionOutlineColor = new(0.1f, 0.1f, 0.1f, 1f);

        private const float PanelTop = 0.86f;

        public static void Build(Transform modalPopups)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "MerchantGuild");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureMarker(root, MerchantGuildUIElementIds.Root);

            BuildButton(rootRect, "ExitButton", "나가기", MerchantGuildUIElementIds.ExitButton, new Vector2(0.02f, 0.88f), new Vector2(0.14f, 0.95f));
            BuildListPanel(rootRect);
            BuildDetailPanel(rootRect);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        private static void BuildListPanel(RectTransform root)
        {
            var panel = BuildPanel(root, "ListPanel", "의뢰 목록", new Vector2(0.02f, 0.06f), new Vector2(0.4f, PanelTop));

            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "List");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, ListColor);

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
            EditorUIBuilder.EnsureImage(row, RowColor);
            var button = EditorUIBuilder.EnsureButton(row);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(row);
            outline.effectColor = SelectionOutlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var title = BuildRowLabel(row.transform, "Title", 0.03f, 0.55f, TextAlignmentOptions.MidlineLeft);
            var requester = BuildRowLabel(row.transform, "Requester", 0.55f, 0.8f, TextAlignmentOptions.MidlineLeft);
            var status = BuildRowLabel(row.transform, "Status", 0.8f, 0.97f, TextAlignmentOptions.MidlineRight);

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
            var panel = BuildPanel(root, "DetailPanel", "의뢰 상세", new Vector2(0.43f, 0.06f), new Vector2(0.98f, PanelTop));

            BuildText(panel, "Title", MerchantGuildUIElementIds.DetailTitle, new Vector2(0.05f, 0.74f), new Vector2(0.95f, 0.87f), TextAlignmentOptions.MidlineLeft, 28f);
            BuildText(panel, "Description", MerchantGuildUIElementIds.DetailDescription, new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.72f), TextAlignmentOptions.TopLeft, 22f);
            BuildText(panel, "Reward", MerchantGuildUIElementIds.DetailReward, new Vector2(0.05f, 0.26f), new Vector2(0.95f, 0.36f), TextAlignmentOptions.MidlineLeft, 22f);
            BuildText(panel, "Status", MerchantGuildUIElementIds.StatusLabel, new Vector2(0.05f, 0.03f), new Vector2(0.6f, 0.15f), TextAlignmentOptions.MidlineLeft, 22f);
            BuildButton(panel, "AcceptButton", "수락", MerchantGuildUIElementIds.AcceptButton, new Vector2(0.65f, 0.03f), new Vector2(0.95f, 0.15f));
        }

        // 저수준 조립은 공용 EditorUIBuilder에 있다 - 여기서는 이 화면의 색만 정해 넘긴다.
        private static RectTransform BuildPanel(RectTransform root, string name, string title, Vector2 min, Vector2 max)
            => EditorUIBuilder.EnsureHeaderedPanel(root, name, title, min, max, PanelColor, HeaderColor);

        private static void BuildButton(RectTransform parent, string name, string text, string id, Vector2 min, Vector2 max)
            => EditorUIBuilder.EnsureLabeledButton(parent, name, text, id, min, max, ButtonColor);

        private static void BuildText(RectTransform parent, string name, string id, Vector2 min, Vector2 max, TextAlignmentOptions alignment, float maxFontSize)
            => EditorUIBuilder.EnsureAnchoredLabel(parent, name, id, min, max, alignment, maxFontSize);

        private static TMP_Text BuildRowLabel(Transform row, string name, float minX, float maxX, TextAlignmentOptions alignment)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(row, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(minX, 0f), new Vector2(maxX, 1f));
            var label = EditorUIBuilder.EnsureLabel(go.transform, string.Empty, autoSize: true, minFontSize: 10f, maxFontSize: 22f);
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }
    }
}

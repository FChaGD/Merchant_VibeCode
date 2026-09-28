using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 용병단 접촉 화면(Docs/설계/54번 §8)을 get-or-create로 조립한다. 무역품 구매 화면(TradeGoodsMarketUIBuilder)과 같은 배치 비율을
    /// 쓰지만 요소가 달라 별도 빌더로 둔다 - 시설 화면 간 공통 조립은 UI 관리 축 정리 때(ui.md §1-9) 함께 뽑는다. 저수준 조립은
    /// EditorUIBuilder에 위임한다. 앵커 수치는 잠정값이다 - 사용자 실전 확인 후 조정한다.
    /// </summary>
    internal static class MercenaryContactUIBuilder
    {
        private static readonly Color PanelColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color ListColor = new(0.9f, 0.88f, 0.84f, 1f);
        private static readonly Color RowColor = new(1f, 1f, 1f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color SelectionOutlineColor = new(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color ReasonColor = new(0.75f, 0.15f, 0.1f, 1f);

        // 우상단 재화 패널(PopupExemptLayer)과 겹치지 않도록 두 패널 윗변을 여기까지로 제한한다(무역품 구매 화면과 같은 값).
        private const float PanelTop = 0.86f;

        public static void Build(Transform modalPopups)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "MercenaryContact");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureMarker(root, MercenaryContactUIElementIds.Root);

            BuildExitButton(rootRect);
            BuildOwnedPanel(rootRect);
            BuildCandidatePanel(rootRect);

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
            EditorUIBuilder.EnsureMarker(go, MercenaryContactUIElementIds.ExitButton);
        }

        // 좌측 보유 용병 - 텍스트 블록 하나(설계 54번 §11). 내용은 런타임(MercenaryContactPanel)이 채운다.
        private static void BuildOwnedPanel(RectTransform root)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "OwnedPanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.02f, 0.06f), new Vector2(0.4f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildHeader(panelRect, "보유 용병");

            var body = EditorUIBuilder.GetOrCreateUIObject(panelRect, "OwnedList");
            EditorUIBuilder.SetAnchors(body.GetComponent<RectTransform>(), new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.88f));
            var label = EditorUIBuilder.EnsureLabel(body.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            label.alignment = TextAlignmentOptions.TopLeft;
            EditorUIBuilder.EnsureMarker(label.gameObject, MercenaryContactUIElementIds.OwnedListLabel);
        }

        private static void BuildCandidatePanel(RectTransform root)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "CandidatePanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.43f, 0.06f), new Vector2(0.98f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildHeader(panelRect, "용병단 접촉");
            BuildCandidateList(panelRect);
            BuildCandidateInfo(panelRect);
        }

        private static void BuildHeader(RectTransform panel, string title)
        {
            var header = EditorUIBuilder.GetOrCreateUIObject(panel, "Header");
            EditorUIBuilder.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one);
            EditorUIBuilder.EnsureImage(header, HeaderColor).raycastTarget = false;
            EditorUIBuilder.EnsureLabel(header.transform, title, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
        }

        // 후보 목록 - 패널 상단 약 55%. 줄 높이는 런타임이 목록 영역 높이 비율로 정한다.
        private static void BuildCandidateList(RectTransform panel)
        {
            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "CandidateList");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.47f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, ListColor);

            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(areaRect);
            EditorUIBuilder.EnsureMarker(viewport.gameObject, MercenaryContactUIElementIds.CandidateViewport);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            EditorUIBuilder.EnsureMarker(content, MercenaryContactUIElementIds.CandidateContent);
            EditorUIBuilder.ConfigureScrollRect(area, viewport, contentRect, horizontal: false, vertical: true);

            var empty = EditorUIBuilder.GetOrCreateUIObject(areaRect, "EmptyCandidate");
            EditorUIBuilder.SetStretch(empty.GetComponent<RectTransform>());
            var emptyLabel = EditorUIBuilder.EnsureLabel(empty.transform, "고용 가능한 용병 없음", autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            EditorUIBuilder.EnsureMarker(emptyLabel.gameObject, MercenaryContactUIElementIds.EmptyCandidateLabel);

            BuildCandidateRowTemplate(areaRect);
        }

        // 런타임이 복제해 재사용하는 원본(비활성). 목록 콘텐츠 밖에 둬 복제본과 섞이지 않게 한다.
        private static void BuildCandidateRowTemplate(RectTransform area)
        {
            var templates = EditorUIBuilder.GetOrCreateUIObject(area, "Templates");
            EditorUIBuilder.SetStretch(templates.GetComponent<RectTransform>());

            var row = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "CandidateRowTemplate");
            EditorUIBuilder.EnsureImage(row, RowColor);
            var button = EditorUIBuilder.EnsureButton(row);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(row);
            outline.effectColor = SelectionOutlineColor;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var icon = EditorUIBuilder.GetOrCreateUIObject(row.transform, "Icon");
            EditorUIBuilder.SetAnchors(icon.GetComponent<RectTransform>(), new Vector2(0.02f, 0.1f), new Vector2(0.1f, 0.9f));
            var iconImage = EditorUIBuilder.EnsureImage(icon, Color.white);
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            var nameLabel = BuildRowLabel(row.transform, "Name", 0.12f, 0.5f, TextAlignmentOptions.MidlineLeft);
            var classLabel = BuildRowLabel(row.transform, "Class", 0.5f, 0.72f, TextAlignmentOptions.Center);
            var costLabel = BuildRowLabel(row.transform, "Cost", 0.72f, 0.97f, TextAlignmentOptions.MidlineRight);

            var view = EditorUIBuilder.GetOrAddComponent<MercenaryCandidateRowView>(row);
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("icon").objectReferenceValue = iconImage;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("classLabel").objectReferenceValue = classLabel;
            so.FindProperty("costLabel").objectReferenceValue = costLabel;
            so.FindProperty("selectionOutline").objectReferenceValue = outline;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(row, MercenaryContactUIElementIds.CandidateRowTemplate);
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

        // 후보 정보 - 패널 하단 약 45%(기획 53번 §4.1): 이름 / 직업명 / 스탯 / 고용비 / 사유 문구 / 고용 버튼.
        private static void BuildCandidateInfo(RectTransform panel)
        {
            var info = EditorUIBuilder.GetOrCreateUIObject(panel, "CandidateInfo");
            var infoRect = info.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(infoRect, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.45f));

            BuildInfoLabel(infoRect, "Name", new Vector2(0f, 0.84f), new Vector2(0.65f, 1f), MercenaryContactUIElementIds.InfoName, TextAlignmentOptions.MidlineLeft, 28f);
            BuildInfoLabel(infoRect, "Class", new Vector2(0.65f, 0.84f), new Vector2(1f, 1f), MercenaryContactUIElementIds.InfoClass, TextAlignmentOptions.MidlineRight, 24f);
            BuildInfoLabel(infoRect, "Stats", new Vector2(0f, 0.36f), new Vector2(0.6f, 0.82f), MercenaryContactUIElementIds.InfoStats, TextAlignmentOptions.TopLeft, 22f);
            BuildInfoLabel(infoRect, "Cost", new Vector2(0.6f, 0.66f), new Vector2(1f, 0.82f), MercenaryContactUIElementIds.InfoCost, TextAlignmentOptions.MidlineRight, 24f);
            var reason = BuildInfoLabel(infoRect, "Reason", new Vector2(0f, 0.2f), new Vector2(1f, 0.34f), MercenaryContactUIElementIds.ReasonLabel, TextAlignmentOptions.MidlineLeft, 20f);
            reason.color = ReasonColor;

            var hire = EditorUIBuilder.GetOrCreateUIObject(infoRect, "HireButton");
            EditorUIBuilder.SetAnchors(hire.GetComponent<RectTransform>(), new Vector2(0.65f, 0f), new Vector2(1f, 0.17f));
            EditorUIBuilder.EnsureImage(hire, ButtonColor);
            EditorUIBuilder.EnsureButton(hire);
            EditorUIBuilder.EnsureLabel(hire.transform, "고용", autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(hire, MercenaryContactUIElementIds.HireButton);
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

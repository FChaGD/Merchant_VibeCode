using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 목록형 구매 화면(용병단 접촉·마구간, Docs/설계/56번 §7.1)을 get-or-create로 조립한다. 두 화면은 요소 구성이 같아 제목·문구·
    /// 요소 ID 접두사만 스펙으로 받는다. 저수준 조립은 EditorUIBuilder에 위임한다. 앵커 수치는 잠정값이다 - 사용자 실전 확인 후 조정한다.
    /// </summary>
    internal static class RosterShopUIBuilder
    {
        public readonly struct Spec
        {
            public readonly string ObjectName;
            public readonly string IdPrefix;
            public readonly string OwnedTitle;
            public readonly string CandidateTitle;
            public readonly string EmptyCandidateText;
            public readonly string ActionLabel;

            public Spec(string objectName, string idPrefix, string ownedTitle, string candidateTitle, string emptyCandidateText, string actionLabel)
            {
                ObjectName = objectName;
                IdPrefix = idPrefix;
                OwnedTitle = ownedTitle;
                CandidateTitle = candidateTitle;
                EmptyCandidateText = emptyCandidateText;
                ActionLabel = actionLabel;
            }
        }

        public static readonly Spec MercenaryContact = new("MercenaryContact", RosterShopUIElementIds.MercenaryContactPrefix, "보유 용병", "용병단 접촉", "고용 가능한 용병 없음", "고용");
        public static readonly Spec Stable = new("Stable", RosterShopUIElementIds.StablePrefix, "보유 현황", "마구간", "구매 가능한 마차·시설 없음", "구매");

        private static readonly Color PanelColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color ListColor = new(0.9f, 0.88f, 0.84f, 1f);
        private static readonly Color RowColor = new(1f, 1f, 1f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color SelectionOutlineColor = new(0.1f, 0.1f, 0.1f, 1f);
        private static readonly Color ReasonColor = new(0.75f, 0.15f, 0.1f, 1f);

        // 우상단 재화 패널(PopupExemptLayer)과 겹치지 않도록 두 패널 윗변을 여기까지로 제한한다(무역품 구매 화면과 같은 값).
        private const float PanelTop = 0.86f;

        public static void Build(Transform modalPopups, Spec spec)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, spec.ObjectName);
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureMarker(root, Id(spec, RosterShopUIElementIds.Root));

            BuildExitButton(rootRect, spec);
            BuildOwnedPanel(rootRect, spec);
            BuildCandidatePanel(rootRect, spec);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        private static string Id(Spec spec, string element) => RosterShopUIElementIds.Of(spec.IdPrefix, element);

        private static void BuildExitButton(RectTransform root, Spec spec)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(root, "ExitButton");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), new Vector2(0.02f, 0.88f), new Vector2(0.14f, 0.95f));
            EditorUIBuilder.EnsureImage(go, ButtonColor);
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, "나가기", autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(go, Id(spec, RosterShopUIElementIds.ExitButton));
        }

        // 좌측 보유 현황 - 텍스트 블록 하나(설계 54번 §11, 56번 §7). 내용은 런타임 패널이 채운다.
        private static void BuildOwnedPanel(RectTransform root, Spec spec)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "OwnedPanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.02f, 0.06f), new Vector2(0.4f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildHeader(panelRect, spec.OwnedTitle);

            var body = EditorUIBuilder.GetOrCreateUIObject(panelRect, "OwnedList");
            EditorUIBuilder.SetAnchors(body.GetComponent<RectTransform>(), new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.88f));
            var label = EditorUIBuilder.EnsureLabel(body.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            label.alignment = TextAlignmentOptions.TopLeft;
            EditorUIBuilder.EnsureMarker(label.gameObject, Id(spec, RosterShopUIElementIds.OwnedListLabel));
        }

        private static void BuildCandidatePanel(RectTransform root, Spec spec)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "CandidatePanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.43f, 0.06f), new Vector2(0.98f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildHeader(panelRect, spec.CandidateTitle);
            BuildCandidateList(panelRect, spec);
            BuildCandidateInfo(panelRect, spec);
        }

        private static void BuildHeader(RectTransform panel, string title)
        {
            var header = EditorUIBuilder.GetOrCreateUIObject(panel, "Header");
            EditorUIBuilder.SetAnchors(header.GetComponent<RectTransform>(), new Vector2(0f, 0.9f), Vector2.one);
            EditorUIBuilder.EnsureImage(header, HeaderColor).raycastTarget = false;
            EditorUIBuilder.EnsureLabel(header.transform, title, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
        }

        // 후보 목록 - 패널 상단 약 55%. 줄 높이는 런타임(RosterShopCandidateList)이 목록 영역 높이 비율로 정한다.
        private static void BuildCandidateList(RectTransform panel, Spec spec)
        {
            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "CandidateList");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.47f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, ListColor);

            var (viewport, content) = EditorUIBuilder.CreateViewportAndContent(areaRect);
            EditorUIBuilder.EnsureMarker(viewport.gameObject, Id(spec, RosterShopUIElementIds.CandidateViewport));
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            EditorUIBuilder.EnsureMarker(content, Id(spec, RosterShopUIElementIds.CandidateContent));
            EditorUIBuilder.ConfigureScrollRect(area, viewport, contentRect, horizontal: false, vertical: true);

            var empty = EditorUIBuilder.GetOrCreateUIObject(areaRect, "EmptyCandidate");
            EditorUIBuilder.SetStretch(empty.GetComponent<RectTransform>());
            var emptyLabel = EditorUIBuilder.EnsureLabel(empty.transform, spec.EmptyCandidateText, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            EditorUIBuilder.EnsureMarker(emptyLabel.gameObject, Id(spec, RosterShopUIElementIds.EmptyCandidateLabel));

            BuildCandidateRowTemplate(areaRect, spec);
        }

        // 런타임이 복제해 재사용하는 원본(비활성). 목록 콘텐츠 밖에 둬 복제본과 섞이지 않게 한다.
        private static void BuildCandidateRowTemplate(RectTransform area, Spec spec)
        {
            var templates = EditorUIBuilder.GetOrCreateUIObject(area, "Templates");
            EditorUIBuilder.SetStretch(templates.GetComponent<RectTransform>());

            var row = EditorUIBuilder.GetOrCreateUIObject(templates.transform, "CandidateRowTemplate");
            // 용병 전용 빌더 시절의 칸 이름(설계 54번) - 공용 빌더로 바뀌며 이름이 달라져 남은 옛 오브젝트를 정리한다.
            EditorUIBuilder.DestroyChildIfExists(row.transform, "Class");
            EditorUIBuilder.DestroyChildIfExists(row.transform, "Cost");

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
            var kindLabel = BuildRowLabel(row.transform, "Kind", 0.5f, 0.72f, TextAlignmentOptions.Center);
            var priceLabel = BuildRowLabel(row.transform, "Price", 0.72f, 0.97f, TextAlignmentOptions.MidlineRight);

            var view = EditorUIBuilder.GetOrAddComponent<RosterShopCandidateRowView>(row);
            var so = new SerializedObject(view);
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("icon").objectReferenceValue = iconImage;
            so.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            so.FindProperty("kindLabel").objectReferenceValue = kindLabel;
            so.FindProperty("priceLabel").objectReferenceValue = priceLabel;
            so.FindProperty("selectionOutline").objectReferenceValue = outline;
            so.ApplyModifiedProperties();
            EditorUIBuilder.EnsureMarker(row, Id(spec, RosterShopUIElementIds.CandidateRowTemplate));
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

        // 후보 정보 - 패널 하단 약 45%: 이름 / 종류 / 상세(용병 스탯, 마구간은 비움) / 가격 / 사유 문구 / 실행 버튼.
        private static void BuildCandidateInfo(RectTransform panel, Spec spec)
        {
            var info = EditorUIBuilder.GetOrCreateUIObject(panel, "CandidateInfo");
            var infoRect = info.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(infoRect, new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.45f));
            // 용병 전용 빌더 시절의 칸 이름 정리(위 행 템플릿과 같은 이유).
            EditorUIBuilder.DestroyChildIfExists(infoRect, "Class");
            EditorUIBuilder.DestroyChildIfExists(infoRect, "Stats");
            EditorUIBuilder.DestroyChildIfExists(infoRect, "Cost");
            EditorUIBuilder.DestroyChildIfExists(infoRect, "HireButton");

            BuildInfoLabel(infoRect, "Name", new Vector2(0f, 0.84f), new Vector2(0.65f, 1f), Id(spec, RosterShopUIElementIds.InfoName), TextAlignmentOptions.MidlineLeft, 28f);
            BuildInfoLabel(infoRect, "Kind", new Vector2(0.65f, 0.84f), new Vector2(1f, 1f), Id(spec, RosterShopUIElementIds.InfoKind), TextAlignmentOptions.MidlineRight, 24f);
            BuildInfoLabel(infoRect, "Detail", new Vector2(0f, 0.36f), new Vector2(0.6f, 0.82f), Id(spec, RosterShopUIElementIds.InfoDetail), TextAlignmentOptions.TopLeft, 22f);
            BuildInfoLabel(infoRect, "Price", new Vector2(0.6f, 0.66f), new Vector2(1f, 0.82f), Id(spec, RosterShopUIElementIds.InfoPrice), TextAlignmentOptions.MidlineRight, 24f);
            var reason = BuildInfoLabel(infoRect, "Reason", new Vector2(0f, 0.2f), new Vector2(1f, 0.34f), Id(spec, RosterShopUIElementIds.ReasonLabel), TextAlignmentOptions.MidlineLeft, 20f);
            reason.color = ReasonColor;

            var action = EditorUIBuilder.GetOrCreateUIObject(infoRect, "ActionButton");
            EditorUIBuilder.SetAnchors(action.GetComponent<RectTransform>(), new Vector2(0.65f, 0f), new Vector2(1f, 0.17f));
            EditorUIBuilder.EnsureImage(action, ButtonColor);
            EditorUIBuilder.EnsureButton(action);
            EditorUIBuilder.EnsureLabel(action.transform, spec.ActionLabel, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(action, Id(spec, RosterShopUIElementIds.ActionButton));
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

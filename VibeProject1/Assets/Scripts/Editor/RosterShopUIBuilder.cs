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
            // 보유 현황을 텍스트 레이블 하나로 둘지 - 마구간은 전용 조립(StableOwnedUIBuilder)으로 채운다(설계 91번 §5.2).
            public readonly bool OwnedListAsText;

            public Spec(string objectName, string idPrefix, string ownedTitle, string candidateTitle, string emptyCandidateText, string actionLabel, bool ownedListAsText = true)
            {
                OwnedListAsText = ownedListAsText;
                ObjectName = objectName;
                IdPrefix = idPrefix;
                OwnedTitle = ownedTitle;
                CandidateTitle = candidateTitle;
                EmptyCandidateText = emptyCandidateText;
                ActionLabel = actionLabel;
            }
        }

        public static readonly Spec MercenaryContact = new(EditorFacilityScreenBuilder.MercenaryContactRoot, RosterShopUIElementIds.MercenaryContactPrefix, "보유 용병", "용병단 접촉", "고용 가능한 용병 없음", "고용");
        public static readonly Spec Stable = new(EditorFacilityScreenBuilder.StableRoot, RosterShopUIElementIds.StablePrefix, "보유 현황", "마구간", "구매 가능한 마차·시설 없음", "구매", ownedListAsText: false);

        // 루트를 돌려준다 - 화면별 추가 조립(마구간 보유 영역)이 같은 루트 아래를 채운다.
        public static RectTransform Build(Transform screens, Spec spec)
        {
            var rootRect = EditorFacilityScreenBuilder.EnsureRoot(screens, spec.ObjectName, Id(spec, RosterShopUIElementIds.Root));
            var root = rootRect.gameObject;

            EditorFacilityScreenBuilder.EnsureExitButton(rootRect, Id(spec, RosterShopUIElementIds.ExitButton));
            BuildOwnedPanel(rootRect, spec);
            BuildCandidatePanel(rootRect, spec);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
            return rootRect;
        }

        private static string Id(Spec spec, string element) => RosterShopUIElementIds.Of(spec.IdPrefix, element);

        // 좌측 보유 현황 - 텍스트 블록 하나(설계 54번 §11, 56번 §7). 내용은 런타임 패널이 채운다.
        private static void BuildOwnedPanel(RectTransform root, Spec spec)
        {
            var panelRect = EditorFacilityScreenBuilder.EnsurePanel(root, "OwnedPanel", spec.OwnedTitle, 0.02f, 0.4f);

            // 마구간은 보유 영역을 전용 조립(StableOwnedUIBuilder)으로 채운다(설계 91번 §5.2) - 예전 텍스트 레이블은 지운다.
            if (!spec.OwnedListAsText)
            {
                EditorUIBuilder.DestroyChildIfExists(panelRect, "OwnedList");
                return;
            }

            var body = EditorUIBuilder.GetOrCreateUIObject(panelRect, "OwnedList");
            EditorUIBuilder.SetAnchors(body.GetComponent<RectTransform>(), new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.88f));
            var label = EditorUIBuilder.EnsureLabel(body.transform, string.Empty, autoSize: true, minFontSize: 12f, maxFontSize: 24f);
            label.alignment = TextAlignmentOptions.TopLeft;
            EditorUIBuilder.EnsureMarker(label.gameObject, Id(spec, RosterShopUIElementIds.OwnedListLabel));
        }

        private static void BuildCandidatePanel(RectTransform root, Spec spec)
        {
            var panelRect = EditorFacilityScreenBuilder.EnsurePanel(root, "CandidatePanel", spec.CandidateTitle, 0.43f, 0.98f);
            BuildCandidateList(panelRect, spec);
            BuildCandidateInfo(panelRect, spec);
        }

        // 후보 목록 - 패널 상단 약 55%. 줄 높이는 런타임(RosterShopCandidateList)이 목록 영역 높이 비율로 정한다.
        private static void BuildCandidateList(RectTransform panel, Spec spec)
        {
            var area = EditorUIBuilder.GetOrCreateUIObject(panel, "CandidateList");
            var areaRect = area.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(areaRect, new Vector2(0.03f, 0.47f), new Vector2(0.97f, 0.88f));
            EditorUIBuilder.EnsureImage(area, EditorUIColors.List);

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

            EditorUIBuilder.EnsureImage(row, EditorUIColors.Row);
            var button = EditorUIBuilder.EnsureButton(row);
            var outline = EditorUIBuilder.GetOrAddComponent<Outline>(row);
            outline.effectColor = EditorUIColors.SelectionOutline;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = false;

            var icon = EditorUIBuilder.GetOrCreateUIObject(row.transform, "Icon");
            EditorUIBuilder.SetAnchors(icon.GetComponent<RectTransform>(), new Vector2(0.02f, 0.1f), new Vector2(0.1f, 0.9f));
            var iconImage = EditorUIBuilder.EnsureImage(icon, Color.white);
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;

            var nameLabel = EditorUIBuilder.EnsureRowLabel(row.transform, "Name", 0.12f, 0.5f, TextAlignmentOptions.MidlineLeft);
            var kindLabel = EditorUIBuilder.EnsureRowLabel(row.transform, "Kind", 0.5f, 0.72f, TextAlignmentOptions.Center);
            var priceLabel = EditorUIBuilder.EnsureRowLabel(row.transform, "Price", 0.72f, 0.97f, TextAlignmentOptions.MidlineRight);

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
            // 사유 문구 줄은 행동 버튼 툴팁으로 옮겼다(Docs/Refactor/2026-10-26_공통.md D-1) - 빈 자리는 상세 칸이 아래로 늘어 메운다.
            EditorUIBuilder.DestroyChildIfExists(infoRect, "Reason");
            BuildInfoLabel(infoRect, "Detail", new Vector2(0f, 0.2f), new Vector2(0.6f, 0.82f), Id(spec, RosterShopUIElementIds.InfoDetail), TextAlignmentOptions.TopLeft, 22f);
            BuildInfoLabel(infoRect, "Price", new Vector2(0.6f, 0.66f), new Vector2(1f, 0.82f), Id(spec, RosterShopUIElementIds.InfoPrice), TextAlignmentOptions.MidlineRight, 24f);

            var action = EditorUIBuilder.GetOrCreateUIObject(infoRect, "ActionButton");
            EditorUIBuilder.SetAnchors(action.GetComponent<RectTransform>(), new Vector2(0.65f, 0f), new Vector2(1f, 0.17f));
            EditorUIBuilder.EnsureImage(action, EditorUIColors.Button);
            EditorUIBuilder.EnsureButton(action);
            EditorUIBuilder.EnsureLabel(action.transform, spec.ActionLabel, autoSize: true, minFontSize: 12f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(action, Id(spec, RosterShopUIElementIds.ActionButton));
            EditorUIBuilder.EnsureButtonTooltip(infoRect, action, "ActionTooltip", Id(spec, RosterShopUIElementIds.ActionTooltip), Id(spec, RosterShopUIElementIds.ActionTooltipText), 0.65f, 1f, 0.18f);
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

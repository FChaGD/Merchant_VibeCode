using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// 방향성 지시 패널의 화면 요소 트리를 만든다 - FormationUIBuilder와 같은 자리(HubSceneInstaller/
    /// FieldUIInstaller가 공유해서 쓰는 저수준 조립 로직, CLAUDE.md 씬 편집 컨벤션). 좌표는
    /// Docs/설계/11번 §5.1(카드 기준)을 그대로 코드로 옮긴 것이다.
    /// </summary>
    internal static class TacticsUIBuilder
    {
        public static void Build(Transform parent)
        {
            var panelRoot = EditorUIBuilder.GetOrCreateUIObject(parent, "TacticsPanel");
            EditorUIBuilder.SetStretch(panelRoot.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(panelRoot, TacticsUIElementIds.PanelRoot);
            var overlay = EditorUIBuilder.EnsureImage(panelRoot, new Color(0f, 0f, 0f, 0.55f));
            overlay.raycastTarget = true; // 뒤 UI 클릭을 차단한다.

            var card = EditorUIBuilder.GetOrCreateUIObject(panelRoot.transform, "Card");
            EditorUIBuilder.SetAnchors(card.GetComponent<RectTransform>(), new Vector2(0.20f, 0.10f), new Vector2(0.80f, 0.90f));
            EditorUIBuilder.EnsureImage(card, new Color(0.95f, 0.95f, 0.95f, 1f));

            BuildTitleAndClose(card.transform);
            var (tabParty, tabRoleGroup, tabIndividual) = BuildTabs(card.transform);

            var tabContentArea = EditorUIBuilder.GetOrCreateUIObject(card.transform, "TabContentArea");
            EditorUIBuilder.SetAnchors(tabContentArea.GetComponent<RectTransform>(), new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.76f));

            BuildPartyContent(tabContentArea.transform);
            BuildRoleGroupContent(tabContentArea.transform);
            BuildIndividualContent(tabContentArea.transform);

            panelRoot.SetActive(false); // 평소에는 숨김 - TacticsPanel.Open()이 켠다.
        }

        private static void BuildTitleAndClose(Transform card)
        {
            var titleGo = EditorUIBuilder.GetOrCreateUIObject(card, "Title");
            EditorUIBuilder.SetAnchors(titleGo.GetComponent<RectTransform>(), new Vector2(0.05f, 0.90f), new Vector2(0.45f, 0.98f));
            var titleLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(titleGo);
            titleLabel.text = "방향성 지시";
            titleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            titleLabel.fontSize = 26;
            titleLabel.color = Color.black;
            titleLabel.raycastTarget = false;

            var closeGo = EditorUIBuilder.GetOrCreateUIObject(card, "CloseButton");
            EditorUIBuilder.SetAnchors(closeGo.GetComponent<RectTransform>(), new Vector2(0.86f, 0.90f), new Vector2(0.97f, 0.98f));
            EditorUIBuilder.EnsureImage(closeGo, new Color(0.85f, 0.85f, 0.85f, 1f));
            EditorUIBuilder.EnsureButton(closeGo);
            EditorUIBuilder.EnsureLabel(closeGo.transform, "닫기");
            EditorUIBuilder.EnsureMarker(closeGo, TacticsUIElementIds.CloseButton);
        }

        private static (GameObject party, GameObject roleGroup, GameObject individual) BuildTabs(Transform card)
        {
            var tabParty = BuildTabButton(card, "TabParty", "파티 전체", new Vector2(0.05f, 0.80f), new Vector2(0.33f, 0.88f), TacticsUIElementIds.TabParty);
            var tabRoleGroup = BuildTabButton(card, "TabRoleGroup", "역할군", new Vector2(0.36f, 0.80f), new Vector2(0.64f, 0.88f), TacticsUIElementIds.TabRoleGroup);
            var tabIndividual = BuildTabButton(card, "TabIndividual", "개체별", new Vector2(0.67f, 0.80f), new Vector2(0.95f, 0.88f), TacticsUIElementIds.TabIndividual);
            return (tabParty, tabRoleGroup, tabIndividual);
        }

        private static GameObject BuildTabButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, string markerId)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureImage(go, new Color(0.85f, 0.85f, 0.9f, 1f));
            EditorUIBuilder.EnsureButton(go);
            EditorUIBuilder.EnsureLabel(go.transform, label);
            EditorUIBuilder.EnsureMarker(go, markerId);
            return go;
        }

        // ==================== 파티 전체 탭 ====================

        private static void BuildPartyContent(Transform tabContentArea)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(tabContentArea, "PartyContent");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(root, TacticsUIElementIds.PartyContentRoot);

            BuildLabeledDropdown(root.transform, "Recognition", "적 인식 유형", 0.83f, 1.00f, out _, TacticsUIElementIds.RecognitionDropdown);
            BuildLabeledDropdown(root.transform, "Radius", "활동 반경", 0.42f, 0.59f, out _, TacticsUIElementIds.RadiusDropdown);
            BuildLabeledDropdown(root.transform, "Pursuit", "추적", 0.00f, 0.17f, out _, TacticsUIElementIds.PursuitDropdown);
        }

        // 라벨(왼쪽)+드롭다운(오른쪽) 한 행 - Docs/설계/11번 §5.1 "Tab.Party 콘텐츠" 표를 그대로 옮김.
        private static void BuildLabeledDropdown(Transform parent, string name, string labelText, float bottom, float top, out TMP_Dropdown dropdown, string dropdownMarkerId)
        {
            var labelGo = EditorUIBuilder.GetOrCreateUIObject(parent, name + "Label");
            EditorUIBuilder.SetAnchors(labelGo.GetComponent<RectTransform>(), new Vector2(0.00f, bottom), new Vector2(0.28f, top));
            var label = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(labelGo);
            label.text = labelText;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 18;
            label.color = Color.black;
            label.raycastTarget = false;

            var dropdownGo = EditorUIBuilder.GetOrCreateUIObject(parent, name + "Dropdown");
            EditorUIBuilder.SetAnchors(dropdownGo.GetComponent<RectTransform>(), new Vector2(0.30f, bottom), new Vector2(0.68f, top));
            dropdown = EditorUIBuilder.EnsureDropdown(dropdownGo);
            EditorUIBuilder.EnsureMarker(dropdownGo, dropdownMarkerId);
        }

        // ==================== 역할군 탭 ====================

        private static void BuildRoleGroupContent(Transform tabContentArea)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(tabContentArea, "RoleGroupContent");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(root, TacticsUIElementIds.RoleGroupContentRoot);

            BuildRoleGroupBlock(
                root.transform, "Frontline", "전열", topBand: true,
                TacticsUIElementIds.FrontlineOverrideToggle, TacticsUIElementIds.FrontlineTargetDropdown,
                TacticsUIElementIds.FrontlinePositioningDropdown, TacticsUIElementIds.FrontlineSelfPreservationDropdown);

            BuildRoleGroupBlock(
                root.transform, "Ranged", "원거리딜러", topBand: false,
                TacticsUIElementIds.RangedOverrideToggle, TacticsUIElementIds.RangedTargetDropdown,
                TacticsUIElementIds.RangedPositioningDropdown, TacticsUIElementIds.RangedSelfPreservationDropdown);
        }

        // 전열은 위쪽 절반(0.60~1.00), 원거리딜러는 아래쪽 절반(0.17~0.57)을 쓴다(Docs/설계/11번 §5.1).
        private static void BuildRoleGroupBlock(
            Transform parent, string name, string roleGroupLabel, bool topBand,
            string overrideToggleMarkerId, string targetDropdownMarkerId, string positioningDropdownMarkerId, string selfPreservationDropdownMarkerId)
        {
            var headerBottom = topBand ? 0.83f : 0.40f;
            var headerTop = topBand ? 1.00f : 0.57f;
            var rowBottom = topBand ? 0.60f : 0.17f;
            var rowTop = topBand ? 0.77f : 0.34f;

            var headerGo = EditorUIBuilder.GetOrCreateUIObject(parent, name + "Header");
            EditorUIBuilder.SetAnchors(headerGo.GetComponent<RectTransform>(), new Vector2(0.00f, headerBottom), new Vector2(0.45f, headerTop));

            var labelGo = EditorUIBuilder.GetOrCreateUIObject(headerGo.transform, "Label");
            EditorUIBuilder.SetAnchors(labelGo.GetComponent<RectTransform>(), new Vector2(0.00f, 0.00f), new Vector2(0.45f, 1.00f));
            var label = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(labelGo);
            label.text = roleGroupLabel;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 20;
            label.color = Color.black;
            label.raycastTarget = false;

            var toggleGo = EditorUIBuilder.GetOrCreateUIObject(headerGo.transform, "OverrideToggle");
            EditorUIBuilder.SetAnchors(toggleGo.GetComponent<RectTransform>(), new Vector2(0.50f, 0.15f), new Vector2(0.65f, 0.85f));
            EditorUIBuilder.EnsureToggle(toggleGo);
            EditorUIBuilder.EnsureMarker(toggleGo, overrideToggleMarkerId);

            var toggleLabelGo = EditorUIBuilder.GetOrCreateUIObject(headerGo.transform, "OverrideLabel");
            EditorUIBuilder.SetAnchors(toggleLabelGo.GetComponent<RectTransform>(), new Vector2(0.68f, 0.00f), new Vector2(1.00f, 1.00f));
            var toggleLabel = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(toggleLabelGo);
            toggleLabel.text = "상단 지침";
            toggleLabel.alignment = TextAlignmentOptions.MidlineLeft;
            toggleLabel.fontSize = 14;
            toggleLabel.color = Color.gray;
            toggleLabel.raycastTarget = false;

            BuildDropdownOnly(parent, name + "Target", new Vector2(0.00f, rowBottom), new Vector2(0.30f, rowTop), targetDropdownMarkerId);
            BuildDropdownOnly(parent, name + "Positioning", new Vector2(0.33f, rowBottom), new Vector2(0.63f, rowTop), positioningDropdownMarkerId);
            BuildDropdownOnly(parent, name + "SelfPreservation", new Vector2(0.66f, rowBottom), new Vector2(0.96f, rowTop), selfPreservationDropdownMarkerId);
        }

        private static void BuildDropdownOnly(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string markerId)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name + "Dropdown");
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            EditorUIBuilder.EnsureDropdown(go);
            EditorUIBuilder.EnsureMarker(go, markerId);
        }

        // ==================== 개체별 탭(플레이스홀더) ====================

        private static void BuildIndividualContent(Transform tabContentArea)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(tabContentArea, "IndividualContent");
            EditorUIBuilder.SetStretch(root.GetComponent<RectTransform>());
            EditorUIBuilder.EnsureMarker(root, TacticsUIElementIds.IndividualContentRoot);

            var labelGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "PlaceholderLabel");
            EditorUIBuilder.SetStretch(labelGo.GetComponent<RectTransform>());
            var label = EditorUIBuilder.GetOrAddComponent<TextMeshProUGUI>(labelGo);
            label.text = "개체별 방향성은 이번 버전에서 지원하지 않습니다\n(향후 확장 예정)";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 18;
            label.color = Color.gray;
            label.raycastTarget = false;
        }
    }
}

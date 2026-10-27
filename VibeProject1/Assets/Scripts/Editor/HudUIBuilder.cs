using Game.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core.Editor
{
    /// <summary>
    /// Hub 상단 HUD 조립(재화 HUD·날짜 HUD, Docs/Refactor/2026-10-26_씬전용.md H-3) - 둘 다 PopupExemptLayer(모달 팝업 동안에도 보임)에 둔다.
    /// </summary>
    internal static class HudUIBuilder
    {
        // ==================== 재화 HUD ====================
        // 스타크래프트류 RTS 자원 바 형태(기획 27번 §3.1) - 항상 노출, 다른 패널이 열려도 가려지지
        // 않는다(27번 §3.5). 예전엔 SetAsLastSibling()으로 최상단을 강제했지만, 이제 PopupExemptLayer(항상
        // ContentRoot의 마지막 자식, 모달 팝업 시에도 유지) 안에 두는 것으로 구조적으로 보장된다(Docs/설계/38번 §5).
        public static void BuildCurrencyHud(Transform popupExemptLayer)
        {
            // 가로폭은 화면의 30%로 고정 요구사항(사용자 확정, 2026-09-23)이 생겨 X축만 스트레치
            // 앵커(anchorMin.x=0.7~anchorMax.x=1)로 바꿨다 - width가 화면 비율로 결정되므로
            // 2026-09-21에 겪은 "고정 픽셀 박스가 콘텐츠보다 작아서 아이콘이 밖으로 튀어나오는"
            // 문제는 재발하지 않는다(30%가 아이콘+텍스트+패딩보다 항상 크다). 세로축은 기존과
            // 동일하게 한 점 고정 + ContentSizeFitter로 콘텐츠 높이에 맞춘다.
            var root = EditorUIBuilder.GetOrCreateUIObject(popupExemptLayer, "PlayerCurrencyHud");
            var rootRect = root.GetComponent<RectTransform>();
            // 우상단 기준(사용자 확정, 2026-09-21). pivot.x=1/anchorMax.x=1이 겹쳐 anchoredPosition.x는
            // 화면 우측 모서리로부터의 오프셋으로 동작한다(스트레치 축이어도 점 앵커와 동일하게 계산됨).
            rootRect.anchorMin = new Vector2(0.7f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            // 화면 우상단 모서리로부터의 마진 2배 확대(사용자 확정, 2026-09-23: -16 -> -32).
            rootRect.anchoredPosition = new Vector2(-32f, -32f);
            rootRect.sizeDelta = new Vector2(0f, rootRect.sizeDelta.y);
            EditorUIBuilder.EnsureMarker(root, HubUIElementIds.CurrencyPanelRoot);
            // 패널 배경 - 이게 없으면 아이콘/텍스트만 화면에 떠 있는 것처럼 보여 "패널 형태"로 보이지
            // 않는다(2026-09-21 실전 확인). 입력은 골드 구역만 받으므로(설계 101번 §5.1) 배경은 레이캐스트 대상이 아니다.
            EditorUIBuilder.EnsureImage(root, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = false;
            // 예전엔 패널 전체가 입력 대상이었다(설계 83번 §6.1) - 루트의 중계를 걷어낸다.
            EditorUIBuilder.DestroyComponentIfExists<PointerHoverRelay>(root);
            EditorUIBuilder.DestroyComponentIfExists<PointerPressRelay>(root);
            // 패널 폭(화면 30%)이 바뀌면 세 레이블 공통 글자 크기를 다시 계산한다(설계 101번 §4.4).
            EditorUIBuilder.GetOrAddComponent<RectTransformDimensionsChangeRelay>(root);

            var layout = EditorUIBuilder.GetOrAddComponent<HorizontalLayoutGroup>(root);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var sizeFitter = EditorUIBuilder.GetOrAddComponent<ContentSizeFitter>(root);
            // 가로는 위에서 anchorMin/Max.x(0.7~1)로 화면의 30%를 직접 지정했으므로 ContentSizeFitter가
            // 다시 콘텐츠 크기로 덮어쓰면 안 된다(Unconstrained) - 세로만 콘텐츠 높이에 맞춘다.
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 골드 구역 = [Icon][금액] - 짧은 클릭=변환 모달, 호버·길게 누름=툴팁(설계 101번 §5.1). 투명 Image라도 UGUI 레이캐스트 대상이 된다.
            var goldSection = EditorUIBuilder.GetOrCreateUIObject(root.transform, "GoldSection");
            EditorUIBuilder.EnsureImage(goldSection, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            EditorUIBuilder.GetOrAddComponent<PointerHoverRelay>(goldSection);
            EditorUIBuilder.GetOrAddComponent<PointerPressRelay>(goldSection);
            EditorUIBuilder.EnsureMarker(goldSection, HubUIElementIds.CurrencyGoldSection);
            var goldLayout = EditorUIBuilder.GetOrAddComponent<HorizontalLayoutGroup>(goldSection);
            goldLayout.childAlignment = TextAnchor.MiddleLeft;
            goldLayout.spacing = 8f;
            goldLayout.padding = new RectOffset(0, 0, 0, 0);
            goldLayout.childControlWidth = true;
            goldLayout.childControlHeight = true;
            goldLayout.childForceExpandWidth = false;
            goldLayout.childForceExpandHeight = false;
            // 예전 구성은 Icon·Label이 패널 루트 바로 아래였다 - 골드 구역으로 옮긴다(재실행 안전).
            EditorUIBuilder.ReparentChildIfFound(root.transform, "Icon", goldSection.transform);
            EditorUIBuilder.ReparentChildIfFound(root.transform, "Label", goldSection.transform);

            EnsureCurrencyIcon(goldSection.transform, "Icon", HubUIElementIds.CurrencyIcon); // currencyIcon 미할당 시 자리표시자

            var amountLabel = EditorUIBuilder.EnsureLabel(goldSection.transform, "0");
            EditorUIBuilder.EnsureMarker(amountLabel.gameObject, HubUIElementIds.CurrencyAmountText);
            var legacyHoverRelay = amountLabel.GetComponent<PointerHoverRelay>();
            if (legacyHoverRelay != null) Object.DestroyImmediate(legacyHoverRelay);
            EnsureCurrencyCountLabel(amountLabel);
            var amountLayoutElement = EditorUIBuilder.GetOrAddComponent<LayoutElement>(amountLabel.gameObject);
            amountLayoutElement.minWidth = 60f;

            // 보유 현황(설계 101번 §5.1) - 아이콘은 자리만(흰 사각형), 입력 없음.
            EnsureCurrencyIcon(root.transform, "WagonIcon", HubUIElementIds.CurrencyWagonIcon);
            var wagonCountGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "WagonCount");
            var wagonCount = EditorUIBuilder.EnsureText(wagonCountGo);
            wagonCount.text = "-";
            EnsureCurrencyCountLabel(wagonCount);
            EditorUIBuilder.EnsureMarker(wagonCountGo, HubUIElementIds.CurrencyWagonCount);
            EnsureCurrencyIcon(root.transform, "UnitIcon", HubUIElementIds.CurrencyUnitIcon);
            var unitCountGo = EditorUIBuilder.GetOrCreateUIObject(root.transform, "UnitCount");
            var unitCount = EditorUIBuilder.EnsureText(unitCountGo);
            unitCount.text = "-";
            EnsureCurrencyCountLabel(unitCount);
            EditorUIBuilder.EnsureMarker(unitCountGo, HubUIElementIds.CurrencyUnitCount);

            goldSection.transform.SetSiblingIndex(0);
            root.transform.Find("WagonIcon").SetSiblingIndex(1);
            wagonCountGo.transform.SetSiblingIndex(2);
            root.transform.Find("UnitIcon").SetSiblingIndex(3);
            unitCountGo.transform.SetSiblingIndex(4);

            // 툴팁(설계 101번 §5.1) - 공용 툴팁 모양. 문구가 금액 칸보다 길어 패널 바로 아래 띠(패널 전체 폭)에 두고, 가로 레이아웃에서 뺀다.
            // 예전 구조(자식 "Label")는 걷어낸다.
            var legacyTooltip = root.transform.Find("CapacityTooltip");
            if (legacyTooltip != null) EditorUIBuilder.DestroyChildIfExists(legacyTooltip, "Label");
            var (tooltipGo, _) = EditorUIBuilder.EnsureTooltip(root.transform, "CapacityTooltip", HubUIElementIds.CurrencyCapacityTooltip, HubUIElementIds.CurrencyCapacityTooltipText);
            EditorUIBuilder.GetOrAddComponent<LayoutElement>(tooltipGo).ignoreLayout = true;
            var tooltipRect = tooltipGo.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0f, 0f);
            tooltipRect.anchorMax = new Vector2(1f, 0f);
            tooltipRect.pivot = new Vector2(0.5f, 1f);
            tooltipRect.sizeDelta = new Vector2(0f, tooltipRect.sizeDelta.y);
            tooltipRect.anchoredPosition = new Vector2(0f, -4f);
            tooltipGo.transform.SetAsLastSibling();
        }

        private static void EnsureCurrencyIcon(Transform parent, string name, string id)
        {
            var iconGo = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.EnsureImage(iconGo, Color.white).raycastTarget = false;
            EditorUIBuilder.EnsureMarker(iconGo, id);
            var iconLayoutElement = EditorUIBuilder.GetOrAddComponent<LayoutElement>(iconGo);
            iconLayoutElement.preferredWidth = 32f;
            iconLayoutElement.preferredHeight = 32f;
        }

        // 세 레이블 공통 크기는 런타임 컨트롤러가 정한다(설계 101번 §4.4) - 자동 크기·줄바꿈을 끄고 한 줄로 둔다.
        // EnsureLabel 기본 색은 검정(밝은 버튼 기준)이라 어두운 패널에 맞춰 흰색으로 바꾼다(2026-09-21 실전 확인).
        private static void EnsureCurrencyCountLabel(TMP_Text label)
        {
            label.enableAutoSizing = false;
            label.fontSize = 24f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        // ==================== 날짜 HUD ====================
        // 상단 중앙(사용자 결정 2026-10-11, 설계 85번 §4.1). 재화 HUD와 같은 PopupExemptLayer라 모달 팝업이 열려도 보인다.
        // 배경(Image)과 문구(TMP)는 부모/자식으로 나눈다 - 같은 오브젝트에 함께 붙이면 AddComponent가 실패한다(2026-09-21 사례).
        // 입력 대상이 아니므로 레이캐스트를 끈다 - 아래 UI 클릭을 막지 않게.
        public static void BuildDateHud(Transform popupExemptLayer)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(popupExemptLayer, "GameDateHud");
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -32f); // 재화 HUD와 같은 위 여백
            rootRect.sizeDelta = new Vector2(200f, 40f);
            EditorUIBuilder.EnsureImage(root, new Color(0f, 0f, 0f, 0.6f)).raycastTarget = false;
            EditorUIBuilder.EnsureMarker(root, HubUIElementIds.DatePanelRoot);

            var label = EditorUIBuilder.EnsureLabel(root.transform, "1월 1일", autoSize: true, minFontSize: 14f, maxFontSize: 26f);
            EditorUIBuilder.EnsureMarker(label.gameObject, HubUIElementIds.DateText);
            label.color = Color.white; // 어두운 배경 기준
        }
    }
}

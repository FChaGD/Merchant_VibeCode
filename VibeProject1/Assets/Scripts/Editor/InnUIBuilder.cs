using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.Core.Editor
{
    /// <summary>
    /// 숙소 화면과 휴식 진행 화면(Docs/설계/103번 §5.2)을 get-or-create로 조립한다. 시설 화면 조립은 기능별 유틸리티로 둔다
    /// (TradeGoodsMarketUIBuilder와 같은 이유). 저수준 조립은 EditorUIBuilder, 탭 본문은 InventoryPopupUIBuilder.BuildArrangementBody에 위임한다.
    /// 좌우 패널 비율은 다른 시설 화면(0.02–0.40 / 0.43–0.98, 윗변 0.86 = 우상단 재화 HUD 아래)과 같다. 진행 패널 앵커는 잠정값이다.
    /// </summary>
    internal static class InnUIBuilder
    {
        private static readonly Color PanelColor = new(0.96f, 0.95f, 0.92f, 1f);
        private static readonly Color HeaderColor = new(0.72f, 0.66f, 0.55f, 1f);
        private static readonly Color ButtonColor = new(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color GaugeBackColor = new(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color GaugeFillColor = new(0.35f, 0.7f, 0.35f, 1f);
        private static readonly Color AreaColor = new(0.9f, 0.88f, 0.84f, 1f);
        private static readonly Color DimColor = new(0f, 0f, 0f, 0.5f);

        private const float PanelTop = 0.86f;

        private static readonly InventoryPopupSpec[] TabSpecs =
        {
            InventoryPopupSpecs.Equipment,
            InventoryPopupSpecs.Consumable,
            InventoryPopupSpecs.PersonalItem,
        };

        public static void Build(Transform modalPopups)
        {
            BuildInn(modalPopups);
            BuildProgress(modalPopups);
        }

        private static void BuildInn(Transform modalPopups)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "Inn");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureMarker(root, InnUIElementIds.Root);

            BuildButton(rootRect, "ExitButton", "나가기", InnUIElementIds.ExitButton, new Vector2(0.02f, 0.88f), new Vector2(0.14f, 0.95f));
            BuildRestPanel(rootRect);
            BuildTabPanel(rootRect);

            // 런타임 패널이 등록될 때 숨기지만, 씬 편집 화면에서 다른 UI를 가리지 않도록 저장 상태도 비활성.
            root.SetActive(false);
        }

        // 좌측 휴식 설정(기획 94 4-1): 일수 스테퍼 + 1일 비용·총 비용 + "휴식". 비용 부족 사유는 버튼 툴팁으로만(4-11).
        private static void BuildRestPanel(RectTransform root)
        {
            var panel = BuildPanel(root, "RestPanel", "휴식", new Vector2(0.02f, 0.06f), new Vector2(0.4f, PanelTop));

            BuildButton(panel, "DaysDownButton", "▼", InnUIElementIds.DaysDownButton, new Vector2(0.12f, 0.62f), new Vector2(0.28f, 0.74f));
            BuildText(panel, "Days", InnUIElementIds.DaysLabel, new Vector2(0.3f, 0.62f), new Vector2(0.7f, 0.74f), TextAlignmentOptions.Center, 28f);
            BuildButton(panel, "DaysUpButton", "▲", InnUIElementIds.DaysUpButton, new Vector2(0.72f, 0.62f), new Vector2(0.88f, 0.74f));
            BuildText(panel, "CostPerDay", InnUIElementIds.CostPerDayLabel, new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.58f), TextAlignmentOptions.Center, 22f);
            BuildText(panel, "TotalCost", InnUIElementIds.TotalCostLabel, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.48f), TextAlignmentOptions.Center, 22f);

            // 비활성 버튼도 진입·이탈·누름 이벤트를 받는다 - 사유 툴팁 입력(설계 100번 §5.2와 같은 구성).
            var rest = BuildButton(panel, "RestButton", "휴식", InnUIElementIds.RestButton, new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.2f));
            EditorUIBuilder.GetOrAddComponent<PointerHoverRelay>(rest);
            EditorUIBuilder.GetOrAddComponent<PointerPressRelay>(rest);

            var (tooltip, _) = EditorUIBuilder.EnsureTooltip(panel, "RestTooltip", InnUIElementIds.RestTooltip, InnUIElementIds.RestTooltipText);
            var tooltipRect = tooltip.GetComponent<RectTransform>();
            tooltipRect.anchorMin = new Vector2(0.25f, 0.21f);
            tooltipRect.anchorMax = new Vector2(0.75f, 0.21f);
            tooltipRect.pivot = new Vector2(0.5f, 0f);
            tooltipRect.anchoredPosition = Vector2.zero;
            tooltipRect.sizeDelta = new Vector2(0f, tooltipRect.sizeDelta.y);
            tooltip.transform.SetAsLastSibling();
        }

        // 우측 탭(기획 94 4-3·4-4): 탭 버튼 줄 + 탭마다 인벤토리 편집 본문(일반 배치, 버리기 포함 - 설계 102번).
        private static void BuildTabPanel(RectTransform root)
        {
            var panel = EditorUIBuilder.GetOrCreateUIObject(root, "TabPanel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.43f, 0.06f), new Vector2(0.98f, PanelTop));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            var tabWidth = 1f / TabSpecs.Length;
            for (var i = 0; i < TabSpecs.Length; i++)
            {
                var spec = TabSpecs[i];
                BuildButton(panelRect, $"Tab_{spec.PopupId}", spec.Title, InnUIElementIds.TabButton(spec.PopupId),
                    new Vector2(i * tabWidth + 0.01f, 0.9f), new Vector2((i + 1) * tabWidth - 0.01f, 0.99f));

                var body = EditorUIBuilder.GetOrCreateUIObject(panelRect, $"Body_{spec.PopupId}");
                var bodyRect = body.GetComponent<RectTransform>();
                EditorUIBuilder.SetAnchors(bodyRect, new Vector2(0f, 0f), new Vector2(1f, 0.89f));
                // 빈 곳 클릭(선택 해제)을 받으려면 본문 루트가 레이캐스트 대상이어야 한다.
                EditorUIBuilder.EnsureImage(body, PanelColor);
                InventoryPopupUIBuilder.BuildArrangementBody(bodyRect, InnUIElementIds.BodyPrefix(spec.PopupId), spec.HasStaging, spec.HasSections);
            }
        }

        // 휴식 진행(기획 94 4-7·4-13): 재화·날짜 HUD만 남기도록 화면 전체를 덮고, 중앙 패널에 게이지·표시값·빈 영역 2개·결과·중단/확인.
        private static void BuildProgress(Transform modalPopups)
        {
            var root = EditorUIBuilder.GetOrCreateUIObject(modalPopups, "InnRestProgress");
            var rootRect = root.GetComponent<RectTransform>();
            EditorUIBuilder.SetStretch(rootRect);
            EditorUIBuilder.EnsureImage(root, DimColor);
            EditorUIBuilder.EnsureMarker(root, InnUIElementIds.ProgressRoot);

            var panel = EditorUIBuilder.GetOrCreateUIObject(rootRect, "Panel");
            var panelRect = panel.GetComponent<RectTransform>();
            EditorUIBuilder.SetAnchors(panelRect, new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.75f));
            EditorUIBuilder.EnsureImage(panel, PanelColor);

            BuildText(panelRect, "Days", InnUIElementIds.ProgressDays, new Vector2(0.05f, 0.8f), new Vector2(0.95f, 0.94f), TextAlignmentOptions.Center, 30f);

            var gauge = EditorUIBuilder.GetOrCreateUIObject(panelRect, "Gauge");
            EditorUIBuilder.SetAnchors(gauge.GetComponent<RectTransform>(), new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.77f));
            EditorUIBuilder.EnsureImage(gauge, GaugeBackColor).raycastTarget = false;
            var fill = EditorUIBuilder.GetOrCreateUIObject(gauge.transform, "Fill");
            // 런타임이 anchorMax.x를 진행 비율로 바꾼다.
            EditorUIBuilder.SetAnchors(fill.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0f, 1f));
            EditorUIBuilder.EnsureImage(fill, GaugeFillColor).raycastTarget = false;
            EditorUIBuilder.EnsureMarker(fill, InnUIElementIds.ProgressGaugeFill);

            BuildText(panelRect, "Date", InnUIElementIds.ProgressDate, new Vector2(0.05f, 0.57f), new Vector2(0.95f, 0.66f), TextAlignmentOptions.Center, 22f);
            BuildText(panelRect, "Cost", InnUIElementIds.ProgressCost, new Vector2(0.05f, 0.48f), new Vector2(0.95f, 0.57f), TextAlignmentOptions.Center, 22f);
            BuildText(panelRect, "Gold", InnUIElementIds.ProgressGold, new Vector2(0.05f, 0.39f), new Vector2(0.95f, 0.48f), TextAlignmentOptions.Center, 22f);

            // 이벤트 알림·회복 표시 자리(기획 94 4-7) - 내용은 system 브랜치가 채운다. 지금은 빈 영역.
            BuildArea(panelRect, "EventArea", InnUIElementIds.ProgressEventArea, new Vector2(0.05f, 0.25f), new Vector2(0.48f, 0.37f));
            BuildArea(panelRect, "RecoveryArea", InnUIElementIds.ProgressRecoveryArea, new Vector2(0.52f, 0.25f), new Vector2(0.95f, 0.37f));

            BuildText(panelRect, "Result", InnUIElementIds.ProgressResult, new Vector2(0.05f, 0.14f), new Vector2(0.95f, 0.24f), TextAlignmentOptions.Center, 24f);
            // 중단과 확인은 같은 자리 - 진행 중엔 중단, 결과 상태엔 확인만 보인다(기획 94 4-13).
            BuildButton(panelRect, "StopButton", "휴식 중단", InnUIElementIds.ProgressStopButton, new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.13f));
            BuildButton(panelRect, "ConfirmButton", "확인", InnUIElementIds.ProgressConfirmButton, new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.13f)).SetActive(false);

            root.SetActive(false);
        }

        // 저수준 조립은 공용 EditorUIBuilder에 있다 - 여기서는 이 화면의 색만 정해 넘긴다.
        private static RectTransform BuildPanel(RectTransform root, string name, string title, Vector2 min, Vector2 max)
            => EditorUIBuilder.EnsureHeaderedPanel(root, name, title, min, max, PanelColor, HeaderColor);

        private static GameObject BuildButton(RectTransform parent, string name, string text, string id, Vector2 min, Vector2 max)
            => EditorUIBuilder.EnsureLabeledButton(parent, name, text, id, min, max, ButtonColor);

        private static void BuildText(RectTransform parent, string name, string id, Vector2 min, Vector2 max, TextAlignmentOptions alignment, float maxFontSize)
            => EditorUIBuilder.EnsureAnchoredLabel(parent, name, id, min, max, alignment, maxFontSize);

        private static void BuildArea(RectTransform parent, string name, string id, Vector2 min, Vector2 max)
        {
            var go = EditorUIBuilder.GetOrCreateUIObject(parent, name);
            EditorUIBuilder.SetAnchors(go.GetComponent<RectTransform>(), min, max);
            EditorUIBuilder.EnsureImage(go, AreaColor).raycastTarget = false;
            EditorUIBuilder.EnsureMarker(go, id);
        }
    }
}

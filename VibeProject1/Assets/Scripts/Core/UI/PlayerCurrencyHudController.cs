using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 재화 HUD(Docs/설계/83번 §6.1, 101번 §4.3) - 한 줄에 소유 골드(적재분 포함)·마차 수·용병/시설 수를 상시 표기하고, 초과 상태를 색으로 알린다.
    /// 입력은 골드 구역만 받는다: 짧게 누르면 변환 모달, 호버하거나 길게 누르면 소지 가능량 툴팁(표시 판정은 공용 ITooltipController).
    /// 다른 패널/팝업이 열려도 가려지지 않아야 하므로(27번 §3.5) PopupExemptLayer에 둔다(설계 38번 §5).
    /// 골드 보유 서비스·보유 목록은 Bootstrap 상주라 Hub를 다시 방문할 때마다 이전 구독을 먼저 해제한다.
    /// 세 레이블은 같은 글자 크기를 쓴다 - 값이나 패널 폭이 바뀔 때만 다시 계산한다(Update 폴링 없음, 101번 §4.4).
    /// </summary>
    public class PlayerCurrencyHudController : MonoBehaviour, IPlayerCurrencyHudController
    {
        private const string NoRosterText = "-";

        [SerializeField] private Sprite currencyIcon;
        // 기획 82번 D4 - 노랑: 출발 시 버려질 개인 골드 있음, 빨강: 소유 골드가 소지 가능량을 넘음.
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color personalExcessColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color overCapacityColor = new(1f, 0.35f, 0.3f, 1f);
        // 공통 글자 크기 범위(설계 101번 §4.4) - 최대는 예전 금액 레이블 크기.
        [SerializeField] private float maxFontSize = 24f;
        [SerializeField] private float minFontSize = 12f;

        private IGoldHoldingsReader goldHoldings;
        private ICaravanRosterProvider roster;
        private IPanelNavigator uiManager;
        private RectTransform panelRoot;
        private TMP_Text amountText;
        private TMP_Text wagonCountText;
        private TMP_Text unitCountText;
        private TMP_Text reputationText;
        private IReputationReader reputation;
        private PointerPressRelay pressRelay;
        private RectTransformDimensionsChangeRelay dimensionsRelay;
        private ITooltipHandle tooltip;
        private float fittedPanelWidth = -1f;

        public void RegisterCurrencyUI(SceneUIRoot sceneUIRoot, IGoldHoldingsReader goldHoldings, IPanelNavigator uiManager, ICaravanRosterProvider roster, IReputationReader reputation = null)
        {
            Unbind();

            if (!sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyPanelRoot, out panelRoot)
                || !sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyAmountText, out amountText))
            {
                Debug.LogWarning($"Hub UI에서 재화 HUD 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
                return;
            }

            // 골드 보유 서비스가 없으면(인스톨러 미실행) HUD 전체를 숨긴다 - 값 없이 "0"을 보여주는 건 오해를 부른다.
            if (goldHoldings == null)
            {
                panelRoot.gameObject.SetActive(false);
                return;
            }

            this.goldHoldings = goldHoldings;
            this.roster = roster;
            this.uiManager = uiManager;

            if (sceneUIRoot.TryGetElement<Image>(HubUIElementIds.CurrencyIcon, out var icon) && currencyIcon != null)
            {
                icon.sprite = currencyIcon;
            }

            // 보유 현황 칸은 선택 요소 - 없으면(인스톨러 미실행) 골드만 표시한다.
            if (!sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyWagonCount, out wagonCountText)
                || !sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyUnitCount, out unitCountText))
            {
                Debug.LogWarning($"Hub UI에서 재화 HUD 보유 현황 칸을 찾을 수 없다(Tools > Game > Build Hub Scene).");
                wagonCountText = null;
                unitCountText = null;
            }

            // 평판 칸(설계 107번 §10) - 평판 계약이 없으면 숨기고, 칸이 없으면(인스톨러 미실행) 경고 후 생략한다.
            if (sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyReputation, out reputationText))
            {
                reputationText.gameObject.SetActive(reputation != null);
                if (reputation == null) reputationText = null;
            }
            else
            {
                Debug.LogWarning($"Hub UI에서 재화 HUD 평판 칸을 찾을 수 없다(Tools > Game > Build Hub Scene).");
            }
            this.reputation = reputationText != null ? reputation : null;

            BindGoldSection(sceneUIRoot);

            if (panelRoot.TryGetComponent(out dimensionsRelay))
            {
                dimensionsRelay.DimensionsChanged += HandlePanelResized;
            }

            goldHoldings.Changed += Refresh;
            if (roster != null) roster.RosterChanged += Refresh;
            if (this.reputation != null) this.reputation.Changed += Refresh;
            Refresh();
        }

        // 골드 구역의 중계 2개 - 짧은 클릭은 직접 받고, 호버·길게 누름은 공용 툴팁 컨트롤러에 등록한다.
        private void BindGoldSection(SceneUIRoot sceneUIRoot)
        {
            var tooltips = GetComponent<ITooltipController>();
            if (tooltips == null)
            {
                throw new InvalidOperationException($"{nameof(PlayerCurrencyHudController)}와 같은 GameObject에 {nameof(ITooltipController)} 구현체가 없다.");
            }

            if (!sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.CurrencyGoldSection, out var goldSection)
                || !goldSection.TryGetComponent<PointerHoverRelay>(out var hover)
                || !goldSection.TryGetComponent(out pressRelay))
            {
                Debug.LogWarning($"Hub UI에서 '{HubUIElementIds.CurrencyGoldSection}' 요소 또는 중계 컴포넌트를 찾을 수 없다(Tools > Game > Build Hub Scene).");
                pressRelay = null;
                return;
            }

            pressRelay.ShortClicked += OpenConversion;

            if (sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.CurrencyCapacityTooltip, out var tooltipRoot)
                && sceneUIRoot.TryGetElement<TMP_Text>(HubUIElementIds.CurrencyCapacityTooltipText, out var tooltipText))
            {
                tooltip = tooltips.Register(hover, pressRelay, tooltipRoot.gameObject, tooltipText);
            }
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (goldHoldings != null) goldHoldings.Changed -= Refresh;
            if (roster != null) roster.RosterChanged -= Refresh;
            if (reputation != null) reputation.Changed -= Refresh;
            if (pressRelay != null) pressRelay.ShortClicked -= OpenConversion;
            if (dimensionsRelay != null) dimensionsRelay.DimensionsChanged -= HandlePanelResized;
            tooltip?.Dispose();

            goldHoldings = null;
            roster = null;
            reputation = null;
            reputationText = null;
            pressRelay = null;
            dimensionsRelay = null;
            tooltip = null;
            fittedPanelWidth = -1f;
        }

        private void Refresh()
        {
            // 골드 보유 서비스·보유 목록(Bootstrap 상주)의 이벤트는 Hub가 언로드된 상태에서도 발화할 수 있다 - 텍스트가 파괴됐으면 무시한다.
            if (amountText == null || goldHoldings == null) return;

            // 합계만 보이면 개인 골드를 변환 모달까지 가야 알 수 있어 적재분을 함께 적는다(사용자 지시, 2026-10-15).
            amountText.text = $"{goldHoldings.OwnedGold:N0} (마차 적재량 {goldHoldings.LoadedGold:N0})";
            amountText.color = goldHoldings.OwnedGold > goldHoldings.CarryCapacity ? overCapacityColor
                : goldHoldings.PersonalGold > goldHoldings.PersonalLimit ? personalExcessColor
                : normalColor;

            if (wagonCountText != null && unitCountText != null)
            {
                if (roster == null)
                {
                    wagonCountText.text = NoRosterText;
                    unitCountText.text = NoRosterText;
                }
                else
                {
                    var composition = CaravanCompositionCounter.Count(roster.GetRoster());
                    wagonCountText.text = CaravanCompositionCounter.FormatWagons(composition.Wagons);
                    unitCountText.text = CaravanCompositionCounter.FormatUnits(composition.Characters, composition.Facilities);
                }
            }

            if (reputationText != null && reputation != null) reputationText.text = reputation.Value > 0 ? $"평판 +{reputation.Value}" : $"평판 {reputation.Value}";

            tooltip?.SetText($"최대량 {goldHoldings.CarryCapacity:N0} (개인 {goldHoldings.PersonalLimit:N0} / 적재 {goldHoldings.LoadedGold:N0} / 여유 공간 {goldHoldings.FreeSpaceGold:N0})");
            FitFontSize();
        }

        // 글자 크기를 바꾸면 패널 높이가 따라 바뀌어 크기 변경 알림이 다시 온다 - 폭이 그대로면 다시 계산하지 않는다.
        private void HandlePanelResized()
        {
            if (panelRoot == null || Mathf.Approximately(panelRoot.rect.width, fittedPanelWidth)) return;
            FitFontSize();
        }

        private void FitFontSize()
        {
            if (panelRoot == null || amountText == null) return;

            var list = new System.Collections.Generic.List<TMP_Text> { amountText };
            if (wagonCountText != null && unitCountText != null)
            {
                list.Add(wagonCountText);
                list.Add(unitCountText);
            }
            if (reputationText != null) list.Add(reputationText);
            var labels = list.ToArray();

            var required = new float[labels.Length];
            for (var i = 0; i < labels.Length; i++)
            {
                // 글자 폭은 크기에 비례하므로 현재 크기에서 잰 폭을 최대 크기 기준으로 환산한다.
                required[i] = labels[i].GetPreferredValues(labels[i].text, float.PositiveInfinity, float.PositiveInfinity).x * maxFontSize / labels[i].fontSize;
            }

            var size = HudUniformFontFitter.Fit(required, panelRoot.rect.width - FixedWidth(panelRoot, labels), maxFontSize, minFontSize);
            foreach (var label in labels) label.fontSize = size;
            fittedPanelWidth = panelRoot.rect.width;
        }

        // 레이블을 뺀 가로 레이아웃의 고정 폭(패딩·간격·아이콘). 레이아웃 그룹이 마지막 재구성 때 저장한 선호 폭은 문구 변경 직후 낡아 있으므로
        // 쓰지 않고 구조를 따라 직접 더한다 - 아이콘 폭은 LayoutElement 값이라 항상 최신이다.
        private static float FixedWidth(RectTransform container, TMP_Text[] labels)
        {
            if (!container.TryGetComponent<HorizontalLayoutGroup>(out var group)) return LayoutUtility.GetPreferredWidth(container);

            var width = (float)group.padding.horizontal;
            var count = 0;
            foreach (RectTransform child in container)
            {
                if (!child.gameObject.activeSelf) continue;
                if (child.TryGetComponent<LayoutElement>(out var element) && element.ignoreLayout) continue;

                count++;
                if (Array.IndexOf(labels, child.GetComponent<TMP_Text>()) >= 0) continue;
                width += FixedWidth(child, labels);
            }
            return width + group.spacing * Mathf.Max(0, count - 1);
        }

        private void OpenConversion() => uiManager?.Open(UIPanelIds.GoldConversion);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 골드 HUD(Docs/설계/83번 §6.1) - 소유 골드(개인 + 적재)를 상시 표기하고, 초과 상태를 색으로 알린다. 짧게 누르면 변환 모달,
    /// 호버하거나 길게 누르면 소지 가능량 툴팁을 띄운다. 다른 패널/팝업이 열려도 가려지지 않아야 하므로(27번 §3.5) PopupExemptLayer에
    /// 둔다(설계 38번 §5). 골드 보유 서비스는 Bootstrap 상주라 Hub를 다시 방문할 때마다 이전 구독을 먼저 해제한다.
    /// </summary>
    public class PlayerCurrencyHudController : MonoBehaviour, IPlayerCurrencyHudController
    {
        [SerializeField] private Sprite currencyIcon;
        // 기획 82번 D4 - 노랑: 출발 시 버려질 개인 골드 있음, 빨강: 소유 골드가 소지 가능량을 넘음.
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color personalExcessColor = new(1f, 0.85f, 0.2f, 1f);
        [SerializeField] private Color overCapacityColor = new(1f, 0.35f, 0.3f, 1f);

        private IGoldHoldingsReader goldHoldings;
        private IUIManager uiManager;
        private TMP_Text amountText;
        private GameObject tooltipRoot;
        private TMP_Text tooltipText;
        private PointerHoverRelay hoverRelay;
        private PointerPressRelay pressRelay;
        private bool hovering;
        private bool longPressing;

        public void RegisterCurrencyUI(SceneUIRoot sceneUIRoot, IGoldHoldingsReader goldHoldings, IUIManager uiManager)
        {
            Unbind();

            if (!sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.CurrencyPanelRoot, out var panelRoot)
                || !sceneUIRoot.TryGetElement<TMP_Text>(HubUIElementIds.CurrencyAmountText, out amountText))
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
            this.uiManager = uiManager;

            if (sceneUIRoot.TryGetElement<Image>(HubUIElementIds.CurrencyIcon, out var icon) && currencyIcon != null)
            {
                icon.sprite = currencyIcon;
            }

            if (sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.CurrencyCapacityTooltip, out var tooltipRect)
                && sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyCapacityTooltipText, out tooltipText))
            {
                tooltipRoot = tooltipRect.gameObject;
                tooltipRoot.SetActive(false);
            }

            if (sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyPanelRoot, out hoverRelay))
            {
                hoverRelay.PointerEntered += HandlePointerEntered;
                hoverRelay.PointerExited += HandlePointerExited;
            }

            if (sceneUIRoot.TryGetElement(HubUIElementIds.CurrencyPanelRoot, out pressRelay))
            {
                pressRelay.ShortClicked += OpenConversion;
                pressRelay.LongPressStarted += HandleLongPressStarted;
                pressRelay.LongPressEnded += HandleLongPressEnded;
            }

            goldHoldings.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (goldHoldings != null) goldHoldings.Changed -= Refresh;
            if (hoverRelay != null)
            {
                hoverRelay.PointerEntered -= HandlePointerEntered;
                hoverRelay.PointerExited -= HandlePointerExited;
            }
            if (pressRelay != null)
            {
                pressRelay.ShortClicked -= OpenConversion;
                pressRelay.LongPressStarted -= HandleLongPressStarted;
                pressRelay.LongPressEnded -= HandleLongPressEnded;
            }

            goldHoldings = null;
            hoverRelay = null;
            pressRelay = null;
            tooltipRoot = null;
            tooltipText = null;
            hovering = false;
            longPressing = false;
        }

        private void Refresh()
        {
            // 골드 보유 서비스(Bootstrap 상주)의 Changed는 Hub가 언로드된 상태에서도 발화할 수 있다 - 텍스트가 파괴됐으면 무시한다.
            if (amountText == null || goldHoldings == null) return;

            amountText.text = goldHoldings.OwnedGold.ToString("N0");
            amountText.color = goldHoldings.OwnedGold > goldHoldings.CarryCapacity ? overCapacityColor
                : goldHoldings.PersonalGold > goldHoldings.PersonalLimit ? personalExcessColor
                : normalColor;
            if (tooltipRoot != null && tooltipRoot.activeSelf) RenderTooltip();
        }

        private void OpenConversion() => uiManager?.Open(UIPanelIds.GoldConversion);

        private void HandlePointerEntered() { hovering = true; UpdateTooltip(); }
        private void HandlePointerExited() { hovering = false; UpdateTooltip(); }
        private void HandleLongPressStarted() { longPressing = true; UpdateTooltip(); }
        private void HandleLongPressEnded() { longPressing = false; UpdateTooltip(); }

        // 호버와 길게 누름 중 하나라도 유지되면 보인다(기획 82번 D2).
        private void UpdateTooltip()
        {
            if (tooltipRoot == null) return;
            var visible = hovering || longPressing;
            if (visible) RenderTooltip();
            tooltipRoot.SetActive(visible);
        }

        private void RenderTooltip()
        {
            tooltipText.text = $"최대량 {goldHoldings.CarryCapacity:N0} (개인 {goldHoldings.PersonalLimit:N0} / 적재 {goldHoldings.LoadedGold:N0} / 여유 공간 {goldHoldings.FreeSpaceGold:N0})";
        }
    }
}

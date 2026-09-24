using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Hub 재화 HUD - 현재값만 상시 표기하고 상한은 호버 시 툴팁으로 보여준다(기획 27번 §3.2).
    /// 다른 패널/팝업이 열려도 가려지지 않아야 하므로(27번 §3.5) depth 축·팝업 축 어느 숨김 로직도 따르지
    /// 않는다 - PopupExemptLayer(ContentRoot의 마지막 레이어)에 두는 것으로 인스톨러가 보장한다(설계 38번 §5).
    /// </summary>
    public class PlayerCurrencyHudController : MonoBehaviour, IPlayerCurrencyHudController
    {
        [SerializeField] private Sprite currencyIcon;

        private IPlayerCurrencyReader currencyReader;
        private TMP_Text amountText;
        private GameObject capacityTooltipRoot;
        private TMP_Text capacityTooltipText;

        public void RegisterCurrencyUI(SceneUIRoot sceneUIRoot, IPlayerCurrencyReader currencyReader)
        {
            if (!sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.CurrencyPanelRoot, out var panelRoot)
                || !sceneUIRoot.TryGetElement<TMP_Text>(HubUIElementIds.CurrencyAmountText, out amountText))
            {
                Debug.LogWarning($"Hub UI에서 재화 HUD 요소를 찾을 수 없다. {nameof(UIElementMarker)}가 부착되어 있는지 확인하라.");
                return;
            }

            this.currencyReader = currencyReader;

            // 재화 지갑이 아직 등록되지 않았으면(인스톨러 미실행 등) HUD 전체를 숨긴다 - 값 없이 "0"을
            // 보여주는 건 오해를 부른다(CLAUDE.md "값이 없으면 창작하지 말 것").
            if (currencyReader == null)
            {
                panelRoot.gameObject.SetActive(false);
                return;
            }

            if (sceneUIRoot.TryGetElement<Image>(HubUIElementIds.CurrencyIcon, out var icon) && currencyIcon != null)
            {
                icon.sprite = currencyIcon;
            }

            RefreshAmount(currencyReader.CurrentAmount);
            currencyReader.OnAmountChanged += RefreshAmount;

            if (sceneUIRoot.TryGetElement<RectTransform>(HubUIElementIds.CurrencyCapacityTooltip, out var tooltipRoot)
                && sceneUIRoot.TryGetElement<TMP_Text>(HubUIElementIds.CurrencyCapacityTooltipText, out capacityTooltipText)
                && sceneUIRoot.TryGetElement<PointerHoverRelay>(HubUIElementIds.CurrencyAmountText, out var hoverRelay))
            {
                capacityTooltipRoot = tooltipRoot.gameObject;
                capacityTooltipRoot.SetActive(false);
                hoverRelay.PointerEntered += ShowCapacityTooltip;
                hoverRelay.PointerExited += HideCapacityTooltip;
            }
        }

        private void RefreshAmount(int amount) => amountText.text = amount.ToString("N0");

        private void ShowCapacityTooltip()
        {
            capacityTooltipText.text = $"상한 {currencyReader.Capacity:N0}";
            capacityTooltipRoot.SetActive(true);
        }

        private void HideCapacityTooltip() => capacityTooltipRoot.SetActive(false);
    }
}

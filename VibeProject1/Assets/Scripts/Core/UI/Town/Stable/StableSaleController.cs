using System;
using TMPro;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 마구간 좌측 개체 정보 화면의 매각 부분(기획 94 4-18~4-21, Docs/설계/103번 §4.6 — 2026-10-26 사용자 지시로 우측 정보 영역에서 좌측으로 이동).
    /// 개체 정보 화면(StableOwnedSection)이 수리와 나란히 보여 주고, 이 협력 객체는 매각가·매각 사유·매각 확인만 맡는다 - 수리 흐름에 매각을 섞지 않는다(SRP).
    /// 우측 구매 영역과 상태를 나누지 않으므로 모드 전환이 없다.
    /// 화물 판정은 무역품 인벤토리 상태에 달려 있어 개체 정보를 보는 동안만 그 변경을 구독한다.
    /// </summary>
    public sealed class StableSaleController : IDisposable
    {
        private const string MissingText = PlaceholderText.Missing;
        private const string ConfirmText = "매각";
        private const string CancelText = "취소";

        private readonly TMP_Text priceLabel;
        private readonly ITooltipHandle sellTooltip; // 매각 버튼 비활성 사유 툴팁(Docs/Refactor/2026-10-26_공통.md D-1) - 요소가 없으면 null
        private readonly Button sellButton;
        private readonly ConfirmDialogView dialog;
        private readonly ICaravanAssetSaleService sale;   // 선택적 - 없으면 매각 버튼 비활성 + 매각가 "값 없음" + 사유 툴팁
        private readonly IOwnedCaravanAssetReader owned;
        private readonly IInventoryReader cargo;          // 선택적 - 개체 정보를 보는 동안 화물 변경 구독

        private string instanceId;
        private bool subscribed;

        public StableSaleController(TMP_Text priceLabel, ITooltipHandle sellTooltip, Button sellButton, ConfirmDialogView dialog, ICaravanAssetSaleService sale, IOwnedCaravanAssetReader owned, IInventoryReader cargo)
        {
            this.priceLabel = priceLabel;
            this.sellTooltip = sellTooltip;
            this.sellButton = sellButton;
            this.dialog = dialog;
            this.sale = sale;
            this.owned = owned;
            this.cargo = cargo;

            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(RequestSell);
            dialog.Hide();
        }

        /// <summary>개체 정보 화면을 그릴 때마다 부른다. 같은 개체면 구독은 유지한다.</summary>
        public void Show(string selectedInstanceId)
        {
            instanceId = selectedInstanceId;
            if (!subscribed && cargo != null)
            {
                cargo.OnChanged += Render;
                subscribed = true;
            }
            Render();
        }

        /// <summary>목록으로 돌아가거나 마구간을 닫을 때.</summary>
        public void Hide()
        {
            instanceId = null;
            Unsubscribe();
            if (dialog != null) dialog.Hide();
        }

        public void Dispose()
        {
            Unsubscribe();
            sellTooltip?.Dispose();
        }

        private void Render()
        {
            if (instanceId == null || priceLabel == null) return; // Hub 언로드 뒤

            if (!owned.TryGetOwned(instanceId, out _))
            {
                priceLabel.text = string.Empty;
                sellTooltip?.SetText(null);
                sellButton.interactable = false;
                return;
            }

            if (sale == null)
            {
                priceLabel.text = "매각가 " + MissingText;
                sellTooltip?.SetText(FacilityBlockReasons.SaleUnavailable);
                sellButton.interactable = false;
                return;
            }

            priceLabel.text = $"매각가 {sale.SalePriceOf(instanceId):N0}";
            var check = sale.Evaluate(instanceId);
            sellButton.interactable = check == CaravanAssetSaleCheck.Available;
            sellTooltip?.SetText(FacilityBlockReasons.For(check));
        }

        // 되돌릴 수 없는 조작이라 확인 창을 거친다(기획 94 4-21).
        private void RequestSell()
        {
            if (sale == null || instanceId == null || !owned.TryGetOwned(instanceId, out var asset)) return;

            var id = instanceId;
            dialog.Show($"{OwnedCaravanAssetNames.Format(asset)}을 {sale.SalePriceOf(id):N0}골드에 매각합니다", ConfirmText, CancelText, () =>
            {
                sale.TrySell(id); // 성공하면 보유 변경 이벤트로 개체 정보 화면이 목록으로 돌아간다. Placeholder는 무동작이라 다시 그려 상태를 맞춘다.
                Render();
            }, null);
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            cargo.OnChanged -= Render;
            subscribed = false;
        }
    }
}

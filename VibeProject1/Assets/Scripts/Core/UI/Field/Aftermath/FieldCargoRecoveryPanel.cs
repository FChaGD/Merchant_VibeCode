using System;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// ② 회수 적재 패널(설계 79번 §7). 무역품 구매 화면의 고정 패널과 같은 편집 본문(InventoryArrangementController)을 상단 물류품
    /// 사양(InventoryPopupSpecs.TradeGoods - 회전·임시보관·마차 스테퍼) 그대로 쓴다. 닫기 버튼·창 드래그 없이 [완료]만 둔다(기획 78번 §4-11).
    /// 패널은 완료 콜백만 부르고 닫기는 단계가 UIManager.Close로 한다. plain C#이며 FieldUIWiring이 Field 로드마다 새로 만든다 -
    /// 저장소(Bootstrap 상주) 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// 패널 루트는 편집 본문 루트와 같은 오브젝트다(FieldCargoRecoveryUIElementIds 요약 주석 참고).
    /// </summary>
    public sealed class FieldCargoRecoveryPanel : IUIPanel, IDisposable
    {
        private const string DiscardWarningFormat = "임시보관에 남은 물품 {0}개는 버려집니다. 그래도 넘어가시겠습니까?";
        private const string DiscardConfirmText = "넘어가기";
        private const string DiscardCancelText = "돌아가기";

        private readonly InventoryArrangementElements elements;
        private readonly Button doneButton;
        private readonly ConfirmDialogView dialog;
        private readonly ITradeGoodsCargoSettlement settlement;
        private readonly InventoryArrangementController inventoryController;

        private Action onCompleted;

        public string PanelId => UIPanelIds.CargoRecovery;

        public FieldCargoRecoveryPanel(InventoryArrangementElements elements, Button doneButton, ConfirmDialogView dialog, ITradeGoodsInventoryRepository inventory, ITradeGoodsCargoSettlement settlement)
        {
            this.elements = elements;
            this.doneButton = doneButton;
            this.dialog = dialog;
            this.settlement = settlement;

            // 같은 인벤토리를 다른 화면과 다른 규칙으로 다루지 않게 상단 물류품 팝업 스펙을 따른다.
            var spec = InventoryPopupSpecs.TradeGoods;
            inventoryController = new InventoryArrangementController(elements, inventory, inventory, spec.AllowsRotation, spec.HasStaging, spec.HasSections);

            doneButton.onClick.RemoveAllListeners();
            doneButton.onClick.AddListener(HandleDone);

            dialog.Hide();
            elements.Root.gameObject.SetActive(false);
        }

        /// <summary>[완료] 처리 후 부를 콜백 - 단계가 열기 전에 지정한다.</summary>
        public void SetCompletion(Action onCompleted)
        {
            this.onCompleted = onCompleted;
        }

        public void Open()
        {
            dialog.Hide();
            elements.Root.gameObject.SetActive(true);
            inventoryController.Show();
        }

        // Close()는 표시/숨김만 한다. [완료] 후 닫기는 이 메서드를 직접 부르지 않고 단계(CargoRecoveryStep)가 UIManager.Close(PanelId)로 위임한다.
        public void Close()
        {
            dialog.Hide();
            inventoryController.Hide();
            elements.Root.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            doneButton.onClick.RemoveListener(HandleDone);
            inventoryController.Dispose();
        }

        // 임시보관이 비었으면 바로 완료, 남았으면 버림 확인 후 완료(기획 78번 §4-13). 자동 배치는 하지 않는다(§4-14).
        private void HandleDone()
        {
            var remaining = settlement.StagedItems.Count;
            if (remaining == 0)
            {
                onCompleted?.Invoke();
                return;
            }

            dialog.Show(string.Format(DiscardWarningFormat, remaining), DiscardConfirmText, DiscardCancelText,
                onConfirm: () =>
                {
                    settlement.DiscardStaged();
                    onCompleted?.Invoke();
                },
                onCancel: null);
        }
    }
}

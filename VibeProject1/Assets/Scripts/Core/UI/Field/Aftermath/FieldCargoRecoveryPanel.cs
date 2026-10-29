using System;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// ② 회수 적재 패널(설계 79번 §7). 무역품 구매 화면의 고정 패널과 같은 편집 본문(InventoryArrangementController)을 상단 물류품
    /// 사양(InventoryPopupSpecs.TradeGoods - 회전·임시보관·마차 스테퍼) 그대로 쓴다. 닫기 버튼·창 드래그 없이 [완료]만 둔다(기획 78번 §4-11).
    /// 패널은 완료 콜백만 부르고 닫기는 단계가 UIManager.Close로 한다. plain C#이며 FieldUIWiring·HubUIWiring이 씬 로드마다 새로 만든다 -
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
        private readonly IQuestCargoWarnings questWarnings; // 선택적 - 넘어가기로 버려질 의뢰 화물 경고(설계 107번 §8.4)

        private Action onCompleted;

        public string PanelId => UIPanelIds.CargoRecovery;

        /// <summary>
        /// 씬의 회수 적재 화면 요소로 패널을 만들어 모달 묶음에 등록한다 - Field(전투 후·이벤트)·Hub(휴식 이벤트) 배선이 같이 쓴다(설계 109 §6.2).
        /// 요소가 없으면 null(경고 문구는 씬마다 달라 호출자가 낸다). 저장소 확인은 호출자 몫이다.
        /// </summary>
        public static FieldCargoRecoveryPanel TryCreate(SceneUIRoot sceneUIRoot, IPanelRegistrar panelRegistrar, ITradeGoodsInventoryRepository inventory,
            ITradeGoodsCargoSettlement settlement, IInputManager input, IGoldItemInfo goldInfo, IQuestCargoWarnings questWarnings)
        {
            var spec = InventoryPopupSpecs.TradeGoods;
            var ok = InventoryArrangementElements.TryBind(sceneUIRoot, FieldCargoRecoveryUIElementIds.InventoryPrefix, spec.HasStaging, spec.HasSections, out var elements)
                & InventoryArrangementElements.TryGet(sceneUIRoot, FieldCargoRecoveryUIElementIds.DoneButton, out Button doneButton)
                & InventoryArrangementElements.TryGet(sceneUIRoot, FieldCargoRecoveryUIElementIds.ConfirmDialog, out ConfirmDialogView dialog);
            if (!ok) return null;

            var panel = new FieldCargoRecoveryPanel(elements, doneButton, dialog, inventory, settlement, input, goldInfo, questWarnings);
            panelRegistrar.RegisterPopupPanel(panel);
            return panel;
        }

        public FieldCargoRecoveryPanel(InventoryArrangementElements elements, Button doneButton, ConfirmDialogView dialog, ITradeGoodsInventoryRepository inventory, ITradeGoodsCargoSettlement settlement, IInputManager input, IGoldItemInfo goldInfo,
            IQuestCargoWarnings questWarnings = null)
        {
            this.elements = elements;
            this.doneButton = doneButton;
            this.dialog = dialog;
            this.settlement = settlement;
            this.questWarnings = questWarnings;

            // 같은 인벤토리를 다른 화면과 다른 규칙으로 다루지 않게 상단 물류품 팝업 스펙을 따른다.
            var spec = InventoryPopupSpecs.TradeGoods;
            inventoryController = new InventoryArrangementController(elements, input, goldInfo, inventory, inventory, spec.AllowsRotation, spec.HasStaging, spec.HasSections, questWarnings);

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

        /// <summary>
        /// 결과 정리 단계 밖에서 여는 공용 절차 - 이벤트 무역품·퀘스트 보상 정리가 쓴다(설계 89 §6, 107번 §8.3). [완료] 시 UIManager로 닫은 뒤 onDone.
        /// </summary>
        public void ShowVia(IPanelNavigator uiManager, Action onDone)
        {
            SetCompletion(() =>
            {
                uiManager.Close(PanelId);
                onDone?.Invoke();
            });
            uiManager.Open(PanelId);
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

            var message = string.Format(DiscardWarningFormat, remaining);
            if (questWarnings != null && InventoryDiscardWarning.QuestCargoLine(questWarnings.Describe(InventoryDiscardWarning.ItemIds(settlement.StagedItems))) is { } questLine)
            {
                message += "\n" + questLine;
            }
            dialog.Show(message, DiscardConfirmText, DiscardCancelText,
                onConfirm: () =>
                {
                    settlement.DiscardStaged();
                    onCompleted?.Invoke();
                },
                onCancel: null);
        }
    }
}

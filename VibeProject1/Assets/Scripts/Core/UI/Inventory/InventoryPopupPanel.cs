using System;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 1개의 조율(Docs/설계/40번 §5). 팝업 4종이 이 클래스의 인스턴스가 되는데, 같은 GameObject에
    /// 같은 타입 컴포넌트 4개를 GetComponent로 구분할 수 없어 MonoBehaviour가 아닌 plain C#으로 둔다
    /// (TownCategoryPanel과 같은 선택). Hub 로드마다 새로 만들어지므로 영속 상태(창 위치)는 외부
    /// PopupWindowPositionStore에 둔다. 그리드 편집은 InventoryArrangementController가 맡고(설계 50번 §6.4), 이 클래스는
    /// 떠 있는 창(창 드래그·닫기 버튼·창 위치)만 더한다. 교체 시 반드시 Dispose한다(편집 컨트롤러의 저장소 구독 해제).
    /// 회전·임시 보관은 InventoryPopupSpec으로 켜고 끈다(설계 42번 §4.2). 임시 보관이 꺼진 팝업도 IPanelCloseGuard는
    /// 그대로 구현한다 - 저장소의 임시 보관이 항상 비어 있어 TryPrepareClose가 즉시 true라 분기가 필요 없다.
    /// </summary>
    public sealed class InventoryPopupPanel : IUIPanel, IPanelCloseGuard, IDisposable
    {
        private const string CloseBlockedMessage = "임시 보관 물품을 넣을 공간이 부족해 닫을 수 없습니다.";

        private readonly InventoryPopupElements elements;
        private readonly PopupWindowPositionStore windowPositions;
        private readonly InventoryArrangementController arrangementController;

        public string PanelId { get; }

        // 여러 팝업 사이의 표시 순서를 정하는 PopupFocusOnPress가 창 루트를 받는다(Docs/설계/46번 §3).
        public UnityEngine.RectTransform WindowRoot => elements.Arrangement.Root;

        public InventoryPopupPanel(InventoryPopupSpec spec, InventoryPopupElements elements, IInventoryReader reader, IInventoryArrangement arrangement, IUIManager uiManager, PopupWindowPositionStore windowPositions)
        {
            PanelId = spec.PopupId;
            this.elements = elements;
            this.windowPositions = windowPositions;
            arrangementController = new InventoryArrangementController(elements.Arrangement, reader, arrangement, spec.AllowsRotation, spec.HasStaging, spec.HasSections);

            // 닫기 버튼도 상시 호출 버튼과 같은 토글 경로를 탄다 - 닫기 차단(IPanelCloseGuard)이 코디네이터 한 곳에서만 확인된다.
            elements.CloseButton.onClick.RemoveAllListeners();
            elements.CloseButton.onClick.AddListener(() => uiManager.ToggleInventoryPopup(PanelId));
            elements.TitleBar.Moved += position => windowPositions.Set(PanelId, position);

            WindowRoot.gameObject.SetActive(false);
        }

        public void Open()
        {
            WindowRoot.gameObject.SetActive(true);
            WindowRoot.SetAsLastSibling();
            if (windowPositions.TryGet(PanelId, out var position)) elements.TitleBar.SetPosition(position);
            arrangementController.Show();
        }

        // Close()는 표시/숨김만 한다. 닫기 버튼 등 패널 내부에서 닫을 때는 이 메서드를 직접 부르지 않고
        // UIManager.ToggleInventoryPopup(PanelId)로 코디네이터에 위임한다(닫기 차단 확인 경로 유지).
        public void Close()
        {
            arrangementController.Hide();
            WindowRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 임시 보관 아이템을 그리드 빈칸에 자동 배치한다. 전부 넣지 못하면 안내 문구를 띄우고 닫기를 막는다
        /// (Docs/기획/39번 §3.5) - 임시 보관이 추가 수납 공간이 되지 않고, 버리기 없음 원칙도 지켜진다.
        /// </summary>
        public bool TryPrepareClose()
        {
            if (arrangementController.TryFlushStaging()) return true;

            arrangementController.ShowMessage(CloseBlockedMessage);
            return false;
        }

        public void Dispose() => arrangementController.Dispose();
    }
}

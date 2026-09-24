namespace Game.Core
{
    public interface IUIManager : IUIVisibilitySignal
    {
        /// <summary>
        /// 패널 Id가 등록된 종류(depth 패널/모달 팝업)의 채널에서 연다. 모달 팝업을 열어도 depth 패널은
        /// 닫히지 않는다(Docs/설계/38번 §4.2).
        /// </summary>
        void Open(string panelId);
        void Close(string panelId);

        // PanelNavigationStack의 단일 활성 패널 정책과 분리된 경로 - 인벤토리 팝업 4종은 동시 중첩
        // 오픈이 가능해야 해서(Docs/기획/31번 §3.5) Open/Close가 아니라 IInventoryPopupCoordinator로
        // 위임하는 이 메서드만 쓴다(Docs/설계/32번 §5). 비모달이라 IUIVisibilitySignal 신호에도 참여하지
        // 않는다(Docs/설계/38번 §3).
        void ToggleInventoryPopup(string popupId);
    }
}

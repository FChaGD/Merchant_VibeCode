namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업 4종의 개별 열림 상태만 다룬다 - PanelNavigationStack과 달리 "하나를 열면 다른
    /// 걸 숨긴다" 정책이 없다(Docs/기획/31번 §3.5, 동시 중첩 오픈). UIManager와 같은 GameObject에
    /// 부착하는 형제 컴포넌트로, 전역 DI 대상이 아니다(HubUIController와 같은 성격).
    /// </summary>
    public interface IInventoryPopupCoordinator
    {
        void RegisterPopup(IUIPanel popup);
        void Toggle(string popupId);
        bool IsOpen(string popupId);

        // 씬 로드 시 호출 - 이전 씬의 팝업 시각 요소는 이미 파괴됐으므로 등록·열림 기록을 모두 지운다.
        // 지우지 않으면 Hub 재방문 시 열림 기록이 남아 첫 토글이 "닫기"로 처리된다(Docs/설계/40번 §2).
        void Reset();
    }
}

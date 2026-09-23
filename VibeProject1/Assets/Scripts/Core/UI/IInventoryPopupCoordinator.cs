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
    }
}

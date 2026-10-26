namespace Game.Core
{
    /// <summary>
    /// 비모달 인벤토리 팝업 등록 권한(Docs/Refactor/2026-10-26_공통.md B-3). 인벤토리 팝업이 없는 씬의 패널 호스트(배틀 테스트)가 빈 구현으로
    /// 채우지 않도록 IPanelRegistrar에서 떼어 냈다.
    /// </summary>
    public interface IInventoryPopupRegistrar
    {
        /// <summary>동시 중첩 오픈, IInventoryPopupToggle.ToggleInventoryPopup으로만 연다(Docs/설계/40번 §6).</summary>
        void RegisterInventoryPopup(IUIPanel popup);
    }
}

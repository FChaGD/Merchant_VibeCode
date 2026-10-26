namespace Game.Core
{
    /// <summary>
    /// 비모달 인벤토리 팝업 조작(Docs/Refactor/2026-10-26_공통.md B-3). 단일 활성 패널 정책(PanelNavigationStack)과 분리된 경로 - 팝업 4종은
    /// 동시에 열 수 있어야 해서(기획 31번 §3.5) IInventoryPopupCoordinator로 위임하고, 두 축 신호에도 참여하지 않는다(설계 38번 §3).
    /// </summary>
    public interface IInventoryPopupToggle
    {
        void ToggleInventoryPopup(string popupId);
        // 열린 인벤토리 팝업을 닫기 차단(임시 보관 비우기) 없이 모두 숨긴다 - 시설 화면 진입용(Docs/설계/50번 §6.2).
        // 임시 보관 상태는 저장소에 있어 시설 화면의 고정 패널이 그대로 이어받으므로 비우지 않아도 된다.
        void CloseAllInventoryPopups();
    }
}

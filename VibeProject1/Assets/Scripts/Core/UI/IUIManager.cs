namespace Game.Core
{
    /// <summary>
    /// UIManager가 내는 계약 전체(열기/닫기 + 인벤토리 팝업 + 두 축 신호). 셋이 다 필요한 배선·컨트롤러만 이 이름을 쓰고,
    /// 나머지 소비자는 실제로 쓰는 계약만 받는다(ISP, Docs/Refactor/2026-10-26_공통.md B-3).
    /// </summary>
    public interface IUIManager : IPanelNavigator, IInventoryPopupToggle, IUIVisibilitySignal
    {
    }
}

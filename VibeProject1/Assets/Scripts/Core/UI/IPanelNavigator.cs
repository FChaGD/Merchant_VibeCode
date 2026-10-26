namespace Game.Core
{
    /// <summary>
    /// 패널 열기/닫기만 노출한다(ISP, Docs/Refactor/2026-10-26_공통.md B-3). 패널·조율자 대부분은 이것만 쓰므로 인벤토리 토글·두 축 신호까지
    /// 들어 있는 IUIManager를 받지 않는다. 패널 Id가 등록된 종류(depth·시설 화면·모달)의 채널에서 열고 닫는다 - 종류가 달라도 다른 종류의
    /// 패널은 닫히지 않는다(설계 38번 §4.2).
    /// </summary>
    public interface IPanelNavigator
    {
        void Open(string panelId);
        void Close(string panelId);
    }
}

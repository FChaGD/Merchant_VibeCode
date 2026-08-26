namespace Game.Core
{
    /// <summary>
    /// IUIPanel을 UIManager의 panelsById에 등록하는 권한만 노출한다(ISP) - Open/Close만 필요한 일반
    /// 소비자는 IUIManager만 의존하고, 씬별 UI 배선 담당(IContentSceneUIWiring 구현체)만 이 인터페이스로
    /// 패널을 등록한다.
    /// </summary>
    public interface IPanelRegistrar
    {
        void RegisterPanel(IUIPanel panel);
    }
}

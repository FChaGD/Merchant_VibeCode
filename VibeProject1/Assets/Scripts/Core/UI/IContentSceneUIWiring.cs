namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 씬(Hub/Field 등)이 로드될 때마다 그 씬의 UI 요소를 찾아 필요한 의존성과 연결하는 배선 책임.
    /// UIManager는 씬 이름으로 어떤 구현체를 호출할지만 판단하고(SceneId로 매칭), 각 구현체가 실제로
    /// 어떤 의존성을 resolve하고 어떤 패널/컨트롤러에 무엇을 넘기는지는 전혀 모른다 - 새 콘텐츠 씬이
    /// 생겨도 UIManager를 고치지 않고 이 인터페이스의 새 구현체를 추가/등록하기만 하면 된다.
    /// </summary>
    public interface IContentSceneUIWiring
    {
        ContentSceneId SceneId { get; }

        void Wire(IDependencyRegistrar registrar, IUIManager uiManager, IPanelRegistrar panelRegistrar);
    }
}

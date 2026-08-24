namespace Game.Core
{
    public interface IFormationPanel : IUIPanel
    {
        /// <summary>
        /// 주어진 콘텐츠 씬(sceneName)의 SceneUIRoot에서 배치 UI 요소를 찾아 바인딩하고, 상행 관리 데이터
        /// 의존성을 연결한다. Hub/Field 등 배치 UI를 제공하는 콘텐츠 씬이 로드될 때마다 다시 호출되어
        /// 그 씬의 화면 요소로 재바인딩한다 - 이전 콘텐츠 씬이 언로드되며 파괴된 요소를 계속 들고 있지
        /// 않게 하기 위함이다.
        /// </summary>
        void RegisterFormationUI(ICaravanRosterProvider rosterProvider, IFormationRepository repository, IUIManager uiManager, string sceneName);
    }
}

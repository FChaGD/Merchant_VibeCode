namespace Game.Core
{
    public interface IFormationPanel : IUIPanel
    {
        /// <summary>
        /// Hub 씬의 SceneUIRoot에서 배치 UI 요소를 찾아 바인딩하고, 상행 관리 데이터 의존성을 연결한다.
        /// </summary>
        void RegisterFormationUI(ICaravanRosterProvider rosterProvider, IFormationRepository repository, IUIManager uiManager);
    }
}

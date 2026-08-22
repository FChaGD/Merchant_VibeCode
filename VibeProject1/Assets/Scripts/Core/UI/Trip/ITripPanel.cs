namespace Game.Core
{
    public interface ITripPanel : IUIPanel
    {
        /// <summary>
        /// Hub 씬의 SceneUIRoot에서 상행 준비 UI 요소를 찾아 바인딩하고, 필요한 의존성을 연결한다.
        /// </summary>
        void RegisterTripUI(IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider);
    }
}

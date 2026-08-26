namespace Game.Core
{
    public interface ITripPanel : IUIPanel
    {
        /// <summary>
        /// Hub 씬의 SceneUIRoot에서 상행 준비 UI 요소를 찾아 바인딩하고, 필요한 의존성을 연결한다.
        /// sceneRevealSignal은 "상행 시작" 버튼을 씬 전환 커튼이 완전히 걷힐 때까지 비활성화하는 데 쓴다.
        /// </summary>
        void RegisterTripUI(IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider, ISceneRevealSignal sceneRevealSignal);
    }
}

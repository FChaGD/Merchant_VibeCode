namespace Game.Core
{
    public interface IHubUIController
    {
        /// <summary>
        /// Hub 씬의 UI(SceneUIRoot)를 찾아 버튼 클릭 동작과 배경 이미지를 연결한다.
        /// </summary>
        void RegisterHubUI(IUIManager uiManager);
    }
}

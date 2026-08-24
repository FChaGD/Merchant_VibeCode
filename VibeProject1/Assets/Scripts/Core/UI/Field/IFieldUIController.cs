namespace Game.Core
{
    public interface IFieldUIController
    {
        /// <summary>
        /// Field 씬의 SceneUIRoot에서 이동 뷰 요소(진행 게이지, 배경, 정비창 재호출 버튼)를 찾아
        /// 바인딩하고, 진행 상태 구독과 상행 시작(Begin)을 처리한다.
        /// </summary>
        void RegisterFieldUI(IUIManager uiManager, ISessionState sessionState);
    }
}

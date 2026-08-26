using UnityEngine;

namespace Game.Core
{
    public interface IHubUIController
    {
        /// <summary>
        /// Hub 씬의 UI(SceneUIRoot)를 찾아 버튼 클릭 동작과 배경 이미지를 연결한다. sceneRevealSignal은
        /// "상행 준비"/"배치" 버튼을 씬 전환 커튼이 완전히 걷힐 때까지 비활성화하는 데 쓴다.
        /// </summary>
        void RegisterHubUI(IUIManager uiManager, ISceneRevealSignal sceneRevealSignal);

        /// <summary>Hub↔Field 씬 전환 연출이 슬라이드시킬 대상. RegisterHubUI 이후에만 유효하다.</summary>
        RectTransform ContentRoot { get; }
    }
}

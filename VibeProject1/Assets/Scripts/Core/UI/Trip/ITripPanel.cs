using System.Collections.Generic;

namespace Game.Core
{
    public interface ITripPanel : IUIPanel
    {
        /// <summary>
        /// Hub 씬의 SceneUIRoot에서 상행 준비 UI 요소를 찾아 바인딩하고, 필요한 의존성을 연결한다.
        /// sceneRevealSignal은 "상행 시작" 버튼을 씬 전환 커튼이 완전히 걷힐 때까지 비활성화하는 데 쓴다.
        /// inventoryStagingReaders는 임시 보관 영역에 아이템이 남아 있는 동안 "상행 시작"을 막는 데 쓴다
        /// (Docs/설계/40번 §5.5) - 인벤토리 카테고리 공통 조건이라 목록으로 받는다.
        /// </summary>
        void RegisterTripUI(SceneUIRoot sceneUIRoot, IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider, ISceneRevealSignal sceneRevealSignal, ITripCurrentLocationReader currentLocationReader, ITripDestinationAssigner destinationAssigner, IReadOnlyList<IInventoryStagingReader> inventoryStagingReaders);
    }
}

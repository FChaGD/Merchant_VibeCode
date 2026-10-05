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
        /// worldMap·routeReader는 지역 지도 표시와 도착지 도달 판정(Docs/설계/69번 §5).
        /// rosterProvider는 출발 조건(마차 대열 연결)을 판정할 때 배치의 유닛 Id를 마차·시설로 해석하는 데 쓴다(Docs/설계/79번 §9.1).
        /// </summary>
        void RegisterTripUI(SceneUIRoot sceneUIRoot, IUIManager uiManager, IGameManager gameManager, IFormationReader formationReader, ITripInfoProvider tripInfoProvider, ISceneRevealSignal sceneRevealSignal, ITripCurrentLocationReader currentLocationReader, ITripDestinationAssigner destinationAssigner, IReadOnlyList<IInventoryStagingReader> inventoryStagingReaders, IWorldMapReader worldMap, ITripRouteReader routeReader, ITripDeparture tripDeparture, ICaravanRosterProvider rosterProvider);

#if UNITY_EDITOR
        /// <summary>지도 디버그 편집(배치·도로·관문·지역 추가/삭제·저장, 설계 69번 §6)을 붙인다. RegisterTripUI 뒤에 부른다.</summary>
        void RegisterDebugMapEditor(IWorldMapEditor editor);
#endif
    }
}

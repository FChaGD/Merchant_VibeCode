using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Field 배선 전용 계약(등록·씬 전환 대상). 상단 버튼 활성은 IFieldTopButtons, 상행 종료는 ITripFinisher(FieldTripLifecycle)로 나눴다 -
    /// 결과 정리·이벤트 소비자가 등록 메서드까지 받지 않게 한다(Docs/Refactor/2026-10-26_씬전용.md SS-2).
    /// </summary>
    public interface IFieldUIController
    {
        /// <summary>
        /// Field 씬의 SceneUIRoot에서 이동 뷰/전투 뷰 요소(진행 게이지, 배경, 정비창 재호출 버튼,
        /// 인카운터 경고창, 전투 뷰, 결과 팝업)를 찾아 바인딩하고, 진행 상태 구독·인카운터→전투 전환·
        /// 결과 처리 흐름 연결을 처리한다. 씬 전환 커튼이 완전히 걷힐 때까지는 정비창 버튼을 막고
        /// 상행 진행(Begin)도 시작하지 않는다(사용자 확정) - sceneRevealSignal이 걷힘을 알려준다.
        /// 전투 정산·회수 적재·대열 정리 의존성(aftermathApplier, cargoSettlement, repairMode, cargoRecoveryPanel)은 전부 선택적이다 -
        /// null이면 정산 없이 진행하거나 해당 결과 정리 단계를 건너뛴다(설계 79번 §6). 상행 시작·종료·도착의 상태 변경은 lifecycle이 한다.
        /// dayRout이 null이면 궤주 날짜 정산 없이 진행한다(설계 93 §4.4).
        /// legDifficulty가 null이면 구간 라벨은 계획 등급명으로 둔다(설계 89 §4.3).
        /// </summary>
        void RegisterFieldUI(SceneUIRoot sceneUIRoot, IPanelNavigator uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IBattleSimulationEvents battleSimulationEvents, IGameManager gameManager, ISceneRevealSignal sceneRevealSignal, FieldTripLifecycle lifecycle, ITripCurrentLocationRepository currentLocationRepository, IFieldFormationActivityRepository fieldActivityRepository, ITripItinerary itinerary, IWorldMapReader worldMap, IBattleAftermathApplier aftermathApplier, ITradeGoodsCargoSettlement cargoSettlement, IFormationRepairMode repairMode, FieldCargoRecoveryPanel cargoRecoveryPanel, ITripDayRout dayRout, FieldTripSignals tripSignals, ICurrentLegDifficulty legDifficulty);

        /// <summary>Hub↔Field 씬 전환 연출이 슬라이드시킬 대상. RegisterFieldUI 이후에만 유효하다.</summary>
        RectTransform MovementViewRoot { get; }

        /// <summary>최종 도착 시 퀘스트 보상 정리 화면을 도착 팝업보다 먼저 연다(설계 107번 §8.3). null이면 바로 도착 팝업.</summary>
        void SetQuestRewardCleanup(FieldQuestRewardCleanup cleanup);
    }
}

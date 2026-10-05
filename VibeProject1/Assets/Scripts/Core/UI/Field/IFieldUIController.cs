using UnityEngine;

namespace Game.Core
{
    public interface IFieldUIController
    {
        /// <summary>
        /// Field 씬의 SceneUIRoot에서 이동 뷰/전투 뷰 요소(진행 게이지, 배경, 정비창 재호출 버튼,
        /// 인카운터 경고창, 전투 뷰, 결과 팝업)를 찾아 바인딩하고, 진행 상태 구독·인카운터→전투 전환·
        /// 결과 처리 흐름 연결을 처리한다. 씬 전환 커튼이 완전히 걷힐 때까지는 정비창 버튼을 막고
        /// 상행 진행(Begin)도 시작하지 않는다(사용자 확정) - sceneRevealSignal이 걷힘을 알려준다.
        /// 전투 정산·회수 적재·대열 정리 의존성(aftermathApplier, cargoSettlement, repairMode, cargoRecoveryPanel)은 전부 선택적이다 -
        /// null이면 정산 없이 진행하거나 해당 결과 정리 단계를 건너뛴다(설계 79번 §6).
        /// </summary>
        void RegisterFieldUI(SceneUIRoot sceneUIRoot, IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IBattleSimulationEvents battleSimulationEvents, IGameManager gameManager, ISceneRevealSignal sceneRevealSignal, IUnitConditionRepository unitConditionRepository, ITripCurrentLocationRepository currentLocationRepository, ITripDestinationAssigner destinationAssigner, IFieldFormationActivityRepository fieldActivityRepository, ITripItinerary itinerary, IWorldMapReader worldMap, IBattleAftermathApplier aftermathApplier, ITradeGoodsCargoSettlement cargoSettlement, IFormationRepairMode repairMode, FieldCargoRecoveryPanel cargoRecoveryPanel);

        /// <summary>Hub↔Field 씬 전환 연출이 슬라이드시킬 대상. RegisterFieldUI 이후에만 유효하다.</summary>
        RectTransform MovementViewRoot { get; }

        /// <summary>
        /// 정비창/방향성 지시 재호출 버튼의 상호작용 가능 여부를 일괄 설정한다. 씬 전환 커튼(사용자
        /// 확정) 외에, 인카운터~전투 진행 중에도 눌리면 안 되므로(사용자 확정, 2026-09-07)
        /// FieldEncounterFlowCoordinator가 인카운터 발생 시 false, 이동 뷰 복귀 슬라이드 시작 직전에
        /// true로 되돌리는 데 쓴다.
        /// </summary>
        void SetTopLevelButtonsInteractable(bool interactable);
    }
}

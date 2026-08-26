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
        /// </summary>
        void RegisterFieldUI(IUIManager uiManager, ISessionState sessionState, IEncounterManager encounterManager, IBattleController battleController, IBattleResultSource battleResultSource, IDefeatConsequenceSource defeatConsequenceSource, IBattleSimulationEvents battleSimulationEvents, IGameManager gameManager, ISceneRevealSignal sceneRevealSignal);

        /// <summary>Hub↔Field 씬 전환 연출이 슬라이드시킬 대상. RegisterFieldUI 이후에만 유효하다.</summary>
        RectTransform MovementViewRoot { get; }
    }
}

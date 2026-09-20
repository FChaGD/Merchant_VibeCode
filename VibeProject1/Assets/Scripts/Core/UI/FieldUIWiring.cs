using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// UIManager 산하 컴포넌트. Field 씬이 로드될 때 필요한 UI 배선만 담당한다 - Battle 도메인
    /// 인터페이스(IBattleController 등)를 아는 곳을 UIManager에서 이 클래스로 옮겨, 공통 UI 프레임워크가
    /// 특정 씬의 구체 도메인을 몰라도 되게 한다(DIP, Docs/Refactor/2026-08-26-공통.md 3단계 수정안).
    /// </summary>
    public class FieldUIWiring : MonoBehaviour, IContentSceneUIWiring
    {
        public ContentSceneId SceneId => ContentSceneId.Field;

        public void Wire(IDependencyResolver registrar, IUIManager uiManager, IPanelRegistrar panelRegistrar)
        {
            // 이 씬의 SceneUIRoot를 여기서 한 번만 찾아 아래 3개 컴포넌트 전부에 넘긴다 - 예전엔
            // 각자 SceneManager.GetSceneByName부터 다시 조회했다(DRY, Docs/Refactor/2026-09-08_Hub.md
            // §3 수정 J).
            if (!SceneUIRootLocator.TryFind(SceneNames.Field, out var sceneUIRoot))
            {
                return;
            }

            var formationPanel = GetComponent<FieldFormationPanel>();
            if (formationPanel == null)
            {
                throw new InvalidOperationException($"{nameof(FieldUIWiring)}와 같은 GameObject에 {nameof(FieldFormationPanel)} 구현체가 없다.");
            }

            var fieldUIController = GetComponent<IFieldUIController>();
            if (fieldUIController == null)
            {
                throw new InvalidOperationException($"{nameof(FieldUIWiring)}와 같은 GameObject에 {nameof(IFieldUIController)} 구현체가 없다.");
            }

            var tacticsPanel = GetComponent<ITacticsPanel>();
            if (tacticsPanel == null)
            {
                throw new InvalidOperationException($"{nameof(FieldUIWiring)}와 같은 GameObject에 {nameof(ITacticsPanel)} 구현체가 없다.");
            }

            // 상행 관리 데이터 시스템이 아직 없어 선택적으로 조회한다 - 등록되면 자동으로 연결된다.
            registrar.TryResolve<ICaravanRosterProvider>(out var caravanRosterProvider);
            registrar.TryResolve<IFormationRepository>(out var formationRepository);
            registrar.TryResolve<IUnitConditionRepository>(out var unitConditionRepository);
            registrar.TryResolve<ITacticsRepository>(out var tacticsRepository);
            registrar.TryResolve<ITripCurrentLocationRepository>(out var currentLocationRepository);
            registrar.TryResolve<ITripDestinationAssigner>(out var destinationAssigner);
            registrar.TryResolve<IFieldFormationActivityRepository>(out var fieldActivityRepository);

            var sessionState = registrar.Resolve<ISessionState>();
            var encounterManager = registrar.Resolve<IEncounterManager>();
            var battleController = registrar.Resolve<IBattleController>();
            var battleResultSource = registrar.Resolve<IBattleResultSource>();
            var defeatConsequenceSource = registrar.Resolve<IDefeatConsequenceSource>();
            var battleSimulationEvents = registrar.Resolve<IBattleSimulationEvents>();
            var gameManager = registrar.Resolve<IGameManager>();
            var sceneRevealSignal = registrar.Resolve<ISceneRevealSignal>();

            // Formation UI(정비창)는 Hub 전용이 아니다 - Field도 자신만의 화면 요소를 갖고 있어
            // (FieldUIInstaller 참고) 여기서도 다시 등록해야 "정비창 재호출"이 동작한다.
            formationPanel.RegisterFieldFormationUI(sceneUIRoot, caravanRosterProvider, formationRepository, unitConditionRepository, fieldActivityRepository, uiManager);
            panelRegistrar.RegisterPanel(formationPanel);

            tacticsPanel.RegisterTacticsUI(sceneUIRoot, tacticsRepository, uiManager);
            panelRegistrar.RegisterPanel(tacticsPanel);

            fieldUIController.RegisterFieldUI(sceneUIRoot, uiManager, sessionState, encounterManager, battleController, battleResultSource, defeatConsequenceSource, battleSimulationEvents, gameManager, sceneRevealSignal, unitConditionRepository, currentLocationRepository, destinationAssigner, fieldActivityRepository);

            // Hub↔Field 씬 전환 연출(SceneTransitionEffectController)이 다음 전환 때 슬라이드시킬
            // 대상을 등록한다 - Field는 전용 요소를 새로 만들지 않고 기존 이동 뷰 루트를 재사용한다
            // (Docs/설계/10-2026-08-26-씬전환_연출_아키텍처.md §8).
            registrar.Resolve<ISceneTransitionContentRootRegistry>().RegisterContentRoot(ContentSceneId.Field, fieldUIController.MovementViewRoot);
        }
    }
}

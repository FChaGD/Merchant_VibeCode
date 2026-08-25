using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class UIManager : MonoBehaviour, IUIManager, IManagedComponent
    {
        private IGameManager gameManager;
        private ISceneLoader sceneLoader;
        private IHubUIController hubUIController;
        private IFormationPanel formationPanel;
        private ITripPanel tripPanel;
        private IFieldUIController fieldUIController;
        private ISessionState sessionState;
        private IEncounterManager encounterManager;
        private IBattleController battleController;
        private IBattleResultSource battleResultSource;
        private IDefeatConsequenceSource defeatConsequenceSource;
        private IBattleSimulationEvents battleSimulationEvents;

        // 상행 관리 데이터 시스템이 아직 없어 선택적으로 조회한다 - 등록되면 자동으로 연결된다.
        private ICaravanRosterProvider caravanRosterProvider;
        private IFormationRepository formationRepository;
        private ITripInfoProvider tripInfoProvider;

        private readonly Dictionary<string, IUIPanel> panelsById = new();
        private readonly PanelNavigationStack navigation = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            gameManager = registrar.Resolve<IGameManager>();

            sceneLoader = registrar.Resolve<ISceneLoader>();
            sceneLoader.OnSceneLoaded += HandleSceneLoaded;

            // HubUIController/FormationPanel/TripPanel은 전역 DI 대상이 아니라 UIManager 산하 컴포넌트 — 같은 GameObject에서 직접 조회한다.
            hubUIController = GetComponent<IHubUIController>();
            if (hubUIController == null)
            {
                throw new InvalidOperationException($"{nameof(UIManager)}와 같은 GameObject에 {nameof(IHubUIController)} 구현체가 없다.");
            }

            formationPanel = GetComponent<IFormationPanel>();
            if (formationPanel == null)
            {
                throw new InvalidOperationException($"{nameof(UIManager)}와 같은 GameObject에 {nameof(IFormationPanel)} 구현체가 없다.");
            }

            tripPanel = GetComponent<ITripPanel>();
            if (tripPanel == null)
            {
                throw new InvalidOperationException($"{nameof(UIManager)}와 같은 GameObject에 {nameof(ITripPanel)} 구현체가 없다.");
            }

            fieldUIController = GetComponent<IFieldUIController>();
            if (fieldUIController == null)
            {
                throw new InvalidOperationException($"{nameof(UIManager)}와 같은 GameObject에 {nameof(IFieldUIController)} 구현체가 없다.");
            }

            sessionState = registrar.Resolve<ISessionState>();
            encounterManager = registrar.Resolve<IEncounterManager>();
            battleController = registrar.Resolve<IBattleController>();
            battleResultSource = registrar.Resolve<IBattleResultSource>();
            defeatConsequenceSource = registrar.Resolve<IDefeatConsequenceSource>();
            battleSimulationEvents = registrar.Resolve<IBattleSimulationEvents>();

            registrar.TryResolve(out caravanRosterProvider);
            registrar.TryResolve(out formationRepository);
            registrar.TryResolve(out tripInfoProvider);

            // TODO: TacticsPanel, HUDPanel, ResultPanel 등 추가 IUIPanel 구현체 연결 - 각 하위 컴포넌트 설계 후 구현
        }

        public void Open(string panelId)
        {
            if (!panelsById.TryGetValue(panelId, out var panel))
            {
                Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
                return;
            }

            var previousToHide = navigation.BeginOpen(panelId);
            if (previousToHide != null && panelsById.TryGetValue(previousToHide, out var previousPanel))
            {
                previousPanel.Close();
            }

            panel.Open();
        }

        public void Close(string panelId)
        {
            if (!panelsById.TryGetValue(panelId, out var panel))
            {
                Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
                return;
            }

            panel.Close();

            var returnTarget = navigation.ResolveReturnTarget(panelId);
            if (returnTarget != null)
            {
                Open(returnTarget);
            }
        }

        private void HandleSceneLoaded(string sceneName)
        {
            if (sceneName == SceneNames.Hub)
            {
                hubUIController.RegisterHubUI(this);

                formationPanel.RegisterFormationUI(caravanRosterProvider, formationRepository, this, SceneNames.Hub);
                panelsById[formationPanel.PanelId] = formationPanel;

                tripPanel.RegisterTripUI(this, gameManager, formationRepository, tripInfoProvider);
                panelsById[tripPanel.PanelId] = tripPanel;
            }
            else if (sceneName == SceneNames.Field)
            {
                // Formation UI(정비창)는 Hub 전용이 아니다 - Field도 자신만의 화면 요소를 갖고 있어
                // (FieldUIInstaller 참고) 여기서도 다시 등록해야 "정비창 재호출"이 동작한다.
                formationPanel.RegisterFormationUI(caravanRosterProvider, formationRepository, this, SceneNames.Field);
                panelsById[formationPanel.PanelId] = formationPanel;

                fieldUIController.RegisterFieldUI(this, sessionState, encounterManager, battleController, battleResultSource, defeatConsequenceSource, battleSimulationEvents, gameManager);
            }
        }

        private void OnDestroy()
        {
            if (sceneLoader != null)
            {
                sceneLoader.OnSceneLoaded -= HandleSceneLoaded;
            }
        }
    }
}

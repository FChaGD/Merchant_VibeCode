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

        // 상행 관리 데이터 시스템이 아직 없어 선택적으로 조회한다 - 등록되면 자동으로 연결된다.
        private ICaravanRosterProvider caravanRosterProvider;
        private IFormationRepository formationRepository;

        private readonly Dictionary<string, IUIPanel> panelsById = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            gameManager = registrar.Resolve<IGameManager>();

            sceneLoader = registrar.Resolve<ISceneLoader>();
            sceneLoader.OnSceneLoaded += HandleSceneLoaded;

            // HubUIController/FormationPanel은 전역 DI 대상이 아니라 UIManager 산하 컴포넌트 — 같은 GameObject에서 직접 조회한다.
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

            registrar.TryResolve(out caravanRosterProvider);
            registrar.TryResolve(out formationRepository);

            // TODO: TacticsPanel, HUDPanel, ResultPanel 등 추가 IUIPanel 구현체 연결 - 각 하위 컴포넌트 설계 후 구현
        }

        public void Open(string panelId)
        {
            if (panelsById.TryGetValue(panelId, out var panel))
            {
                panel.Open();
            }
            else
            {
                Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
            }
        }

        public void Close(string panelId)
        {
            if (panelsById.TryGetValue(panelId, out var panel))
            {
                panel.Close();
            }
            else
            {
                Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
            }
        }

        private void HandleSceneLoaded(string sceneName)
        {
            if (sceneName == SceneNames.Hub)
            {
                hubUIController.RegisterHubUI(gameManager, this);

                formationPanel.RegisterFormationUI(caravanRosterProvider, formationRepository);
                panelsById[formationPanel.PanelId] = formationPanel;
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

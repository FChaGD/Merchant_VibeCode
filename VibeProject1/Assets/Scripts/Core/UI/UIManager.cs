using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class UIManager : MonoBehaviour, IUIManager, IPanelRegistrar, IManagedComponent
    {
        private IDependencyRegistrar registrar;
        private ISceneLoader sceneLoader;

        private readonly Dictionary<string, IUIPanel> panelsById = new();
        private readonly Dictionary<ContentSceneId, IContentSceneUIWiring> wiringBySceneId = new();
        private readonly PanelNavigationStack navigation = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            this.registrar = registrar;

            sceneLoader = registrar.Resolve<ISceneLoader>();
            sceneLoader.OnSceneLoaded += HandleSceneLoaded;

            // 씬별 UI 배선(IContentSceneUIWiring)은 전역 DI 대상이 아니라 UIManager 산하 컴포넌트다 -
            // 같은 GameObject에서 전부 수집해 씬 id로 찾아 위임한다. 새 콘텐츠 씬이 늘어나도 이 목록
            // 수집 로직은 그대로이고, 새 구현체를 형제 컴포넌트로 추가/등록하기만 하면 된다.
            foreach (var wiring in GetComponents<IContentSceneUIWiring>())
            {
                if (!wiringBySceneId.TryAdd(wiring.SceneId, wiring))
                {
                    Debug.LogWarning($"'{wiring.SceneId}'에 대한 {nameof(IContentSceneUIWiring)}이 중복 등록되어 있다: {wiring.GetType().Name}", this);
                }
            }

            // TODO: TacticsPanel, HUDPanel, ResultPanel 등 추가 IUIPanel 구현체 연결 - 각 하위 컴포넌트 설계 후 구현
        }

        public void RegisterPanel(IUIPanel panel)
        {
            panelsById[panel.PanelId] = panel;
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
            // ContentSceneId 밖의 씬(예: SampleScene)은 UI 배선 대상이 아니다 - 조용히 건너뛴다.
            if (!Enum.TryParse<ContentSceneId>(sceneName, out var sceneId) || !wiringBySceneId.TryGetValue(sceneId, out var wiring))
            {
                return;
            }

            wiring.Wire(registrar, this, this);
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

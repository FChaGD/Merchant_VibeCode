using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 최소 IUIManager 구현. 실제 UIManager는 ISceneLoader(Hub/Field 콘텐츠 씬
    /// 전환 시스템)에 강하게 결합돼 있어(ResolveDependencies가 registrar.Resolve&lt;ISceneLoader&gt;()를
    /// 하드 요구) 독립 씬인 배틀 테스트 씬에 그대로 가져다 쓸 수 없다 - 여기서는 "패널 열기/닫기 +
    /// 이전 패널로 복귀" 정책만 그대로 재사용(PanelNavigationStack)하고 씬 전환 관련 책임은 뺐다.
    /// </summary>
    public class BattleTestPanelHost : MonoBehaviour, IUIManager, IPanelRegistrar, IManagedComponent
    {
        private readonly Dictionary<string, IUIPanel> panelsById = new();
        private readonly PanelNavigationStack navigation = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 이 씬은 콘텐츠 씬 전환이 없어 IContentSceneUIWiring 수집/위임이 필요 없다 -
            // BattleTestController가 FormationPanel/TacticsPanel을 직접 등록한다.
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
    }
}

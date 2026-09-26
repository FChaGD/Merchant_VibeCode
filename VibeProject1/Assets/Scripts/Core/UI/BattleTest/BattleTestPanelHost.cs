using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 최소 IUIManager 구현. 실제 UIManager는 ISceneLoader(Hub/Field 콘텐츠 씬
    /// 전환 시스템)에 강하게 결합돼 있어(ResolveDependencies가 registrar.Resolve&lt;ISceneLoader&gt;()를
    /// 하드 요구) 독립 씬인 배틀 테스트 씬에 그대로 가져다 쓸 수 없다 - 여기서는 "패널 열기/닫기 +
    /// 이전 패널로 복귀" 정책만 그대로 재사용(PanelChannel)하고 씬 전환 관련 책임은 뺐다.
    /// </summary>
    public class BattleTestPanelHost : MonoBehaviour, IUIManager, IPanelRegistrar, IManagedComponent
    {
        // UIManager와 같은 채널 구성(Docs/설계/38번 §4) - 이 씬은 depth 패널이 없지만 IPanelRegistrar
        // 계약을 동일하게 만족시키기 위해 두 채널을 모두 둔다.
        private readonly PanelChannel depthChannel = new();
        private readonly PanelChannel popupChannel = new();

        private void Awake()
        {
            depthChannel.ActiveChanged += hasActive => RootDepthChanged?.Invoke(!hasActive);
            popupChannel.ActiveChanged += hasActive => ModalPopupOpenChanged?.Invoke(hasActive);
        }

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 이 씬은 콘텐츠 씬 전환이 없어 IContentSceneUIWiring 수집/위임이 필요 없다 -
            // BattleTestController가 FormationPanel/TacticsPanel을 직접 등록한다.
        }

        public bool IsAtRootDepth => !depthChannel.HasActive;
        public event Action<bool> RootDepthChanged;
        public bool IsModalPopupOpen => popupChannel.HasActive;
        public event Action<bool> ModalPopupOpenChanged;

        public void RegisterDepthPanel(IUIPanel panel) => depthChannel.Register(panel);
        public void RegisterPopupPanel(IUIPanel panel) => popupChannel.Register(panel);

        public void Open(string panelId)
        {
            if (depthChannel.Contains(panelId)) depthChannel.Open(panelId);
            else if (popupChannel.Contains(panelId)) popupChannel.Open(panelId);
            else Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
        }

        public void Close(string panelId)
        {
            if (depthChannel.Contains(panelId)) depthChannel.Close(panelId);
            else if (popupChannel.Contains(panelId)) popupChannel.Close(panelId);
            else Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
        }

        // 배틀 테스트 씬에는 인벤토리 팝업이 없다 - IPanelRegistrar 계약을 만족시키기 위한 빈 구현.
        public void RegisterInventoryPopup(IUIPanel popup)
        {
            Debug.LogWarning($"배틀 테스트 씬은 인벤토리 팝업을 지원하지 않는다: '{popup?.PanelId}'.");
        }

        // 배틀 테스트 씬(Hub/상행 없이 전투만 반복 검증)에는 인벤토리 팝업 자체가 없다 - IUIManager
        // 계약을 만족시키기 위한 빈 구현(Docs/설계/32번 §5).
        public void ToggleInventoryPopup(string popupId)
        {
            Debug.LogWarning($"배틀 테스트 씬은 인벤토리 팝업을 지원하지 않는다: '{popupId}'.");
        }
    }
}

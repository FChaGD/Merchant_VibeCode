using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 패널 호스트. 실제 UIManager는 ISceneLoader(Hub/Field 콘텐츠 씬 전환 시스템)에 강하게 결합돼 있어
    /// 독립 씬인 배틀 테스트 씬에 그대로 가져다 쓸 수 없다 - 패널 라우팅·두 축 신호는 UIManager와 같은 공용 패널 라우터를 소유해
    /// 재사용하고(Docs/Refactor/2026-10-26_공통.md B-2), 씬 전환·인벤토리 팝업 책임은 갖지 않는다(인벤토리 계약을 구현하지 않음, B-3).
    /// </summary>
    public class BattleTestPanelHost : MonoBehaviour, IPanelNavigator, IUIVisibilitySignal, IPanelRegistrar, IManagedComponent
    {
        private readonly PanelRouter router = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IPanelNavigator>(this);
            registrar.Register<IUIVisibilitySignal>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 이 씬은 콘텐츠 씬 전환이 없어 IContentSceneUIWiring 수집/위임이 필요 없다 -
            // BattleTestController가 FormationPanel/TacticsPanel을 직접 등록한다.
        }

        public bool IsAtRootDepth => router.IsAtRootDepth;
        public event Action<bool> RootDepthChanged { add => router.RootDepthChanged += value; remove => router.RootDepthChanged -= value; }
        public bool IsScreenOpen => router.IsScreenOpen;
        public event Action<bool> ScreenOpenChanged { add => router.ScreenOpenChanged += value; remove => router.ScreenOpenChanged -= value; }
        public bool IsModalPopupOpen => router.IsModalPopupOpen;
        public event Action<bool> ModalPopupOpenChanged { add => router.ModalPopupOpenChanged += value; remove => router.ModalPopupOpenChanged -= value; }

        public void RegisterDepthPanel(IUIPanel panel) => router.RegisterDepthPanel(panel);
        public void RegisterScreenPanel(IUIPanel panel) => router.RegisterScreenPanel(panel);
        public void RegisterPopupPanel(IUIPanel panel) => router.RegisterPopupPanel(panel);

        public void Open(string panelId) => router.Open(panelId);

        public void Close(string panelId) => router.Close(panelId);
    }
}

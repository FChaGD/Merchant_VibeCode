using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 콘텐츠 씬이 로드될 때마다 그 씬의 UI 배선(IContentSceneUIWiring)에 위임하고, 패널 열기/닫기·두 축 신호는 공용 패널 라우터에,
    /// 인벤토리 팝업은 조율자에 위임한다(Docs/Refactor/2026-10-26_공통.md B-2). 소비자가 쓰는 계약을 각각 DI에 등록한다 -
    /// 등록하지 않은 계약 타입은 TryResolve가 조용히 null을 돌려준다.
    /// </summary>
    public class UIManager : MonoBehaviour, IUIManager, IPanelRegistrar, IInventoryPopupRegistrar, IManagedComponent
    {
        private IDependencyResolver registrar;
        private ISceneLoader sceneLoader;
        private IInventoryPopupCoordinator inventoryPopupCoordinator;

        private readonly Dictionary<ContentSceneId, IContentSceneUIWiring> wiringBySceneId = new();

        private readonly PanelRouter router = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IUIManager>(this);
            registrar.Register<IPanelNavigator>(this);
            registrar.Register<IInventoryPopupToggle>(this);
            registrar.Register<IUIVisibilitySignal>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            this.registrar = registrar;

            sceneLoader = registrar.Resolve<ISceneLoader>();
            sceneLoader.OnSceneLoaded += HandleSceneLoaded;

            inventoryPopupCoordinator = GetComponent<IInventoryPopupCoordinator>();
            if (inventoryPopupCoordinator == null)
            {
                throw new InvalidOperationException($"{nameof(UIManager)}와 같은 GameObject에 {nameof(IInventoryPopupCoordinator)} 구현체가 없다.");
            }

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

            // 산하 패널의 로딩 준비 작업(Docs/설계/67번 §4.3)을 Bootstrap 초기화 때 한 번 등록한다 - 전환을 시작할 때 작업 수가 이미
            // 정해져 있어야 로딩바 게이지가 뒤로 가지 않는다. 패널은 전역 DI 대상이 아니라 조율자를 모르므로 여기서 대신 등록한다.
            if (registrar.TryResolve<ISceneLoadingTaskRegistry>(out var loadingTasks))
            {
                foreach (var source in GetComponents<ISceneLoadingTaskSource>())
                {
                    foreach (var task in source.GetLoadingTasks()) loadingTasks.Register(source.LoadingScene, task);
                }
            }
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
        public void RegisterInventoryPopup(IUIPanel popup) => inventoryPopupCoordinator.RegisterPopup(popup);

        public void Open(string panelId) => router.Open(panelId);

        public void Close(string panelId) => router.Close(panelId);

        public void ToggleInventoryPopup(string popupId) => inventoryPopupCoordinator.Toggle(popupId);

        public void OpenInventoryPopup(string popupId)
        {
            if (!inventoryPopupCoordinator.IsOpen(popupId)) inventoryPopupCoordinator.Toggle(popupId);
        }

        public void CloseAllInventoryPopups() => inventoryPopupCoordinator.CloseAll();

        private void HandleSceneLoaded(string sceneName)
        {
            // ContentSceneId 밖의 씬(예: SampleScene)은 UI 배선 대상이 아니다 - 조용히 건너뛴다.
            if (!Enum.TryParse<ContentSceneId>(sceneName, out var sceneId) || !wiringBySceneId.TryGetValue(sceneId, out var wiring))
            {
                return;
            }

            // 이 씬의 패널 시각 요소는 전부 새로 만들어졌으므로, 이전 씬(또는 이전 방문)의 열림/복귀
            // 기록은 더 이상 유효하지 않다 - 씬 전환이 항상 UIManager.Close를 거치는 것은 아니므로
            // (예: "상행 시작"으로 인한 Hub→Field 전환) 여기서 매번 명시적으로 지운다.
            router.Reset();
            inventoryPopupCoordinator.Reset();

            wiring.Wire(registrar, this, this, this);
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

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public class UIManager : MonoBehaviour, IUIManager, IPanelRegistrar, IManagedComponent
    {
        private IDependencyResolver registrar;
        private ISceneLoader sceneLoader;
        private IInventoryPopupCoordinator inventoryPopupCoordinator;

        private readonly Dictionary<ContentSceneId, IContentSceneUIWiring> wiringBySceneId = new();

        // depth 패널과 모달 팝업은 네비게이션 스택을 따로 쓴다 - 모달 팝업을 열어도 depth 패널이 닫혔다
        // 열리지 않게 하기 위함이다(Docs/설계/38번 §4.2). 채널별 활성 변화가 곧 두 축의 신호다.
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
        }

        public bool IsAtRootDepth => !depthChannel.HasActive;
        public event Action<bool> RootDepthChanged;
        public bool IsModalPopupOpen => popupChannel.HasActive;
        public event Action<bool> ModalPopupOpenChanged;

        public void RegisterDepthPanel(IUIPanel panel) => depthChannel.Register(panel);
        public void RegisterPopupPanel(IUIPanel panel) => popupChannel.Register(panel);
        public void RegisterInventoryPopup(IUIPanel popup) => inventoryPopupCoordinator.RegisterPopup(popup);

        public void Open(string panelId)
        {
            if (TryGetChannel(panelId, out var channel))
            {
                channel.Open(panelId);
            }
        }

        public void Close(string panelId)
        {
            if (TryGetChannel(panelId, out var channel))
            {
                channel.Close(panelId);
            }
        }

        private bool TryGetChannel(string panelId, out PanelChannel channel)
        {
            channel = depthChannel.Contains(panelId) ? depthChannel
                : popupChannel.Contains(panelId) ? popupChannel
                : null;
            if (channel == null)
            {
                Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
            }
            return channel != null;
        }

        public void ToggleInventoryPopup(string popupId) => inventoryPopupCoordinator.Toggle(popupId);

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
            depthChannel.Reset();
            popupChannel.Reset();
            inventoryPopupCoordinator.Reset();

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

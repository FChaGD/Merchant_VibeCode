using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 패널 종류별 채널(depth·시설 화면·모달)과 "패널 Id → 채널" 라우팅, 채널 활성 변화 → 두 축 신호 변환을 묶었다
    /// (Docs/Refactor/2026-10-26_공통.md B-2). UIManager는 씬 전환(ISceneLoader)에 묶여 있어 배틀 테스트 씬이 재사용하지 못하고
    /// 같은 로직을 따로 구현하고 있었다 - 이 객체를 두 호스트가 함께 소유한다. 채널마다 스택이 따로라 다른 종류의 패널을 열어도
    /// 서로 닫히지 않는다(설계 38번 §4.2).
    /// </summary>
    internal sealed class PanelRouter : IPanelNavigator, IUIVisibilitySignal, IPanelRegistrar
    {
        private readonly PanelChannel depthChannel = new();
        private readonly PanelChannel screenChannel = new();
        private readonly PanelChannel popupChannel = new();

        public PanelRouter()
        {
            depthChannel.ActiveChanged += hasActive => RootDepthChanged?.Invoke(!hasActive);
            screenChannel.ActiveChanged += hasActive => ScreenOpenChanged?.Invoke(hasActive);
            popupChannel.ActiveChanged += hasActive => ModalPopupOpenChanged?.Invoke(hasActive);
        }

        public bool IsAtRootDepth => !depthChannel.HasActive;
        public event Action<bool> RootDepthChanged;
        public bool IsScreenOpen => screenChannel.HasActive;
        public event Action<bool> ScreenOpenChanged;
        public bool IsModalPopupOpen => popupChannel.HasActive;
        public event Action<bool> ModalPopupOpenChanged;

        public void RegisterDepthPanel(IUIPanel panel) => depthChannel.Register(panel);
        public void RegisterScreenPanel(IUIPanel panel) => screenChannel.Register(panel);
        public void RegisterPopupPanel(IUIPanel panel) => popupChannel.Register(panel);

        public bool Contains(string panelId) => FindChannel(panelId) != null;

        public void Open(string panelId)
        {
            if (TryGetChannel(panelId, out var channel)) channel.Open(panelId);
        }

        public void Close(string panelId)
        {
            if (TryGetChannel(panelId, out var channel)) channel.Close(panelId);
        }

        /// <summary>콘텐츠 씬 (재)로드 시 - 이전 씬 패널의 열림·복귀 기록을 모든 채널에서 지운다(PanelNavigationStack.Reset 요약 주석).</summary>
        public void Reset()
        {
            depthChannel.Reset();
            screenChannel.Reset();
            popupChannel.Reset();
        }

        private PanelChannel FindChannel(string panelId)
            => depthChannel.Contains(panelId) ? depthChannel
                : screenChannel.Contains(panelId) ? screenChannel
                : popupChannel.Contains(panelId) ? popupChannel
                : null;

        private bool TryGetChannel(string panelId, out PanelChannel channel)
        {
            channel = FindChannel(panelId);
            if (channel == null)
            {
                Debug.LogWarning($"'{panelId}'에 해당하는 UI 패널이 등록되어 있지 않다.");
            }
            return channel != null;
        }
    }
}

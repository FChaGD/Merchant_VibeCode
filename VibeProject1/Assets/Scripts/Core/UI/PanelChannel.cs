using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 패널 한 종류(depth 패널 또는 모달 팝업)의 등록 목록 + 네비게이션 스택 + "활성 패널 있음" 변화 신호를
    /// 묶은 단위(Docs/설계/38번 §4). 종류마다 스택이 따로여야 모달 팝업을 열어도 depth 패널이 닫혔다
    /// 열리지 않는다. UIManager와 BattleTestPanelHost가 같은 열기/닫기/복귀 로직을 중복 구현하지 않도록
    /// 이 클래스를 공유한다.
    /// </summary>
    internal class PanelChannel
    {
        private readonly Dictionary<string, IUIPanel> panelsById = new();
        private readonly PanelNavigationStack navigation = new();

        public bool HasActive => navigation.HasActive;

        /// <summary>
        /// 활성 패널 유무가 실제로 바뀔 때만 발생한다 - 같은 채널 안에서 패널끼리 전환(상행 준비→상단 배치→
        /// 복귀)하는 동안에는 true가 유지되어 발생하지 않는다.
        /// </summary>
        public event Action<bool> ActiveChanged;

        public void Register(IUIPanel panel) => panelsById[panel.PanelId] = panel;

        public bool Contains(string panelId) => panelsById.ContainsKey(panelId);

        public void Open(string panelId)
        {
            var wasActive = HasActive;
            OpenWithoutNotify(panelId);
            NotifyIfChanged(wasActive);
        }

        public void Close(string panelId)
        {
            var wasActive = HasActive;
            if (!panelsById.TryGetValue(panelId, out var panel))
            {
                return;
            }

            panel.Close();
            var returnTarget = navigation.ResolveReturnTarget(panelId);
            if (returnTarget != null)
            {
                OpenWithoutNotify(returnTarget);
            }

            NotifyIfChanged(wasActive);
        }

        /// <summary>
        /// 콘텐츠 씬 (재)로드 시 호출한다 - PanelNavigationStack.Reset 요약 주석과 같은 이유. 이전 씬에서
        /// 패널이 열린 채 전환됐다면 비활성으로 바뀌었다는 신호도 함께 보낸다.
        /// </summary>
        public void Reset()
        {
            var wasActive = HasActive;
            navigation.Reset();
            NotifyIfChanged(wasActive);
        }

        private void OpenWithoutNotify(string panelId)
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

        private void NotifyIfChanged(bool wasActive)
        {
            if (HasActive != wasActive)
            {
                ActiveChanged?.Invoke(HasActive);
            }
        }
    }
}

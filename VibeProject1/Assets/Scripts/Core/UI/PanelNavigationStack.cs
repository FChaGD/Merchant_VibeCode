using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// "패널을 열 때 이전 패널을 숨기고, 닫으면 그 이전 패널로 되돌아간다"는 네비게이션 정책만
    /// 담당한다. 패널 조회/등록과 실제 Open/Close 호출은 UIManager의 책임으로 남긴다.
    /// </summary>
    internal class PanelNavigationStack
    {
        private string activePanelId;
        private readonly Dictionary<string, string> returnToPanelId = new();

        /// <summary>
        /// 패널을 열기 직전에 호출한다. 숨겨야 할 이전 패널의 id를 반환한다(없으면 null).
        /// </summary>
        public string BeginOpen(string panelId)
        {
            string previousToHide = null;
            if (activePanelId != null && activePanelId != panelId)
            {
                previousToHide = activePanelId;
                returnToPanelId[panelId] = activePanelId;
            }

            activePanelId = panelId;
            return previousToHide;
        }

        /// <summary>
        /// 패널을 닫은 직후에 호출한다. 복귀해야 할 패널의 id를 반환한다(없으면 null).
        /// </summary>
        public string ResolveReturnTarget(string closedPanelId)
        {
            if (activePanelId == closedPanelId)
            {
                activePanelId = null;
            }

            return returnToPanelId.Remove(closedPanelId, out var previous) ? previous : null;
        }
    }
}

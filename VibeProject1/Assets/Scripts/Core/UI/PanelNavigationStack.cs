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

        /// <summary>
        /// 콘텐츠 씬이 (재)로드될 때마다 호출해야 한다. 패널의 실제 시각 요소는 씬 스코프라 씬이
        /// 다시 로드되면 전부 재생성되는데, 이 스택이 들고 있는 activePanelId/복귀 매핑은 이전 씬의
        /// 패널을 가리키는 문자열이라 자동으로는 안 지워진다 - 예를 들어 상행 준비 UI에서 "상행 시작"을
        /// 눌러 Field로 전환하면 UIManager.Close를 거치지 않으므로 activePanelId가 "Trip"으로 남고,
        /// 다음 Hub 세션에서 배치 UI를 Hub 배치 버튼으로 직접 열어도 그 잔여값 때문에 잘못된 복귀
        /// 대상(Trip)이 기록되는 버그가 있었다.
        /// </summary>
        public void Reset()
        {
            activePanelId = null;
            returnToPanelId.Clear();
        }
    }
}

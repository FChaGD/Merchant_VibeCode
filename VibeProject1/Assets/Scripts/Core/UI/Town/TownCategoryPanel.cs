using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마을 카테고리 depth. 시각적으로는 오버레이가 아니라 루트 depth의 버튼 구성이 바뀌는 것이지만,
    /// "열면 루트 depth가 숨고 닫으면 돌아온다"는 계약이 기존 패널과 같아 IUIPanel로 표현한다 - 루트
    /// 숨김/복귀(IUIVisibilitySignal.RootDepthChanged)와 후속 시설 화면에서의 복귀(depth 채널 스택)를 새 코드
    /// 없이 그대로 쓴다(Docs/설계/37번 §4.1). depth 패널로 등록된다(RegisterDepthPanel, 38번 §4.1).
    /// IUIPanel.Open()이 매개변수를 받지 않으므로 카테고리마다 인스턴스를 나눈다(MonoBehaviour 아님 - HubUIWiring이 생성한다).
    /// </summary>
    public class TownCategoryPanel : IUIPanel
    {
        private readonly string categoryId;
        private readonly TownCategoryDepthElements elements;
        private readonly IUIManager uiManager;
        private readonly CurrentTownFacilityFilter facilityFilter;
        private readonly List<GameObject> visibleBuffer = new();

        public TownCategoryPanel(string categoryId, TownCategoryDepthElements elements, IUIManager uiManager, CurrentTownFacilityFilter facilityFilter)
        {
            this.categoryId = categoryId;
            this.elements = elements;
            this.uiManager = uiManager;
            this.facilityFilter = facilityFilter;
        }

        public string PanelId => UIPanelIds.TownCategory(categoryId);

        public void Open()
        {
            visibleBuffer.Clear();
            foreach (var facilityId in TownFacilityCatalog.GetFacilityIds(categoryId))
            {
                if (!facilityFilter.IsFacilityAvailable(facilityId) || !elements.TryGetFacilityButton(facilityId, out var button))
                {
                    continue;
                }

                // 시설 버튼/뒤로 가기는 카테고리 4개가 공유하는 요소라, 열 때마다 이 카테고리 기준으로 다시 연결한다.
                var capturedId = facilityId;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Debug.LogWarning($"'{capturedId}' 시설 화면은 아직 구현되지 않았다."));
                visibleBuffer.Add(button.gameObject);
            }

            elements.BackButton.onClick.RemoveAllListeners();
            elements.BackButton.onClick.AddListener(() => uiManager.Close(PanelId));

            elements.Column.Arrange(visibleBuffer, elements.BackButton.gameObject);
            elements.Root.SetActive(true);
        }

        // 이 메서드는 표시/숨김만 한다. 뒤로 가기 등 네비게이션은 패널이 직접 Close()를 부르지 않고
        // UIManager.Close(PanelId)에 위임해야 한다 - 그래야 루트 depth 복귀(RootDepthChanged)와
        // 복귀 대상 계산(depth 채널 스택)이 함께 처리된다.
        public void Close()
        {
            elements.Root.SetActive(false);
        }
    }
}

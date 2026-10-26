using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 숙소 화면(기획 94 4-1~4-6·4-11, Docs/설계/103번 §4.3). 좌측 휴식 설정, 우측 탭(전투 장비/전투 소모품/개인 물품) 편집. 무역품 구매 화면과 같은
    /// 시설 화면 방식이라 모달 채널에 등록되고 HubUIWiring이 Hub 로드마다 새로 만든다 - 골드·저장소(Bootstrap 상주) 이벤트를 구독하므로 교체 시 Dispose한다.
    /// 탭마다 편집 본문 컨트롤러를 따로 둔다 - 탭을 바꿀 때 보이는 본문만 Show하고 나머지는 Hide해 드래그·버리기 선택 상태가 남지 않게 한다.
    /// 휴식 가능 여부는 InnRestPlan 하나로 판정해 버튼 활성과 툴팁 사유가 어긋나지 않는다.
    /// </summary>
    public sealed class InnPanel : IUIPanel, IPanelCloseGuard, IDisposable
    {
        private const string ExitBlockedText = "정리하지 못한 물품이 있어 나갈 수 없습니다.";

        private readonly InnElements elements;
        private readonly IInnRestService rest;
        private readonly IGoldHoldingsReader gold;
        private readonly InnRestProgressPanel progress;
        private readonly List<InventoryArrangementController> bodies = new();
        private readonly ITooltipHandle restTooltip;
        private readonly int defaultTabIndex;

        private int days = InnRestPlan.MinDays;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.Inn);

        /// <param name="tabSources">탭 순서대로 각 탭 인벤토리의 조회·정리 계약. elements.Tabs와 같은 순서다.</param>
        public InnPanel(InnElements elements, IReadOnlyList<(IInventoryReader Reader, IInventoryArrangement Arrangement)> tabSources, IInnRestService rest, IGoldHoldingsReader gold, ITooltipController tooltips, InnRestProgressPanel progress, IUIManager uiManager, IInputManager input)
        {
            this.elements = elements;
            this.rest = rest;
            this.gold = gold;
            this.progress = progress;

            for (var i = 0; i < elements.Tabs.Count; i++)
            {
                var tab = elements.Tabs[i];
                var spec = tab.Spec;
                // 골드 상자는 상단 물류품에만 있어 숙소 탭에는 골드 정보를 넘기지 않는다(설계 102번 §4.2).
                bodies.Add(new InventoryArrangementController(tab.Body, input, null, tabSources[i].Reader, tabSources[i].Arrangement, spec.AllowsRotation, spec.HasStaging, spec.HasSections));
                var index = i;
                tab.Button.onClick.RemoveAllListeners();
                tab.Button.onClick.AddListener(() => SelectTab(index));
                if (spec.PopupId == InventoryPopupIds.PersonalItem) defaultTabIndex = i; // 기본 탭 = 개인 물품(기획 94 4-5)
            }

            restTooltip = tooltips.Register(elements.RestHover, elements.RestPress, elements.RestTooltip, elements.RestTooltipText);

            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 닫기 직전 확인(TryPrepareClose)과 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.DaysDownButton.onClick.RemoveAllListeners();
            elements.DaysDownButton.onClick.AddListener(() => SetDays(days - 1));
            elements.DaysUpButton.onClick.RemoveAllListeners();
            elements.DaysUpButton.onClick.AddListener(() => SetDays(days + 1));
            elements.RestButton.onClick.RemoveAllListeners();
            elements.RestButton.onClick.AddListener(StartRest);

            if (gold != null) gold.Changed += HandleGoldChanged;
            elements.Root.SetActive(false);
        }

        // 열 때마다 개인 물품 탭 + 1일(기획 94 4-5, 설계 103번 §4.3). 휴식 진행에서 돌아올 때도 같다.
        public void Open()
        {
            isOpen = true;
            days = InnRestPlan.MinDays;
            elements.Root.SetActive(true);
            SelectTab(defaultTabIndex);
            RefreshRest();
        }

        // Close()는 표시/숨김만 한다. 나가기 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        public void Close()
        {
            isOpen = false;
            foreach (var body in bodies) body.Hide();
            elements.Root.SetActive(false);
        }

        /// <summary>
        /// 탭 인벤토리의 임시 보관을 빈 자리에 넣어 본다(지금 탭 사양은 모두 임시 보관이 없어 항상 통과 - 설계 103번 §2). 못 넣으면 나가기를 막는다.
        /// </summary>
        public bool TryPrepareClose()
        {
            for (var i = 0; i < bodies.Count; i++)
            {
                if (bodies[i].TryFlushStaging()) continue;

                SelectTab(i);
                bodies[i].ShowMessage(ExitBlockedText);
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if (gold != null) gold.Changed -= HandleGoldChanged;
            restTooltip?.Dispose();
            foreach (var body in bodies) body.Dispose();
        }

        private void SelectTab(int index)
        {
            for (var i = 0; i < bodies.Count; i++)
            {
                var selected = i == index;
                var tab = elements.Tabs[i];
                tab.Button.interactable = !selected; // 선택된 탭은 눌린 상태로 보인다
                if (selected)
                {
                    tab.Body.Root.gameObject.SetActive(true);
                    bodies[i].Show();
                }
                else
                {
                    bodies[i].Hide();
                    tab.Body.Root.gameObject.SetActive(false);
                }
            }
        }

        private void SetDays(int value)
        {
            days = InnRestPlan.ClampDays(value);
            RefreshRest();
        }

        private void HandleGoldChanged()
        {
            if (isOpen) RefreshRest();
        }

        private void RefreshRest()
        {
            var costPerDay = rest.CostPerDay;
            elements.DaysLabel.text = $"{days}일";
            elements.CostPerDayLabel.text = $"1일 비용 {costPerDay:N0}";
            elements.TotalCostLabel.text = $"총 비용 {InnRestPlan.TotalCost(days, costPerDay):N0}";
            elements.DaysDownButton.interactable = days > InnRestPlan.MinDays;
            elements.DaysUpButton.interactable = days < InnRestPlan.MaxDays;

            var reason = InnRestPlan.BlockReason(days, costPerDay, gold?.PersonalGold ?? 0);
            elements.RestButton.interactable = reason == null;
            restTooltip?.SetText(reason);
        }

        private void StartRest()
        {
            if (InnRestPlan.BlockReason(days, rest.CostPerDay, gold?.PersonalGold ?? 0) != null) return;
            progress.Begin(days); // 진행 화면이 같은 채널에서 열리며 채널이 이 패널을 숨긴다.
        }
    }
}

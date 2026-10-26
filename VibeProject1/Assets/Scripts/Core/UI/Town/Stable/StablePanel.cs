using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마구간(마차·시설 구매) 화면(Docs/기획/55번, 설계 56번 §7.2). 용병단 접촉 화면과 같은 목록형 구매 화면 요소를 쓰고
    /// (RosterShopElements), 시설 화면 채널에 등록되어 재화 패널 외 숨김·공업 지구 복귀를 기존 채널이 처리한다. plain C#이며
    /// HubUIWiring이 Hub 로드마다 새로 만든다 - 골드 보유·로스터 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// 좌측 보유 영역(목록·개별 정보·수리)은 StableOwnedSection에 위임한다(설계 91번 §5.2) - 이 패널은 구매만 맡는다.
    /// </summary>
    public sealed class StablePanel : IUIPanel, IDisposable
    {
        private const string EmptyCandidateText = "판매 품목 없음";

        private readonly RosterShopElements elements;
        private readonly IGoldSpender gold;
        private readonly ICaravanAssetCandidateReader candidateReader;
        private readonly IOwnedCaravanAssetRoster roster;
        private readonly StableOwnedSection ownedSection; // 선택적 - 요소가 없으면 보유 영역 없이 구매만
        private readonly ICaravanAssetIconReader iconReader;
        private readonly ITripCurrentLocationReader currentLocation; // 선택적 - 없으면 마을 Id 0(재고가 없는 마을로 취급된다)
        private readonly CaravanAssetPurchaseService purchaseService;
        private readonly RosterShopCandidateList candidateList;

        private IReadOnlyList<CaravanAssetProfile> candidates = Array.Empty<CaravanAssetProfile>();
        private int selectedIndex = -1;
        private bool isOpen;

        // 구매 버튼 비활성 사유 툴팁(Docs/Refactor/2026-10-26_공통.md D-1) - 배선이 등록해 넘기고, 이 패널이 Dispose에서 해제한다. 요소가 없으면 null.
        private readonly ITooltipHandle actionTooltip;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.Stable);

        public StablePanel(RosterShopElements elements, IGoldSpender gold, ICaravanAssetCandidateReader candidateReader, IOwnedCaravanAssetRoster roster, StableOwnedSection ownedSection, ICaravanAssetIconReader iconReader, ITownStockReader stockReader, ITownStockConsumer stockConsumer, ITripCurrentLocationReader currentLocation, IPanelNavigator uiManager, ITooltipHandle actionTooltip)
        {
            this.actionTooltip = actionTooltip;
            this.elements = elements;
            this.gold = gold;
            this.candidateReader = candidateReader;
            this.roster = roster;
            this.ownedSection = ownedSection;
            this.iconReader = iconReader;
            this.currentLocation = currentLocation;
            purchaseService = new CaravanAssetPurchaseService(gold, roster, stockReader, stockConsumer);
            candidateList = new RosterShopCandidateList(elements);

            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 카테고리 depth 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.ActionButton.onClick.RemoveAllListeners();
            elements.ActionButton.onClick.AddListener(Purchase);

            gold.Changed += HandleCurrencyChanged;
            roster.OnOwnedChanged += HandleRosterChanged;
            candidateReader.OnCandidatesChanged += HandleRosterChanged;
            elements.Root.SetActive(false);
        }

        public void Open()
        {
            isOpen = true;
            elements.Root.SetActive(true);
            ownedSection?.Open();
            Refresh();
        }

        // Close()는 표시/숨김만 한다. 나가기 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        public void Close()
        {
            isOpen = false;
            ownedSection?.Close();
            elements.Root.SetActive(false);
        }

        public void Dispose()
        {
            actionTooltip?.Dispose();
            gold.Changed -= HandleCurrencyChanged;
            roster.OnOwnedChanged -= HandleRosterChanged;
            candidateReader.OnCandidatesChanged -= HandleRosterChanged;
            ownedSection?.Dispose();
        }

        private void HandleCurrencyChanged()
        {
            if (isOpen) UpdateInfo();
        }

        // 재고 1대 = 1행이고 산 행은 재고 차감으로 사라지므로(설계 81번 §4.3) 선택을 해제하고 전부 다시 그린다(용병 화면과 같은 규칙).
        // 보유 목록 변경과 재고 변경 둘 다 이 처리기로 온다.
        private void HandleRosterChanged()
        {
            if (isOpen) Refresh();
        }

        private int CityId => currentLocation?.CurrentCityId ?? 0;

        private void Refresh()
        {
            candidates = candidateReader.GetCandidates(CityId, TownFacilityIds.Stable);
            selectedIndex = -1;

            candidateList.Render(candidates.Count, BindRow, EmptyCandidateText);
            UpdateInfo();
        }

        private void BindRow(RosterShopCandidateRowView row, int index)
        {
            var candidate = candidates[index];
            row.Bind(index, iconReader?.GetKindIcon(candidate.Kind), candidate.Name, candidate.KindLabel, candidate.Price, Select);
        }

        private void Purchase()
        {
            if (!TryGetSelected(out var candidate)) return;

            purchaseService.TryPurchase(candidate, CityId); // 성공 시 로스터·재고·재화 이벤트로 화면이 갱신된다.
            UpdateInfo();
        }

        private void Select(RosterShopCandidateRowView row)
        {
            selectedIndex = row.Index;
            UpdateInfo();
        }

        private bool TryGetSelected(out CaravanAssetProfile candidate)
        {
            var valid = selectedIndex >= 0 && selectedIndex < candidates.Count;
            candidate = valid ? candidates[selectedIndex] : null;
            return valid;
        }

        // 선택한 후보의 정보와 구매 가능 여부를 다시 그린다. 선택·재화·로스터가 바뀔 때마다 호출된다(기획 55번 §4.2).
        // 마차·시설은 스탯이 없어 상세 칸은 비워 둔다(기획 55번 §3).
        private void UpdateInfo()
        {
            candidateList.SetSelected(selectedIndex);
            elements.InfoDetail.text = string.Empty;

            if (!TryGetSelected(out var candidate))
            {
                elements.InfoName.text = string.Empty;
                elements.InfoKind.text = string.Empty;
                elements.InfoPrice.text = string.Empty;
                actionTooltip?.SetText(null);
                elements.ActionButton.interactable = false;
                return;
            }

            elements.InfoName.text = candidate.Name;
            elements.InfoKind.text = candidate.KindLabel;
            elements.InfoPrice.text = $"가격 {candidate.Price:N0}";

            var check = purchaseService.Evaluate(candidate, CityId);
            elements.ActionButton.interactable = check == CaravanAssetPurchaseCheck.Available;
            actionTooltip?.SetText(FacilityBlockReasons.For(check));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 마구간(마차·시설 구매) 화면(Docs/기획/55번, 설계 56번 §7.2). 용병단 접촉 화면과 같은 목록형 구매 화면 요소를 쓰고
    /// (RosterShopElements), 모달 팝업 채널에 등록되어 재화 패널 외 숨김·공업 지구 복귀를 기존 채널이 처리한다. plain C#이며
    /// HubUIWiring이 Hub 로드마다 새로 만든다 - 재화 지갑·로스터 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// 마차는 교역품이 아니므로 인벤토리 그리드가 아니라 좌측 목록으로 소유 현황을 보여 준다(기획 55번 §3).
    /// </summary>
    public sealed class StablePanel : IUIPanel, IDisposable
    {
        private const string EmptyCandidateText = "구매 가능한 마차·시설 없음";
        private const string InsufficientFundsText = "재화가 부족합니다.";
        private const string OwnedFullText = "더 보유할 수 없습니다.(최대 5개)";
        private const string AlreadyOwnedText = "이미 보유한 항목입니다.";
        // 좌측 목록의 종류 순서 - 카탈로그 목록 순서(마차 → 시설)와 같다.
        private static readonly FormationUnitKind[] KindOrder = { FormationUnitKind.Wagon, FormationUnitKind.Facility };

        private readonly RosterShopElements elements;
        private readonly IPlayerCurrencyWallet wallet;
        private readonly ICaravanAssetCandidateReader candidateReader;
        private readonly IOwnedCaravanAssetRoster roster;
        private readonly ICaravanAssetCatalogReader catalog;
        private readonly ICaravanAssetIconReader iconReader;
        private readonly ITripCurrentLocationReader currentLocation; // 선택적 - 없으면 마을 Id 0(지금은 무시되는 값)
        private readonly CaravanAssetPurchaseService purchaseService;
        private readonly RosterShopCandidateList candidateList;
        private readonly StringBuilder ownedText = new();

        private IReadOnlyList<CaravanAssetProfile> candidates = Array.Empty<CaravanAssetProfile>();
        private int selectedIndex = -1;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.Stable);

        public StablePanel(RosterShopElements elements, IPlayerCurrencyWallet wallet, ICaravanAssetCandidateReader candidateReader, IOwnedCaravanAssetRoster roster, ICaravanAssetCatalogReader catalog, ICaravanAssetIconReader iconReader, ITripCurrentLocationReader currentLocation, IUIManager uiManager)
        {
            this.elements = elements;
            this.wallet = wallet;
            this.candidateReader = candidateReader;
            this.roster = roster;
            this.catalog = catalog;
            this.iconReader = iconReader;
            this.currentLocation = currentLocation;
            purchaseService = new CaravanAssetPurchaseService(wallet, roster);
            candidateList = new RosterShopCandidateList(elements);

            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 카테고리 depth 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.ActionButton.onClick.RemoveAllListeners();
            elements.ActionButton.onClick.AddListener(Purchase);

            wallet.OnAmountChanged += HandleCurrencyChanged;
            roster.OnOwnedChanged += HandleRosterChanged;
            elements.Root.SetActive(false);
        }

        public void Open()
        {
            isOpen = true;
            elements.Root.SetActive(true);
            Refresh();
        }

        // Close()는 표시/숨김만 한다. 나가기 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        public void Close()
        {
            isOpen = false;
            elements.Root.SetActive(false);
        }

        public void Dispose()
        {
            wallet.OnAmountChanged -= HandleCurrencyChanged;
            roster.OnOwnedChanged -= HandleRosterChanged;
        }

        private void HandleCurrencyChanged(int _)
        {
            if (isOpen) UpdateInfo();
        }

        // 구매한 후보는 목록에서 사라지므로 선택을 해제하고 전부 다시 그린다(용병 화면과 같은 규칙).
        private void HandleRosterChanged()
        {
            if (isOpen) Refresh();
        }

        private void Refresh()
        {
            var cityId = currentLocation?.CurrentCityId ?? 0;
            candidates = candidateReader.GetCandidates(cityId, TownFacilityIds.Stable);
            selectedIndex = -1;

            RenderOwned();
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

            purchaseService.TryPurchase(candidate); // 성공 시 로스터·재화 이벤트로 화면이 갱신된다.
            UpdateInfo();
        }

        // 종류별 제목("마차 1/5")과 보유 개체 "n번 이름"(보유 순서, 기획 80번 §4-1).
        private void RenderOwned()
        {
            ownedText.Clear();
            foreach (var kind in KindOrder)
            {
                if (ownedText.Length > 0) ownedText.AppendLine();
                ownedText.Append("<b>").Append(catalog.GetKindLabel(kind)).Append(' ')
                    .Append(roster.CountOwnedOfKind(kind)).Append('/').Append(CaravanAssetPurchaseService.MaxOwnedPerKind).AppendLine("</b>");

                foreach (var instanceId in roster.GetOwnedIds(kind))
                {
                    if (roster.TryGetOwned(instanceId, out var asset)) ownedText.Append("  ").AppendLine(OwnedCaravanAssetNames.Format(asset));
                }
            }

            elements.OwnedListLabel.text = ownedText.ToString();
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
                elements.ReasonLabel.text = string.Empty;
                elements.ActionButton.interactable = false;
                return;
            }

            elements.InfoName.text = candidate.Name;
            elements.InfoKind.text = candidate.KindLabel;
            elements.InfoPrice.text = $"가격 {candidate.Price:N0}";

            var check = purchaseService.Evaluate(candidate);
            elements.ActionButton.interactable = check == CaravanAssetPurchaseCheck.Available;
            elements.ReasonLabel.text = check switch
            {
                CaravanAssetPurchaseCheck.InsufficientFunds => InsufficientFundsText,
                CaravanAssetPurchaseCheck.OwnedFull => OwnedFullText,
                CaravanAssetPurchaseCheck.AlreadyOwned => AlreadyOwnedText,
                _ => string.Empty,
            };
        }
    }
}

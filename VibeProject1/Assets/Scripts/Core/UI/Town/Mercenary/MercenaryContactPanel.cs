using System;
using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 용병단 접촉(캐릭터 고용) 화면(Docs/기획/53번, 설계 54번 §8). 무역품 구매 화면과 같이 모달 팝업 채널에 등록된다 - 재화 패널 외
    /// Hub UI 숨김과 "나가면 주점 지구로 복귀"를 기존 채널이 처리한다. MonoBehaviour가 아닌 plain C#이며 HubUIWiring이 Hub 로드마다
    /// 새로 만든다 - 골드 보유·로스터(Bootstrap 상주) 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// 화면 요소와 후보 목록 처리는 마구간과 공유한다(RosterShopElements/RosterShopCandidateList, 설계 56번 §7.1). 좌측 보유 목록은
    /// 텍스트 블록 하나로 그린다(설계 54번 §11) - 직업 수가 바뀌어도 레이아웃을 고치지 않기 위해서다.
    /// </summary>
    public sealed class MercenaryContactPanel : IUIPanel, IDisposable
    {
        private const string EmptyCandidateText = "고용 가능한 용병 없음";
        private const string InsufficientFundsText = "재화가 부족합니다.";
        private const string AlreadyHiredText = "이미 고용한 용병입니다.";

        private readonly RosterShopElements elements;
        private readonly IGoldSpender gold;
        private readonly IMercenaryCandidateReader candidateReader;
        private readonly IHiredCharacterRoster roster;
        private readonly ICharacterCatalogReader catalog;
        private readonly IMercenaryClassIconReader iconReader;
        private readonly ITripCurrentLocationReader currentLocation; // 선택적 - 없으면 마을 Id 0(지금은 무시되는 값)
        private readonly MercenaryHiringService hiringService;
        private readonly RosterShopCandidateList candidateList;
        private readonly StringBuilder ownedText = new();

        private IReadOnlyList<CharacterProfile> candidates = Array.Empty<CharacterProfile>();
        private int selectedIndex = -1;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.MercenaryContact);

        public MercenaryContactPanel(RosterShopElements elements, IGoldSpender gold, IMercenaryCandidateReader candidateReader, IHiredCharacterRoster roster, ICharacterCatalogReader catalog, IMercenaryClassIconReader iconReader, ITripCurrentLocationReader currentLocation, IUIManager uiManager)
        {
            this.elements = elements;
            this.gold = gold;
            this.candidateReader = candidateReader;
            this.roster = roster;
            this.catalog = catalog;
            this.iconReader = iconReader;
            this.currentLocation = currentLocation;
            hiringService = new MercenaryHiringService(gold, roster);
            candidateList = new RosterShopCandidateList(elements);

            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 카테고리 depth 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.ActionButton.onClick.RemoveAllListeners();
            elements.ActionButton.onClick.AddListener(Hire);

            gold.Changed += HandleCurrencyChanged;
            roster.OnHiredChanged += HandleRosterChanged;
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
            gold.Changed -= HandleCurrencyChanged;
            roster.OnHiredChanged -= HandleRosterChanged;
        }

        private void HandleCurrencyChanged()
        {
            if (isOpen) UpdateInfo();
        }

        // 고용한 후보는 목록에서 사라지므로 선택을 해제하고 전부 다시 그린다(설계 54번 §8.3).
        private void HandleRosterChanged()
        {
            if (isOpen) Refresh();
        }

        private void Refresh()
        {
            var cityId = currentLocation?.CurrentCityId ?? 0;
            candidates = candidateReader.GetCandidates(cityId, TownFacilityIds.MercenaryContact);
            selectedIndex = -1;

            RenderOwned();
            candidateList.Render(candidates.Count, BindRow, EmptyCandidateText);
            UpdateInfo();
        }

        private void BindRow(RosterShopCandidateRowView row, int index)
        {
            var candidate = candidates[index];
            row.Bind(index, iconReader?.GetClassIcon(candidate.MercenaryClass), candidate.Name, candidate.ClassLabel, candidate.HireCost, Select);
        }

        private void Hire()
        {
            if (!TryGetSelected(out var candidate)) return;

            hiringService.TryHire(candidate); // 성공 시 로스터·재화 이벤트로 화면이 갱신된다.
            UpdateInfo();
        }

        // 직업별 제목("궁수 2/5")과 보유 캐릭터 이름. 직업 순서와 이름 순서는 테이블 행 순서를 따른다.
        private void RenderOwned()
        {
            ownedText.Clear();
            var classOrder = new List<string>();
            foreach (var profile in catalog.All)
            {
                if (!classOrder.Contains(profile.MercenaryClass)) classOrder.Add(profile.MercenaryClass);
            }

            foreach (var mercenaryClass in classOrder)
            {
                var classLabel = catalog.TryGetClassLabel(mercenaryClass, out var label) ? label : mercenaryClass;
                if (ownedText.Length > 0) ownedText.AppendLine();
                ownedText.Append("<b>").Append(classLabel).Append(' ')
                    .Append(roster.CountHiredOfClass(mercenaryClass)).AppendLine("</b>");

                foreach (var profile in catalog.All)
                {
                    if (profile.MercenaryClass == mercenaryClass && roster.IsHired(profile.CharacterId))
                    {
                        ownedText.Append("  ").AppendLine(profile.Name);
                    }
                }
            }

            elements.OwnedListLabel.text = ownedText.ToString();
        }

        private void Select(RosterShopCandidateRowView row)
        {
            selectedIndex = row.Index;
            UpdateInfo();
        }

        private bool TryGetSelected(out CharacterProfile candidate)
        {
            var valid = selectedIndex >= 0 && selectedIndex < candidates.Count;
            candidate = valid ? candidates[selectedIndex] : null;
            return valid;
        }

        // 선택한 후보의 정보와 고용 가능 여부를 다시 그린다. 선택·재화·로스터가 바뀔 때마다 호출된다(기획 53번 §4.2).
        private void UpdateInfo()
        {
            candidateList.SetSelected(selectedIndex);

            if (!TryGetSelected(out var candidate))
            {
                elements.InfoName.text = string.Empty;
                elements.InfoKind.text = string.Empty;
                elements.InfoDetail.text = string.Empty;
                elements.InfoPrice.text = string.Empty;
                elements.ReasonLabel.text = string.Empty;
                elements.ActionButton.interactable = false;
                return;
            }

            var stats = candidate.Stats;
            elements.InfoName.text = candidate.Name;
            elements.InfoKind.text = candidate.ClassLabel;
            elements.InfoDetail.text = $"최대 HP {stats.MaxHp:0.##}\n공격력 {stats.Attack:0.##}\n방어력 {stats.Defense:0.##}\n사거리 {stats.Range:0.##}";
            elements.InfoPrice.text = $"고용비 {candidate.HireCost:N0}";

            var check = hiringService.Evaluate(candidate);
            elements.ActionButton.interactable = check == MercenaryHireCheck.Available;
            elements.ReasonLabel.text = check switch
            {
                MercenaryHireCheck.InsufficientFunds => InsufficientFundsText,
                MercenaryHireCheck.AlreadyHired => AlreadyHiredText,
                _ => string.Empty,
            };
        }
    }
}

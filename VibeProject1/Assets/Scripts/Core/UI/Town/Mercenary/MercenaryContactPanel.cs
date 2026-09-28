using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 용병단 접촉(캐릭터 고용) 화면(Docs/기획/53번, 설계 54번 §8). 무역품 구매 화면과 같이 모달 팝업 채널에 등록된다 - 재화 패널 외
    /// Hub UI 숨김과 "나가면 주점 지구로 복귀"를 기존 채널이 처리한다. MonoBehaviour가 아닌 plain C#이며 HubUIWiring이 Hub 로드마다
    /// 새로 만든다 - 재화 지갑·로스터(Bootstrap 상주) 이벤트를 구독하므로 교체 시 반드시 Dispose한다.
    /// 좌측 보유 목록은 텍스트 블록 하나로 그린다(설계 54번 §11) - 하드코딩 방침(ui.md §1-9) 아래에서 직업 수가 바뀌어도 레이아웃을
    /// 고치지 않기 위해서다.
    /// </summary>
    public sealed class MercenaryContactPanel : IUIPanel, IDisposable
    {
        private const string EmptyCandidateText = "고용 가능한 용병 없음";
        private const string InsufficientFundsText = "재화가 부족합니다.";
        private const string RosterFullText = "해당 직업은 더 고용할 수 없습니다.(최대 5명)";
        private const string AlreadyHiredText = "이미 고용한 용병입니다.";
        private const int VisibleCandidateRows = 6; // 목록 영역에 한 번에 보이는 줄 수 - 줄 높이를 영역 높이 비율로 정한다(잠정)

        private readonly MercenaryContactElements elements;
        private readonly IPlayerCurrencyWallet wallet;
        private readonly IMercenaryCandidateReader candidateReader;
        private readonly IHiredCharacterRoster roster;
        private readonly ICharacterCatalogReader catalog;
        private readonly IMercenaryClassIconReader iconReader;
        private readonly ITripCurrentLocationReader currentLocation; // 선택적 - 없으면 마을 Id 0(지금은 무시되는 값)
        private readonly MercenaryHiringService hiringService;
        private readonly List<MercenaryCandidateRowView> rowViews = new();
        private readonly StringBuilder ownedText = new();

        private IReadOnlyList<CharacterProfile> candidates = Array.Empty<CharacterProfile>();
        private int selectedIndex = -1;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.MercenaryContact);

        public MercenaryContactPanel(MercenaryContactElements elements, IPlayerCurrencyWallet wallet, IMercenaryCandidateReader candidateReader, IHiredCharacterRoster roster, ICharacterCatalogReader catalog, IMercenaryClassIconReader iconReader, ITripCurrentLocationReader currentLocation, IUIManager uiManager)
        {
            this.elements = elements;
            this.wallet = wallet;
            this.candidateReader = candidateReader;
            this.roster = roster;
            this.catalog = catalog;
            this.iconReader = iconReader;
            this.currentLocation = currentLocation;
            hiringService = new MercenaryHiringService(wallet, roster);

            elements.CandidateRowTemplate.gameObject.SetActive(false);

            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 카테고리 depth 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.HireButton.onClick.RemoveAllListeners();
            elements.HireButton.onClick.AddListener(Hire);

            wallet.OnAmountChanged += HandleCurrencyChanged;
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
            wallet.OnAmountChanged -= HandleCurrencyChanged;
            roster.OnHiredChanged -= HandleRosterChanged;
        }

        private void HandleCurrencyChanged(int _)
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
            RenderCandidates();
            UpdateInfo();
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
                    .Append(roster.CountHiredOfClass(mercenaryClass)).Append('/').Append(MercenaryHiringService.MaxOwnedPerClass).AppendLine("</b>");

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

        private void RenderCandidates()
        {
            var viewportHeight = Mathf.Max(elements.CandidateViewport.rect.height, 1f);
            var rowHeight = viewportHeight / VisibleCandidateRows;

            for (var i = 0; i < candidates.Count; i++)
            {
                if (i >= rowViews.Count) rowViews.Add(UnityEngine.Object.Instantiate(elements.CandidateRowTemplate, elements.CandidateContent));

                var row = rowViews[i];
                row.gameObject.SetActive(true);
                row.Bind(i, candidates[i], iconReader?.GetClassIcon(candidates[i].MercenaryClass), Select);
                var rect = row.RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -i * rowHeight);
                rect.sizeDelta = new Vector2(0f, rowHeight);
            }

            for (var i = candidates.Count; i < rowViews.Count; i++) rowViews[i].gameObject.SetActive(false);

            elements.CandidateContent.sizeDelta = new Vector2(elements.CandidateContent.sizeDelta.x, candidates.Count * rowHeight);
            elements.EmptyCandidateLabel.gameObject.SetActive(candidates.Count == 0);
            elements.EmptyCandidateLabel.text = EmptyCandidateText;
        }

        private void Select(MercenaryCandidateRowView row)
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
            for (var i = 0; i < rowViews.Count; i++) rowViews[i].SetSelected(i == selectedIndex);

            if (!TryGetSelected(out var candidate))
            {
                elements.InfoName.text = string.Empty;
                elements.InfoClass.text = string.Empty;
                elements.InfoStats.text = string.Empty;
                elements.InfoCost.text = string.Empty;
                elements.ReasonLabel.text = string.Empty;
                elements.HireButton.interactable = false;
                return;
            }

            var stats = candidate.Stats;
            elements.InfoName.text = candidate.Name;
            elements.InfoClass.text = candidate.ClassLabel;
            elements.InfoStats.text = $"최대 HP {stats.MaxHp:0.##}\n공격력 {stats.Attack:0.##}\n방어력 {stats.Defense:0.##}\n사거리 {stats.Range:0.##}";
            elements.InfoCost.text = $"고용비 {candidate.HireCost:N0}";

            var check = hiringService.Evaluate(candidate);
            elements.HireButton.interactable = check == MercenaryHireCheck.Available;
            elements.ReasonLabel.text = check switch
            {
                MercenaryHireCheck.InsufficientFunds => InsufficientFundsText,
                MercenaryHireCheck.RosterFull => RosterFullText,
                MercenaryHireCheck.AlreadyHired => AlreadyHiredText,
                _ => string.Empty,
            };
        }
    }
}

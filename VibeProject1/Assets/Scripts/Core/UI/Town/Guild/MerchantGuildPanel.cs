using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상인조합 화면(기획 94 4-14~4-17, Docs/설계/103번 §4.5) - 좌측 의뢰 목록 + 우측 상세·수락. 시설 화면 방식이라 시설 화면 채널에 등록되고
    /// HubUIWiring이 Hub 로드마다 새로 만든다 - 게시판(Bootstrap 상주) 변경을 구독하므로 교체 시 Dispose한다.
    /// 수락은 확인 창 없이 게시판에 요청하고, 게시판 변경 알림으로 목록·상세를 다시 그린다(수락 상태의 출처는 게시판 하나).
    /// </summary>
    public sealed class MerchantGuildPanel : IUIPanel, IDisposable
    {
        private const string EmptyText = "수락 가능한 의뢰 없음";
        private const string AcceptedText = "수락함";
        private const int VisibleRows = 8; // 목록 영역에 한 번에 보이는 줄 수(잠정)

        private readonly MerchantGuildElements elements;
        private readonly IGuildQuestBoard board;
        private readonly List<GuildQuestRowView> rows = new();

        private string selectedId;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.MerchantGuild);

        public MerchantGuildPanel(MerchantGuildElements elements, IGuildQuestBoard board, IPanelNavigator uiManager)
        {
            this.elements = elements;
            this.board = board;

            elements.RowTemplate.gameObject.SetActive(false);
            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 카테고리 depth 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            elements.AcceptButton.onClick.RemoveAllListeners();
            elements.AcceptButton.onClick.AddListener(Accept);

            board.Changed += HandleBoardChanged;
            elements.Root.SetActive(false);
        }

        public void Open()
        {
            isOpen = true;
            selectedId = null;
            elements.Root.SetActive(true);
            Refresh();
        }

        // Close()는 표시/숨김만 한다. 나가기 버튼은 이 메서드를 직접 부르지 않고 UIManager.Close(PanelId)에 위임한다.
        public void Close()
        {
            isOpen = false;
            elements.Root.SetActive(false);
        }

        public void Dispose() => board.Changed -= HandleBoardChanged;

        private void HandleBoardChanged()
        {
            if (isOpen) Refresh();
        }

        private void Refresh()
        {
            var quests = board.Quests;
            if (selectedId != null && FindQuest(selectedId) == null) selectedId = null;

            var rowHeight = FixedRowListLayout.RowHeight(elements.ListViewport.rect.height, VisibleRows);
            FixedRowListLayout.Render(rows, quests.Count, elements.RowTemplate, elements.ListContent, rowHeight, (row, i) =>
            {
                var quest = quests[i];
                row.Bind(quest.Title, quest.Requester, board.IsAccepted(quest.Id) ? AcceptedText : string.Empty, () => Select(quest.Id));
                row.SetSelected(quest.Id == selectedId);
            });
            elements.EmptyLabel.text = quests.Count == 0 ? EmptyText : string.Empty;

            RenderDetail();
        }

        private void RenderDetail()
        {
            var quest = selectedId != null ? FindQuest(selectedId) : null;
            if (quest == null)
            {
                elements.DetailTitle.text = string.Empty;
                elements.DetailDescription.text = string.Empty;
                elements.DetailReward.text = string.Empty;
                elements.StatusLabel.text = string.Empty;
                elements.AcceptButton.interactable = false;
                return;
            }

            var accepted = board.IsAccepted(quest.Id);
            elements.DetailTitle.text = quest.Title;
            elements.DetailDescription.text = quest.Description;
            elements.DetailReward.text = "보상 " + quest.Reward;
            elements.StatusLabel.text = accepted ? AcceptedText : string.Empty;
            elements.AcceptButton.interactable = !accepted;
        }

        private void Select(string questId)
        {
            selectedId = questId;
            Refresh();
        }

        private void Accept()
        {
            if (selectedId != null) board.TryAccept(selectedId); // 성공하면 Changed로 다시 그려진다.
        }

        private GuildQuestInfo FindQuest(string questId)
        {
            foreach (var quest in board.Quests)
            {
                if (quest.Id == questId) return quest;
            }
            return null;
        }
    }
}

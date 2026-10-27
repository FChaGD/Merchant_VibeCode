using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// 상인조합 화면(기획 94 4-14~4-17·기획 106 §4.1, Docs/설계/103번 §4.5·107번 §11.2) - 좌측 의뢰 목록 + 우측 상세(목표·기한·보상)와 수락·포기·보고.
    /// 퀘스트 목록 UI가 생기기 전까지 진행 확인·포기·보고를 여기서 한다(기획 106 §4.1). 시설 화면 방식이라 시설 화면 채널에 등록되고 HubUIWiring이 Hub 로드마다
    /// 새로 만든다 - 게시판(Bootstrap 상주) 변경을 구독하므로 교체 시 Dispose한다.
    /// 수락·보고는 확인 창 없이 게시판에 요청하고, 포기는 불이익이 있는 되돌릴 수 없는 조작이라 확인 창을 거친다(설계 107번 §13-14). 상태의 출처는 게시판 하나 -
    /// 변경 알림으로 목록·상세를 다시 그린다.
    /// </summary>
    public sealed class MerchantGuildPanel : IUIPanel, IDisposable
    {
        private const string EmptyText = "수락 가능한 의뢰 없음";
        private const string ActiveText = "진행 중";
        private const string ReportableText = "보고 가능";
        private const string AbandonMessage = "포기하면 퀘스트가 실패로 처리됩니다.";
        private const int VisibleRows = 8; // 목록 영역에 한 번에 보이는 줄 수(잠정)

        private readonly MerchantGuildElements elements;
        private readonly IGuildQuestBoard board;
        private readonly ITooltipHandle acceptTooltip;
        private readonly ITooltipHandle abandonTooltip;
        private readonly ITooltipHandle reportTooltip;
        private readonly List<GuildQuestRowView> rows = new();

        private string selectedId;
        private bool isOpen;

        public string PanelId => UIPanelIds.Facility(TownFacilityIds.MerchantGuild);

        public MerchantGuildPanel(MerchantGuildElements elements, IGuildQuestBoard board, IPanelNavigator uiManager,
            ITooltipHandle acceptTooltip, ITooltipHandle abandonTooltip, ITooltipHandle reportTooltip)
        {
            this.elements = elements;
            this.board = board;
            this.acceptTooltip = acceptTooltip;
            this.abandonTooltip = abandonTooltip;
            this.reportTooltip = reportTooltip;

            elements.RowTemplate.gameObject.SetActive(false);
            // 나가기는 패널이 자기 Close()를 부르지 않고 UIManager에 위임한다 - 카테고리 depth 복귀가 함께 처리된다.
            elements.ExitButton.onClick.RemoveAllListeners();
            elements.ExitButton.onClick.AddListener(() => uiManager.Close(PanelId));
            Listen(elements.AcceptButton, Accept);
            Listen(elements.AbandonButton, ConfirmAbandon);
            Listen(elements.ReportButton, Report);

            board.Changed += HandleBoardChanged;
            elements.ConfirmDialog.Hide();
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
            elements.ConfirmDialog.Hide();
            elements.Root.SetActive(false);
        }

        public void Dispose()
        {
            board.Changed -= HandleBoardChanged;
            acceptTooltip?.Dispose();
            abandonTooltip?.Dispose();
            reportTooltip?.Dispose();
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        private void HandleBoardChanged()
        {
            if (isOpen) Refresh();
        }

        private void Refresh()
        {
            var quests = board.Quests;
            if (selectedId != null && FindQuest(quests, selectedId) == null) selectedId = null;

            var rowHeight = FixedRowListLayout.RowHeight(elements.ListViewport.rect.height, VisibleRows);
            FixedRowListLayout.Render(rows, quests.Count, elements.RowTemplate, elements.ListContent, rowHeight, (row, i) =>
            {
                var quest = quests[i];
                row.Bind(quest.Title, quest.Requester, StatusText(quest.Status), () => Select(quest.Id));
                row.SetSelected(quest.Id == selectedId);
            });
            elements.EmptyLabel.text = quests.Count == 0 ? EmptyText : string.Empty;

            RenderDetail();
        }

        private void RenderDetail()
        {
            var detail = selectedId != null ? board.GetDetail(selectedId) : null;
            if (detail == null)
            {
                SetTexts(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
                SetButton(elements.AcceptButton, acceptTooltip, false, false, null);
                SetButton(elements.AbandonButton, abandonTooltip, false, false, null);
                SetButton(elements.ReportButton, reportTooltip, false, false, null);
                return;
            }

            SetTexts(detail.Title, detail.Description, detail.Objectives, "기한 " + detail.Deadline, "보상 " + detail.Reward, StatusText(detail.Status));
            var accepted = detail.Status != GuildQuestStatus.Available;
            var showReport = accepted && detail.IsReportType;
            var canAccept = board.CanAccept(selectedId, out var acceptReason);
            var canAbandon = board.CanAbandon(selectedId, out var abandonReason);
            var canReport = board.CanReport(selectedId, out var reportReason);
            SetButton(elements.AcceptButton, acceptTooltip, !accepted, canAccept, acceptReason);
            SetButton(elements.AbandonButton, abandonTooltip, accepted, canAbandon, abandonReason);
            SetButton(elements.ReportButton, reportTooltip, showReport, canReport, reportReason);
        }

        private void SetTexts(string title, string description, string objectives, string deadline, string reward, string status)
        {
            elements.DetailTitle.text = title;
            elements.DetailDescription.text = description;
            elements.DetailObjectives.text = objectives;
            elements.DetailDeadline.text = deadline;
            elements.DetailReward.text = reward;
            elements.StatusLabel.text = status;
        }

        // 보이지 않는 버튼은 사유도 지운다 - 숨은 버튼의 툴팁이 남지 않게.
        private static void SetButton(Button button, ITooltipHandle tooltip, bool visible, bool interactable, string reason)
        {
            button.gameObject.SetActive(visible);
            button.interactable = interactable;
            tooltip?.SetText(visible && !interactable ? reason : null);
        }

        private static string StatusText(GuildQuestStatus status) => status switch
        {
            GuildQuestStatus.Active => ActiveText,
            GuildQuestStatus.Reportable => ReportableText,
            _ => string.Empty,
        };

        private void Select(string questId)
        {
            selectedId = questId;
            Refresh();
        }

        private void Accept()
        {
            if (selectedId != null) board.TryAccept(selectedId); // 성공하면 Changed로 다시 그려진다.
        }

        private void Report()
        {
            if (selectedId != null) board.TryReport(selectedId);
        }

        private void ConfirmAbandon()
        {
            if (selectedId == null) return;
            var questId = selectedId;
            var penalty = board.GetDetail(questId)?.Penalty;
            var message = penalty != null ? $"{AbandonMessage}\n불이익: {penalty}" : AbandonMessage;
            elements.ConfirmDialog.Show(message, "포기", "취소", () => board.TryAbandon(questId), null);
        }

        private static GuildQuestInfo FindQuest(IReadOnlyList<GuildQuestInfo> quests, string questId)
        {
            foreach (var quest in quests)
            {
                if (quest.Id == questId) return quest;
            }
            return null;
        }
    }
}

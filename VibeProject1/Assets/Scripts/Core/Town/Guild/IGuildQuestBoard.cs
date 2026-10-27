using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>게시판 행 상태(Docs/설계/107번 §11.1). Available = 이 조합 게시, Active = 이 조합에서 수락, Reportable = 이 조합이 보고처.</summary>
    public enum GuildQuestStatus { Available, Active, Reportable }

    /// <summary>상인조합 게시판의 의뢰 1건(목록 행).</summary>
    public sealed class GuildQuestInfo
    {
        public GuildQuestInfo(string id, string title, string requester, GuildQuestStatus status)
        {
            Id = id;
            Title = title;
            Requester = requester;
            Status = status;
        }

        public string Id { get; }
        public string Title { get; }
        public string Requester { get; }
        public GuildQuestStatus Status { get; }
    }

    /// <summary>우측 상세에 그릴 문구 묶음. 문구 조립은 게시판이 하고 화면은 그대로 쓴다.</summary>
    public sealed class GuildQuestDetail
    {
        public string Title;
        public string Requester;
        public string Description;
        public string Objectives;
        public string Deadline;
        public string Reward;
        public GuildQuestStatus Status;
        public bool IsReportType;
        /// <summary>포기 확인 창의 불이익 문구. 없으면 null.</summary>
        public string Penalty;
    }

    /// <summary>
    /// 상인조합 의뢰 게시판(Docs/설계/103번 §4.1 → 107번 §11.1 확장). 화면은 현재 마을 기준 목록·상세·버튼 사유를 읽고 수락·포기·보고만 요청한다 -
    /// 목표·진행·보상 처리는 퀘스트 시스템(IQuestActions) 몫.
    /// </summary>
    public interface IGuildQuestBoard
    {
        IReadOnlyList<GuildQuestInfo> Quests { get; }
        GuildQuestDetail GetDetail(string questId);
        bool CanAccept(string questId, out string reason);
        bool CanAbandon(string questId, out string reason);
        bool CanReport(string questId, out string reason);
        bool TryAccept(string questId);
        bool TryAbandon(string questId);
        bool TryReport(string questId);
        event Action Changed;
    }
}

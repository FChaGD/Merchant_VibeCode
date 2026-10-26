using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>상인조합 게시판의 의뢰 1건(표시용, Docs/설계/103번 §4.1).</summary>
    public sealed class GuildQuestInfo
    {
        public GuildQuestInfo(string id, string title, string requester, string description, string reward)
        {
            Id = id;
            Title = title;
            Requester = requester;
            Description = description;
            Reward = reward;
        }

        public string Id { get; }
        public string Title { get; }
        public string Requester { get; }
        public string Description { get; }
        public string Reward { get; }
    }

    /// <summary>
    /// 상인조합 의뢰 게시판(Docs/설계/103번 §4.1). 화면은 목록·수락 여부를 읽고 수락만 요청한다 - 목표·진행·보상 처리는 퀘스트 시스템 몫.
    /// </summary>
    public interface IGuildQuestBoard
    {
        IReadOnlyList<GuildQuestInfo> Quests { get; }
        bool IsAccepted(string questId);
        /// <summary>수락한다. 없는 의뢰이거나 이미 수락했으면 false.</summary>
        bool TryAccept(string questId);
        event Action Changed;
    }
}

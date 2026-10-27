using System;
using System.Collections.Generic;

namespace Game.Core
{
    [Serializable]
    public sealed class QuestRepostEntry
    {
        public string QuestId;
        public int Day;
        public bool Never;
    }

    /// <summary>게시 상태(Docs/설계/107번 §5.2) - 성공 이력·재게시 일정·후속 해금. 저장 대비로 직렬화 가능한 값만 둔다.</summary>
    [Serializable]
    public sealed class QuestPostingState
    {
        public List<string> CompletedIds = new();
        public List<QuestRepostEntry> Reposts = new();
        public List<string> UnlockedIds = new();

        public QuestRepostEntry FindRepost(string questId)
        {
            foreach (var entry in Reposts)
            {
                if (entry.QuestId == questId) return entry;
            }
            return null;
        }

        public void SetRepost(string questId, int day, bool never)
        {
            var entry = FindRepost(questId);
            if (entry == null)
            {
                entry = new QuestRepostEntry { QuestId = questId };
                Reposts.Add(entry);
            }
            entry.Day = day;
            entry.Never = never;
        }
    }

    /// <summary>
    /// 게시 판정(Docs/설계/107번 §5.2, 기획 106 §4.1). 미충족 퀘스트는 목록에 나타나지 않는다. 이번 구현은 의뢰 수락형 일반 퀘스트만 게시한다 - 메인·트리거형·이벤트형은
    /// 구조만 있다(§6.5). 재게시는 "그 날 이후"(Day ≤ 오늘)라 날짜가 여러 날 한 번에 넘어가도 빠지지 않는다.
    /// </summary>
    public static class QuestBoardRules
    {
        public static bool IsPosted(QuestDefinition def, QuestPostingState posting, int cityId, int today, int reputation, Func<string, bool> isActive)
        {
            if (def.Acceptance != QuestAcceptance.Request || def.Kind != QuestKind.General) return false;
            if (!def.BoardCityIds.Contains(cityId) || isActive(def.Id)) return false;
            if (def.Repeat == QuestRepeat.Once && posting.CompletedIds.Contains(def.Id)) return false;

            var repost = posting.FindRepost(def.Id);
            if (repost != null && (repost.Never || repost.Day > today)) return false;

            if (!string.IsNullOrEmpty(def.RequiredQuestId) && !posting.CompletedIds.Contains(def.RequiredQuestId) && !posting.UnlockedIds.Contains(def.Id)) return false;
            if (def.HasMinReputation && reputation < def.MinReputation) return false;
            return true;
        }
    }
}

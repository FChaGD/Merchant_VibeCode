using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상인조합 게시판 Placeholder(기획 94 4-15·4-16) - 의뢰 3건(제목만 번호, 나머지 "값 없음"), 수락 상태는 실행 중 메모리에만 둔다.
    /// 실제 시스템 설계 후 대체/제거 대상(퀘스트 시스템·저장).
    /// </summary>
    public class PlaceholderGuildQuestBoard : MonoBehaviour, IGuildQuestBoard, IManagedComponent
    {
        private const string MissingText = "값 없음";
        private const int QuestCount = 3;

        private readonly List<GuildQuestInfo> quests = new();
        private readonly HashSet<string> accepted = new();

        public IReadOnlyList<GuildQuestInfo> Quests => quests;
        public event Action Changed;

        public void RegisterSelf(IDependencyRegistrar registrar) => registrar.Register<IGuildQuestBoard>(this);

        // registrar가 null이어도(EditMode 테스트) 같은 목록으로 시작한다.
        public void ResolveDependencies(IDependencyResolver registrar)
        {
            quests.Clear();
            accepted.Clear();
            for (var i = 1; i <= QuestCount; i++)
            {
                quests.Add(new GuildQuestInfo($"PlaceholderQuest{i}", $"퀘스트이름{i}", MissingText, MissingText, MissingText));
            }
        }

        public bool IsAccepted(string questId) => questId != null && accepted.Contains(questId);

        public bool TryAccept(string questId)
        {
            if (questId == null || !quests.Exists(q => q.Id == questId) || !accepted.Add(questId)) return false;

            Changed?.Invoke();
            return true;
        }
    }
}

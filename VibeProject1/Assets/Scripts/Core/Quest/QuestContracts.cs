using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum QuestStartSource { Request, Trigger, Event }

    /// <summary>퀘스트 상태 조회(Docs/설계/107번 §5) - 게시판 표시용. Active = 진행 중·보고 가능 인스턴스.</summary>
    public interface IQuestReader
    {
        IReadOnlyList<QuestInstance> Active { get; }
        bool TryGetDefinition(string questId, out QuestDefinition definition);
        bool TryGetInstance(string questId, out QuestInstance instance);
        /// <summary>그 도시 게시판에 수락 가능하게 걸린 퀘스트(게시 판정 §5.2, 정의 순서).</summary>
        IReadOnlyList<QuestDefinition> PostedAt(int cityId);
        int Today { get; }
        /// <summary>그 품목의 보유 개수(적재 + 임시 보관) - 퀘스트 판정과 같은 기준으로 진행 표시를 그리게 한다.</summary>
        int CountOwned(string itemId);
        event Action Changed;
    }

    /// <summary>
    /// 퀘스트 시작 진입점(설계 107번 §6.5) - 게시판은 Request로 부른다. 트리거형·이벤트형은 같은 진입점을 쓰도록 열어 두고 이번에는 연결하지 않는다.
    /// </summary>
    public interface IQuestStarter
    {
        bool TryStart(string questId, QuestStartSource source, int cityId, out string reason);
    }

    /// <summary>게시판 조작(설계 107번 §11.1) - 수락·포기·보고와 버튼 활성 사유.</summary>
    public interface IQuestActions : IQuestReader, IQuestStarter
    {
        bool CanAccept(string questId, int cityId, out string reason);
        bool CanAbandon(string questId, out string reason);
        bool TryAbandon(string questId, out string reason);
        bool CanReport(string questId, int cityId, out string reason);
        bool TryReport(string questId, int cityId, out string reason);
    }

    /// <summary>
    /// 보상이 임시 보관으로 갔다는 알림(설계 107번 §8.3) - 상태 저장소는 화면을 모르고, 씬 배선이 이걸 듣고 정리 화면을 연다.
    /// 정리하지 못한 표시는 다음 상행 시작에 지워진다.
    /// </summary>
    public interface IQuestRewardCleanupSource
    {
        bool HasPendingCleanup { get; }
        void ClearPendingCleanup();
        event Action RewardStaged;
    }

    /// <summary>의뢰 화물 버리기 경고 조회(설계 107번 §8.4) - 아이템 Id 중 진행 중 퀘스트의 지급 화물이 있으면 그 퀘스트 이름들(중복 제거·순서 유지).</summary>
    public interface IQuestCargoWarnings
    {
        IReadOnlyList<string> Describe(IEnumerable<string> itemIds);
    }

    /// <summary>
    /// 퀘스트 난이도 가산(설계 107번 §9.2) - 상행 계획(예상값)과 구간 실행이 같은 값을 읽는다. 목적지 = 이번 상행의 최종 목적지.
    /// </summary>
    public interface IQuestDifficultyModifier
    {
        int GetBonus(int destinationCityId);
        event Action BonusChanged;
    }

    /// <summary>이벤트 조건 키 `Quest:퀘스트Id`의 판정(설계 107번 §9.3) - 판정 순간의 구간 진행률을 받는다.</summary>
    public interface IQuestEncounterGate
    {
        bool IsEventDue(string questId, float legProgress);
    }
}

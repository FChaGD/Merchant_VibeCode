using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum QuestKind { Main, General }
    public enum QuestRepeat { Once, Repeatable }
    public enum QuestAcceptance { Request, Trigger, Event }
    public enum QuestCompletion { Instant, Report }
    public enum QuestObjectiveRole { Core, Bonus }
    public enum QuestObjectiveType { Arrive, Deliver, Deadline, BattleWin, Preserve }
    public enum QuestRewardType { Gold, Reputation, Item, Unit }
    public enum QuestBonusTiming { OnSuccess, Immediate }
    public enum QuestEffectScope { Destination, AllTrips }
    public enum QuestEventRepeat { PerQuest, PerTrip }
    public enum QuestEventTiming { Progress, Random }
    public enum QuestFollowUpMode { Board, AutoAccept }

    /// <summary>
    /// 퀘스트 1건의 정의(Docs/설계/107번 §4). QuestTableImporter가 Quest.xlsx 7개 시트를 퀘스트 단위로 묶어 채운다 - 시트를 그대로 들고 다니면
    /// 소비자마다 퀘스트 Id로 다시 조인해야 해서 묶음으로 둔다. 선택 값은 Has 플래그로 "빈칸"과 0을 구분한다(최소 평판 0 ≠ 조건 없음).
    /// </summary>
    [Serializable]
    public sealed class QuestDefinition
    {
        public string Id;
        public QuestKind Kind;
        public QuestRepeat Repeat;
        public QuestAcceptance Acceptance;
        public QuestCompletion Completion;
        public int ReportCityId;
        public List<int> BoardCityIds = new();
        public string RequiredQuestId;
        public bool HasMinReputation;
        public int MinReputation;
        public bool HasRepostDays;
        public int RepostDays;
        public int FailReputation;
        public int FailGold;
        public bool ReturnCargoOnFail;
        public QuestBonusTiming BonusTiming;
        public string Title;
        public string Requester;
        public string Description;
        public List<QuestObjectiveDefinition> Objectives = new();
        public List<QuestRewardDefinition> Rewards = new();
        public List<QuestCargoDefinition> Cargo = new();
        public List<QuestEffectDefinition> Effects = new();
        public List<QuestFollowUpDefinition> FollowUps = new();

        public bool HasGivenCargo => Cargo.Count > 0;

        public QuestObjectiveDefinition FindObjective(string objectiveId)
        {
            foreach (var objective in Objectives)
            {
                if (objective.Id == objectiveId) return objective;
            }
            return null;
        }
    }

    [Serializable]
    public sealed class QuestObjectiveDefinition
    {
        public string Id;
        public QuestObjectiveRole Role;
        public QuestObjectiveType Type;
        public int CityId;
        public string ItemId;
        public int Count;
        public string EnemyType;
        public bool QuestBattleOnly;
        public bool HasDeadlineDays;
        public int DeadlineDays;
        public bool HasDeadlineDate;
        public int DeadlineTotalDays;
        public string Label;
    }

    /// <summary>ObjectiveId가 비었으면 성공 보상, 값이 있으면 그 보조 목표의 추가 보상.</summary>
    [Serializable]
    public sealed class QuestRewardDefinition
    {
        public string ObjectiveId;
        public QuestRewardType Type;
        public string ItemId;
        public int Amount;
    }

    /// <summary>수락 시 지급하는 의뢰 화물(무역품 테이블의 TradeKind = QuestCargo 행).</summary>
    [Serializable]
    public sealed class QuestCargoDefinition
    {
        public string ItemId;
        public int Count;
    }

    [Serializable]
    public sealed class QuestEffectDefinition
    {
        public QuestEffectScope Scope;
        public int DifficultyBonus;
        public string EventId;
        public QuestEventRepeat EventRepeat;
        public QuestEventTiming EventTiming;
        public float ProgressMin;
        public float ProgressMax;
    }

    [Serializable]
    public sealed class QuestFollowUpDefinition
    {
        public string NextQuestId;
        public QuestFollowUpMode Mode;
        public string RequiredBonusId;
    }
}

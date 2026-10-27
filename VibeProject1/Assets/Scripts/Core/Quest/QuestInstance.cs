using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Reportable = 보고형 퀘스트의 핵심 목표가 모두 충족된 상태(보고 대기). 그 뒤 셋은 종료 상태.</summary>
    public enum QuestState { Active, Reportable, Completed, Failed, Abandoned }

    /// <summary>목표 1개의 진행(Docs/설계/107번 §5.1). DeadlineDay -1 = 기한 없음. Impossible = 불가 또는 기한 만료(되돌릴 수 없음).</summary>
    [Serializable]
    public sealed class QuestObjectiveProgress
    {
        public string ObjectiveId;
        public int Value;
        public bool Met;
        public bool Impossible;
        public int DeadlineDay = -1;
    }

    /// <summary>
    /// 진행 중인 퀘스트 1건(Docs/설계/107번 §5.1). 저장 시스템에 대비해 직렬화 가능한 값만 둔다 - 정의는 Id로 다시 찾는다(설계 §5.3).
    /// </summary>
    [Serializable]
    public sealed class QuestInstance
    {
        public string QuestId;
        public QuestState State;
        public int AcceptedDay;
        public int SourceCityId;
        public int GivenCargoCount;
        public List<QuestObjectiveProgress> Objectives = new();
        public List<string> PaidBonusIds = new();
        public bool EventFiredThisQuest;
        public bool EventFiredThisTrip;
        public bool HasEventSpot;
        public int EventLegIndex;
        public float EventLegProgress;

        public QuestObjectiveProgress Progress(string objectiveId)
        {
            foreach (var p in Objectives)
            {
                if (p.ObjectiveId == objectiveId) return p;
            }
            return null;
        }
    }
}

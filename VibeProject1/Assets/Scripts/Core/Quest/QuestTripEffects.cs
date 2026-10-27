using System.Collections.Generic;
using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 퀘스트의 상행 영향(Docs/설계/107번 §9) - 난이도 가산 조회, 확정 사건 발생 위치 확정·발생 판정·발생 기록. QuestManager에서 분리했다(2026-10-27 검진 8) -
    /// 상태 전이와 달리 상행 계기·여정 상태만 보는 별도 축이라 상행 쪽 규칙이 늘어도 상태 저장소가 커지지 않게.
    /// 진행 목록·정의는 QuestManager의 것을 그대로 읽는다(복사하지 않음). 적용 대상은 진행 중(Active) 인스턴스만(§9.1).
    /// 가산 변경 알림은 "가산을 주는 진행 중 퀘스트 집합"이 실제로 바뀔 때만 낸다 - 날짜·인벤토리 변경마다 상행 난이도를 다시 계산하지 않게(검진 7).
    /// </summary>
    public sealed class QuestTripEffects
    {
        private readonly IReadOnlyList<QuestInstance> active;
        private readonly IReadOnlyDictionary<string, QuestDefinition> byId;
        private readonly QuestManagerSources sources;
        private string bonusKey = string.Empty;

        public QuestTripEffects(IReadOnlyList<QuestInstance> active, IReadOnlyDictionary<string, QuestDefinition> byId, QuestManagerSources sources)
        {
            this.active = active;
            this.byId = byId;
            this.sources = sources;
        }

        public int GetBonus(int destinationCityId)
        {
            var total = 0;
            foreach (var instance in active)
            {
                if (instance.State != QuestState.Active || !byId.TryGetValue(instance.QuestId, out var def)) continue;
                foreach (var e in def.Effects)
                {
                    if (AppliesTo(def, e, destinationCityId)) total += e.DifficultyBonus;
                }
            }
            return total;
        }

        /// <summary>가산 대상 집합이 직전 확인 때와 달라졌는지. 확인하면서 기준을 갱신한다.</summary>
        public bool RefreshBonusKey()
        {
            var builder = new StringBuilder();
            foreach (var instance in active)
            {
                if (instance.State != QuestState.Active || !byId.TryGetValue(instance.QuestId, out var def) || !HasDifficultyBonus(def)) continue;
                builder.Append(instance.QuestId).Append('\n');
            }
            var key = builder.ToString();
            if (key == bonusKey) return false;
            bonusKey = key;
            return true;
        }

        public bool IsEventDue(string questId, float legProgress)
        {
            var instance = Find(questId);
            if (instance == null || instance.State != QuestState.Active || !instance.HasEventSpot) return false;
            var def = byId[questId];
            var effect = EventEffect(def);
            if (effect == null || !AppliesTo(def, effect, CurrentDestination())) return false;
            if (effect.EventRepeat == QuestEventRepeat.PerQuest ? instance.EventFiredThisQuest : instance.EventFiredThisTrip) return false;
            var itinerary = sources.Itinerary;
            if (itinerary == null || !itinerary.HasItinerary || itinerary.CurrentLegIndex != instance.EventLegIndex) return false;
            return legProgress >= instance.EventLegProgress;
        }

        /// <summary>상행 시작 - 인스턴스마다 이번 상행의 발생 위치를 확정한다(구간 실행 시간이 바뀌어도 다시 계산하지 않음, §9.3).</summary>
        public void OnTripStarted()
        {
            var plan = sources.Itinerary?.CurrentPlan;
            var durations = new List<float>();
            if (plan != null)
            {
                foreach (var leg in plan.Legs) durations.Add(leg.DurationSeconds);
            }
            foreach (var instance in active)
            {
                instance.EventFiredThisTrip = false;
                var effect = EventEffect(byId[instance.QuestId]);
                if (effect == null)
                {
                    instance.HasEventSpot = false;
                    continue;
                }
                var spot = QuestEventPlacement.Resolve(effect.EventTiming, durations, effect.ProgressMin, effect.ProgressMax, sources.Random01);
                instance.HasEventSpot = true;
                instance.EventLegIndex = spot.LegIndex;
                instance.EventLegProgress = spot.LegProgress;
            }
        }

        public void OnEventStarted(string eventId)
        {
            if (eventId == null) return;
            foreach (var instance in active)
            {
                if (EventEffect(byId[instance.QuestId])?.EventId != eventId) continue;
                instance.EventFiredThisQuest = true;
                instance.EventFiredThisTrip = true;
            }
        }

        /// <summary>진행 중인 이벤트가 이 퀘스트의 확정 사건인지 - 퀘스트 전용 전투 승리 판정용.</summary>
        public bool IsQuestEvent(QuestDefinition def, string runningEventId)
            => runningEventId != null && EventEffect(def)?.EventId == runningEventId;

        private static QuestEffectDefinition EventEffect(QuestDefinition def)
        {
            foreach (var e in def.Effects)
            {
                if (!string.IsNullOrEmpty(e.EventId)) return e;
            }
            return null;
        }

        private static bool HasDifficultyBonus(QuestDefinition def)
        {
            foreach (var e in def.Effects)
            {
                if (e.DifficultyBonus != 0) return true;
            }
            return false;
        }

        private static bool AppliesTo(QuestDefinition def, QuestEffectDefinition effect, int destinationCityId)
        {
            if (effect.Scope == QuestEffectScope.AllTrips) return true;
            return QuestTableValidator.TryGetTargetCity(def, out var target) && target == destinationCityId;
        }

        private int CurrentDestination()
        {
            var plan = sources.Itinerary?.CurrentPlan;
            return plan != null && plan.Legs.Count > 0 ? plan.Legs[plan.Legs.Count - 1].ToCityId : -1;
        }

        private QuestInstance Find(string questId)
        {
            foreach (var instance in active)
            {
                if (instance.QuestId == questId) return instance;
            }
            return null;
        }
    }
}

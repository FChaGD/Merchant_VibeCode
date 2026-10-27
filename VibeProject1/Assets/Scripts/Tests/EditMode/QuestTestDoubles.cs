using System;
using System.Collections.Generic;

namespace Game.Core.Tests
{
    // 퀘스트 테스트(설계 107번 §12) 공용 가짜 객체.
    internal sealed class QuestTestDate : IDateReader
    {
        public GameDate Today { get; private set; } = new(0);
        public event Action Changed;
        public void Set(int totalDays)
        {
            Today = new GameDate(totalDays);
            Changed?.Invoke();
        }
    }

    internal sealed class QuestTestArrivals : ITripArrivalSource
    {
        public event Action<int> FinalArrived;
        public void Arrive(int cityId) => FinalArrived?.Invoke(cityId);
    }

    internal sealed class QuestTestBattles : IBattleResultSource
    {
        public event Action<BattleResult> OnBattleEnded;
        public void Win(string enemyType) => OnBattleEnded?.Invoke(new BattleResult(BattleOutcome.Victory, BattleDefeatCause.None, BattleCargoReport.Empty, null, enemyType));
        public void Lose() => OnBattleEnded?.Invoke(new BattleResult(BattleOutcome.Defeat));
    }

    internal sealed class QuestTestMilestones : ITripMilestoneSource
    {
        public event Action<TripMilestone> Raised;
        public void Raise(TripMilestone milestone) => Raised?.Invoke(milestone);
    }

    internal sealed class QuestTestEvents : IEventEncounterStatus
    {
        public bool IsRunning { get; set; }
        public string CurrentEventId { get; set; }
        public string PendingEventId => null;
        public IReadOnlyList<EventEntry> QueuedEvents => Array.Empty<EventEntry>();
        public event Action<string> EventStarted;
        public void Start(string id)
        {
            IsRunning = true;
            CurrentEventId = id;
            EventStarted?.Invoke(id);
        }
    }

    internal sealed class QuestTestItinerary : ITripItinerary
    {
        public TripPlan CurrentPlan { get; set; }
        public bool HasItinerary => CurrentPlan != null && CurrentPlan.Legs.Count > 0;
        public int LegCount => CurrentPlan?.Legs.Count ?? 0;
        public int CurrentLegIndex { get; set; }
        public TripLeg CurrentLeg => HasItinerary ? CurrentPlan.Legs[CurrentLegIndex] : default;
        public bool HasNextLeg => HasItinerary && CurrentLegIndex < LegCount - 1;
        public void AdvanceLeg() => CurrentLegIndex++;
        public void Clear() => CurrentPlan = null;

        public static TripPlan Plan(params (int from, int to, float seconds)[] legs)
        {
            var list = new List<TripLeg>();
            foreach (var (from, to, seconds) in legs) list.Add(new TripLeg(from, to, 1f, 50, false, "b", "g", seconds, 1f, 1));
            return new TripPlan(list, TripTravelSettings.Default());
        }
    }

    internal sealed class QuestTestLocation : ITripCurrentLocationReader
    {
        public int CurrentCityId { get; set; } = 1;
        public event Action Changed { add { } remove { } }
    }

    internal sealed class QuestTestReputation : IReputationAdjuster
    {
        public int Value { get; private set; }
        public int Min => -100;
        public int Max => 100;
        public event Action Changed;
        public int Adjust(int delta)
        {
            Value += delta;
            Changed?.Invoke();
            return delta;
        }
    }

    internal sealed class QuestTestIncome : IGoldIncome
    {
        public int Total;
        public int Earn(int amount)
        {
            if (amount <= 0) return 0;
            Total += amount;
            return amount;
        }
    }
}

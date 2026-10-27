using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>퀘스트 상행 영향 - 난이도 가산·확정 사건 조건(Docs/설계/107번 §9).</summary>
    public class QuestTripEffectsTests
    {
        private GameObject go;
        private QuestTestMilestones milestones;
        private QuestTestEvents events;
        private QuestTestItinerary itinerary;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject(nameof(QuestManager));
            milestones = new QuestTestMilestones();
            events = new QuestTestEvents();
            itinerary = new QuestTestItinerary();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        private static QuestDefinition Def(string id, int targetCity, QuestEffectDefinition effect) => new()
        {
            Id = id,
            Kind = QuestKind.General,
            Acceptance = QuestAcceptance.Request,
            Completion = QuestCompletion.Instant,
            BoardCityIds = new List<int> { 1 },
            Objectives = new List<QuestObjectiveDefinition> { new() { Id = "a", Role = QuestObjectiveRole.Core, Type = QuestObjectiveType.Arrive, CityId = targetCity } },
            Effects = new List<QuestEffectDefinition> { effect },
        };

        private QuestManager Create(float random = 0f, params QuestDefinition[] defs)
        {
            var manager = go.AddComponent<QuestManager>();
            manager.Initialize(defs, new QuestManagerSources { Milestones = milestones, Events = events, Itinerary = itinerary, Random01 = () => random, Warn = _ => { } });
            return manager;
        }

        [Test]
        public void Bonus_SumsApplicableOnly()
        {
            var manager = Create(0f,
                Def("Dest", 5, new QuestEffectDefinition { Scope = QuestEffectScope.Destination, DifficultyBonus = 10 }),
                Def("All", 6, new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, DifficultyBonus = 3 }));
            Assert.AreEqual(0, manager.GetBonus(5), "수락 전에는 없다");
            var raised = 0;
            manager.BonusChanged += () => raised++;
            manager.TryStart("Dest", QuestStartSource.Request, 1, out _);
            manager.TryStart("All", QuestStartSource.Request, 1, out _);
            Assert.AreEqual(2, raised);
            Assert.AreEqual(13, manager.GetBonus(5));
            Assert.AreEqual(3, manager.GetBonus(7));
        }

        [Test]
        public void Runtime_AddsBonusBeforeLegOnly()
        {
            var bonus = 30;
            var runtime = new CurrentLegRuntime(TripTravelSettings.Default, null, () => bonus);
            runtime.Reset(new TripLeg(1, 2, 1f, 80, false, "b", "g", 10f, 1f, 1));
            Assert.AreEqual(100, runtime.Difficulty, "80 + 30 → 100에서 자름");
            runtime.AdjustLegOnly(-10);
            Assert.AreEqual(90, runtime.Difficulty, "가산을 먼저 자른 뒤 이번 구간 증감");
            bonus = 0;
            Assert.IsTrue(runtime.Recompute());
            Assert.AreEqual(70, runtime.Difficulty);
        }

        [Test]
        public void Gate_DueAtLegAndProgress()
        {
            var manager = Create(0f, Def("Q", 3, new QuestEffectDefinition { Scope = QuestEffectScope.Destination, EventId = "QE", EventTiming = QuestEventTiming.Progress, ProgressMin = 0.75f }));
            manager.TryStart("Q", QuestStartSource.Request, 1, out _);
            itinerary.CurrentPlan = QuestTestItinerary.Plan((1, 2, 10f), (2, 3, 10f));
            milestones.Raise(TripMilestone.TripStarted);

            Assert.IsFalse(manager.IsEventDue("Q", 0.9f), "아직 첫 구간");
            itinerary.CurrentLegIndex = 1;
            Assert.IsFalse(manager.IsEventDue("Q", 0.4f));
            Assert.IsTrue(manager.IsEventDue("Q", 0.5f));

            itinerary.CurrentPlan = QuestTestItinerary.Plan((1, 2, 10f), (2, 4, 10f));
            Assert.IsFalse(manager.IsEventDue("Q", 0.9f), "목적지가 대상 도시가 아니면 적용 안 함");
        }

        [Test]
        public void Gate_RandomTiming_UsesRolledLeg()
        {
            var manager = Create(0.6f, Def("Q", 3, new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, EventId = "QE", EventTiming = QuestEventTiming.Random, ProgressMin = 0.2f, ProgressMax = 0.7f }));
            manager.TryStart("Q", QuestStartSource.Request, 1, out _);
            itinerary.CurrentPlan = QuestTestItinerary.Plan((1, 2, 10f), (2, 3, 10f));
            milestones.Raise(TripMilestone.TripStarted);
            manager.TryGetInstance("Q", out var instance);
            Assert.AreEqual(1, instance.EventLegIndex);
            Assert.AreEqual(0.5f, instance.EventLegProgress, 1e-4f);
        }

        [Test]
        public void Gate_PerQuestOnce_PerTripResets()
        {
            var perQuest = Def("Q1", 3, new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, EventId = "E1", EventRepeat = QuestEventRepeat.PerQuest });
            var perTrip = Def("Q2", 3, new QuestEffectDefinition { Scope = QuestEffectScope.AllTrips, EventId = "E2", EventRepeat = QuestEventRepeat.PerTrip });
            var manager = Create(0f, perQuest, perTrip);
            manager.TryStart("Q1", QuestStartSource.Request, 1, out _);
            manager.TryStart("Q2", QuestStartSource.Request, 1, out _);
            itinerary.CurrentPlan = QuestTestItinerary.Plan((1, 2, 10f));
            milestones.Raise(TripMilestone.TripStarted);
            Assert.IsTrue(manager.IsEventDue("Q1", 0f));
            Assert.IsTrue(manager.IsEventDue("Q2", 0f));

            events.Start("E1");
            events.Start("E2");
            Assert.IsFalse(manager.IsEventDue("Q1", 1f));
            Assert.IsFalse(manager.IsEventDue("Q2", 1f));

            milestones.Raise(TripMilestone.TripStarted);
            Assert.IsFalse(manager.IsEventDue("Q1", 1f), "퀘스트당 1회");
            Assert.IsTrue(manager.IsEventDue("Q2", 1f), "상행마다 다시");
        }

        [Test]
        public void Condition_QuestKey()
        {
            var registry = EncounterConditionRegistry.CreateDefault();
            Assert.IsTrue(registry.IsValidArgument(EncounterConditionKeys.Quest, "Q1"));
            Assert.IsFalse(registry.IsValidArgument(EncounterConditionKeys.Quest, " "));

            var gate = new FakeGate();
            var condition = new EncounterCondition { Key = EncounterConditionKeys.Quest, Argument = "Q1" };
            Assert.IsFalse(registry.Evaluate(condition, new EncounterConditionContext(default, true, 1, 2, 0, 0, 0.5f, null)), "게이트 없음 = 거짓");
            Assert.IsTrue(registry.Evaluate(condition, new EncounterConditionContext(default, true, 1, 2, 0, 0, 0.5f, null, gate)));
            Assert.AreEqual(("Q1", 0.5f), gate.Last);
        }

        private sealed class FakeGate : IQuestEncounterGate
        {
            public (string, float) Last;
            public bool IsEventDue(string questId, float legProgress)
            {
                Last = (questId, legProgress);
                return true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class TripDialogueSelectorTests
    {
        private static readonly EncounterConditionRegistry Conditions = EncounterConditionRegistry.CreateDefault();

        private static DialogueEntry D(string id, DialogueOccurrence o, DialogueTrigger t = DialogueTrigger.None, string speakers = "a", int w = 100, string cond = null)
            => new() { Id = id, YarnNode = id + "_node", Occurrence = o, Trigger = t, Speakers = new List<string>(speakers.Split(';')), Weight = w, Conditions = EncounterConditionParser.Parse(cond) };

        private static Func<float> Rolls(params float[] values)
        {
            var queue = new Queue<float>(values);
            return () => queue.Count > 0 ? queue.Dequeue() : 0.99f;
        }

        private static DialogueEntry Check(IReadOnlyList<DialogueTrigger> pending, List<DialogueEntry> table, EncounterHistory history = null, Func<string, bool> available = null, Func<float> random = null)
        {
            history ??= new EncounterHistory();
            var context = new EncounterConditionContext(default, false, 0, 0, 0, 0, 0f, history);
            return TripDialogueSelector.PickForCheck(pending, table, history, available ?? (_ => true), Conditions, context, random ?? Rolls());
        }

        [Test]
        public void Check_AfterEventPreferred()
        {
            var table = new List<DialogueEntry>
            {
                D("battle", DialogueOccurrence.Trigger, DialogueTrigger.AfterBattle),
                D("event", DialogueOccurrence.Trigger, DialogueTrigger.AfterEvent),
            };
            Assert.AreEqual("event", Check(new[] { DialogueTrigger.AfterBattle, DialogueTrigger.AfterEvent }, table).Id);
        }

        [Test]
        public void Check_EarlierTriggerFirst_ThenSkipsTriggersWithoutCandidates()
        {
            var table = new List<DialogueEntry>
            {
                D("battle", DialogueOccurrence.Trigger, DialogueTrigger.AfterBattle),
                D("city", DialogueOccurrence.Trigger, DialogueTrigger.MidCityArrival),
            };
            Assert.AreEqual("city", Check(new[] { DialogueTrigger.MidCityArrival, DialogueTrigger.AfterBattle }, table).Id);
            Assert.AreEqual("battle", Check(new[] { DialogueTrigger.Departure, DialogueTrigger.AfterBattle }, table).Id);
            // '이벤트 직후'에 후보가 없으면 나머지를 먼저 생긴 순서로 본다.
            Assert.AreEqual("battle", Check(new[] { DialogueTrigger.AfterBattle, DialogueTrigger.AfterEvent }, table).Id);
        }

        [Test]
        public void Check_TriggerUsesFirstPassingRowInTableOrder()
        {
            var table = new List<DialogueEntry>
            {
                D("d_blocked", DialogueOccurrence.Trigger, DialogueTrigger.Departure, cond: "flag:never"),
                D("d_first", DialogueOccurrence.Trigger, DialogueTrigger.Departure),
                D("d_second", DialogueOccurrence.Trigger, DialogueTrigger.Departure),
            };
            Assert.AreEqual("d_first", Check(new[] { DialogueTrigger.Departure }, table).Id);
        }

        [Test]
        public void Check_NoTriggerCandidate_RollsRandomAt30Percent()
        {
            var table = new List<DialogueEntry> { D("r1", DialogueOccurrence.Random), D("r2", DialogueOccurrence.Random) };
            Assert.IsNull(Check(Array.Empty<DialogueTrigger>(), table, random: Rolls(0.3f)));          // 30% 경계 = 실패
            Assert.AreEqual("r2", Check(Array.Empty<DialogueTrigger>(), table, random: Rolls(0.29f, 0.9f)).Id);
        }

        [Test]
        public void Check_RandomPlayedThisTrip_IsExcluded()
        {
            var history = new EncounterHistory();
            history.RecordCompleted("r1");
            var table = new List<DialogueEntry> { D("r1", DialogueOccurrence.Random) };
            Assert.IsNull(Check(Array.Empty<DialogueTrigger>(), table, history, random: Rolls(0f, 0f)));
        }

        [Test]
        public void Check_UnavailableSpeakerExcluded()
        {
            var table = new List<DialogueEntry>
            {
                D("dead", DialogueOccurrence.Trigger, DialogueTrigger.Departure, speakers: "gone"),
                D("alive", DialogueOccurrence.Trigger, DialogueTrigger.Departure, speakers: "a"),
            };
            Assert.AreEqual("alive", Check(new[] { DialogueTrigger.Departure }, table, available: id => id != "gone").Id);
        }

        [Test]
        public void Manual_PrefersUnplayedManual_ThenRepeat_OnlyForThatSpeaker()
        {
            var history = new EncounterHistory();
            var context = new EncounterConditionContext(default, false, 0, 0, 0, 0, 0f, history);
            var table = new List<DialogueEntry>
            {
                D("other", DialogueOccurrence.Manual, speakers: "b"),
                D("m1", DialogueOccurrence.Manual, speakers: "a"),
                D("rep", DialogueOccurrence.ManualRepeat, speakers: "a"),
            };
            Assert.AreEqual("m1", TripDialogueSelector.PickManual("a", table, history, _ => true, Conditions, context, Rolls()).Id);
            history.RecordCompleted("m1");
            Assert.AreEqual("rep", TripDialogueSelector.PickManual("a", table, history, _ => true, Conditions, context, Rolls()).Id);
            history.RecordCompleted("rep");
            Assert.AreEqual("rep", TripDialogueSelector.PickManual("a", table, history, _ => true, Conditions, context, Rolls()).Id);   // 반복은 제한 없음
            Assert.IsTrue(TripDialogueSelector.HasManualTalk("a", table, history, _ => true, Conditions, context));
            Assert.IsFalse(TripDialogueSelector.HasManualTalk("c", table, history, _ => true, Conditions, context));
        }

        [Test]
        public void MapMilestone_TripEndedHasNoTrigger()
        {
            Assert.IsTrue(TripDialogueSelector.TryMapMilestone(TripMilestone.TripStarted, out var t1));
            Assert.AreEqual(DialogueTrigger.Departure, t1);
            Assert.IsTrue(TripDialogueSelector.TryMapMilestone(TripMilestone.EventEnded, out var t2));
            Assert.AreEqual(DialogueTrigger.AfterEvent, t2);
            Assert.IsFalse(TripDialogueSelector.TryMapMilestone(TripMilestone.TripEnded, out _));
        }

        [Test]
        public void ContextFactory_MapsLegAndRegions()
        {
            var leg = new TripLeg(3, 7, 10f, 1, true, "band", "grade", 60f, 1f, 1);
            var c = EncounterConditionContextFactory.Create(new GameDate(40), true, leg, city => city == 3 ? 1 : 2, 0.5f, null);
            Assert.IsTrue(c.HasLeg);
            Assert.AreEqual(3, c.FromCityId);
            Assert.AreEqual(7, c.ToCityId);
            Assert.AreEqual(1, c.FromRegionId);
            Assert.AreEqual(2, c.ToRegionId);
            Assert.AreEqual(0.5f, c.Progress);
            Assert.AreEqual(2, c.Today.Month);

            var none = EncounterConditionContextFactory.Create(new GameDate(0), false, default, _ => 9, 0f, null);
            Assert.IsFalse(none.HasLeg);
            Assert.AreEqual(0, none.FromRegionId);
        }
    }
}

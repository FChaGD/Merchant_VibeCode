using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EncounterConditionRegistryTests
    {
        private static readonly EncounterConditionRegistry Registry = EncounterConditionRegistry.CreateDefault();

        // 3월 10일 = (3-1)*30 + (10-1) = 69일째.
        private static EncounterConditionContext Ctx(EncounterHistory history = null, bool hasLeg = true, float progress = 0.5f)
            => new(new GameDate(69), hasLeg, fromCityId: 4, toCityId: 7, fromRegionId: 1, toRegionId: 2, progress, history ?? new EncounterHistory());

        private static bool Eval(string text, EncounterConditionContext ctx) => Registry.EvaluateAll(EncounterConditionParser.Parse(text), ctx);

        [Test] public void EmptyConditions_AreTrue() => Assert.IsTrue(Registry.EvaluateAll(new List<EncounterCondition>(), Ctx()));

        [TestCase("month:3", true)]
        [TestCase("month:2-4", true)]
        [TestCase("month:4-5", false)]
        [TestCase("month:5-3", true)]   // 해를 넘는 범위(5,6,1,2,3)
        [TestCase("date:3/1-3/10", true)]
        [TestCase("date:3/11-3/20", false)]
        [TestCase("progress:0.2-0.6", true)]
        [TestCase("progress:0.6-1", false)]
        [TestCase("region:2", true)]
        [TestCase("region:3", false)]
        [TestCase("road:7-4", true)]
        [TestCase("road:4-5", false)]
        [TestCase("from:4", true)]
        [TestCase("to:4", false)]
        public void BuiltInKeys(string text, bool expected) => Assert.AreEqual(expected, Eval(text, Ctx()));

        [Test]
        public void FlagAndSeen_ReadHistory()
        {
            var h = new EncounterHistory();
            h.SetFlag("helped_cart", true);
            h.RecordCompleted("E1");
            Assert.IsTrue(Eval("flag:helped_cart; seen:E1", Ctx(h)));
            Assert.IsFalse(Eval("!flag:helped_cart", Ctx(h)));
            Assert.IsTrue(Eval("!seen:E2", Ctx(h)));
        }

        [Test]
        public void LocationKeys_AreFalseWithoutLeg()
        {
            Assert.IsFalse(Eval("region:2", Ctx(hasLeg: false)));
            Assert.IsTrue(Eval("!region:2", Ctx(hasLeg: false)));
        }

        [TestCase("month", "abc")]
        [TestCase("progress", "x-1")]
        [TestCase("date", "3-10")]
        [TestCase("road", "4")]
        public void MalformedArgument_IsInvalid_AndEvaluatesFalse(string key, string argument)
        {
            Assert.IsFalse(Registry.IsValidArgument(key, argument));
            Assert.IsFalse(Registry.Evaluate(new EncounterCondition(key, false, argument), Ctx()));
        }

        [Test]
        public void UnknownKeyAtRuntime_EvaluatesFalse()
            => Assert.IsFalse(Registry.Evaluate(new EncounterCondition("weather", false, "rain"), Ctx()));
    }
}

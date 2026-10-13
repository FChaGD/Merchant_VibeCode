using System;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class EncounterConditionParserTests
    {
        [Test]
        public void Empty_ReturnsNoConditions()
        {
            Assert.IsEmpty(EncounterConditionParser.Parse(null));
            Assert.IsEmpty(EncounterConditionParser.Parse("   "));
        }

        [Test]
        public void ParsesKeysNegationAndArguments()
        {
            var list = EncounterConditionParser.Parse("region:2; !flag:paid_toll ;month:3-5");
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(("region", false, "2"), (list[0].Key, list[0].Negated, list[0].Argument));
            Assert.AreEqual(("flag", true, "paid_toll"), (list[1].Key, list[1].Negated, list[1].Argument));
            Assert.AreEqual(("month", false, "3-5"), (list[2].Key, list[2].Negated, list[2].Argument));
        }

        [Test]
        public void TrailingSeparator_IsIgnored()
        {
            Assert.AreEqual(1, EncounterConditionParser.Parse("flag:a;").Count);
        }

        [TestCase("region")]
        [TestCase("region:")]
        [TestCase(":2")]
        [TestCase("weather:rain")]
        public void Malformed_Throws(string text)
        {
            Assert.Throws<FormatException>(() => EncounterConditionParser.Parse(text));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    public class MercenaryCandidateFilterTests
    {
        [Test]
        public void Excludes_Hired_Deceased_AndExcludedFromHiring()
        {
            var all = new List<CharacterProfile>
            {
                new("A", "A", "Warrior", "전사", default, 0),
                new("B", "B", "Warrior", "전사", default, 0),
                new("C", "C", "Warrior", "전사", default, 0),
                new("Escort01", "호위", "Warrior", "전사", default, 0, excludedFromHiring: true),
            };
            var result = MercenaryCandidateFilter.Filter(all, id => id == "A", id => id == "B");
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("C", result[0].CharacterId);
        }
    }
}

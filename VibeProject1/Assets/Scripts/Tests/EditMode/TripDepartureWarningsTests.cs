using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TripDepartureWarningsTests
    {
        private const string GoldText700 = "개인 소유 가능량 500을 넘는 700골드가 버려집니다. 마차에 적재하거나 은행에 보관하지 않은 골드는 버려집니다. 그래도 출발하시겠습니까?";

        [Test]
        public void Collect_NothingToWarn_Empty()
        {
            Assert.AreEqual(0, TripDepartureWarnings.Collect(true, 0, 500).Count);
        }

        [Test]
        public void Collect_AtLimit_NoGoldWarning()
        {
            // ExcessGold는 개인 골드가 정확히 500이면 0이다.
            Assert.AreEqual(0, TripDepartureWarnings.Collect(true, 0, 500).Count);
        }

        [Test]
        public void Collect_GoldOnly_ExactText()
        {
            CollectionAssert.AreEqual(new[] { GoldText700 }, TripDepartureWarnings.Collect(true, 700, 500));
        }

        [Test]
        public void Collect_Both_EmptyFormationFirst()
        {
            CollectionAssert.AreEqual(new[] { TripDepartureWarnings.EmptyFormation, GoldText700 }, TripDepartureWarnings.Collect(false, 700, 500));
        }

        [Test]
        public void FormatGold_UsesGivenLimit_WithThousandsSeparator()
        {
            StringAssert.StartsWith("개인 소유 가능량 1,000을 넘는 1,200골드가", TripDepartureWarnings.FormatGold(1000, 1200));
        }
    }
}

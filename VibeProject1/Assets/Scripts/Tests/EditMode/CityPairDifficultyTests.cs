using NUnit.Framework;

namespace Game.Core.Tests
{
    public class CityPairDifficultyTests
    {
        private static readonly CityPairKey AB = CityPairKey.Of(1, 2);
        private static readonly CityPairKey AC = CityPairKey.Of(1, 3);
        private static readonly GameDate Mar12 = new(2 * GameDate.DaysPerMonth + 11);   // 3월 12일 = 누적 71일
        private const int Day = 71;

        [Test]
        public void Key_IgnoresDirection()
        {
            Assert.AreEqual(CityPairKey.Of(1, 2), CityPairKey.Of(2, 1));
            Assert.AreNotEqual(CityPairKey.Of(1, 2), CityPairKey.Of(1, 3));
        }

        [Test]
        public void Timed_95Plus10Minus10_Is95()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, Day + 3);
            Assert.AreEqual(100, o.GetDifficulty(AB, 95));
            o.Apply(AB, "E1", Mar12, -10, Day + 5);
            Assert.AreEqual(95, o.GetDifficulty(AB, 95), "요청량 합 0 — 읽을 때 자른다");
        }

        [Test]
        public void Read_ClampsPermanentFirst()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 50, null);
            o.Apply(AB, "E2", Mar12, -10, Day + 3);
            Assert.AreEqual(90, o.GetDifficulty(AB, 90), "자르기(90+50)=100 → −10 = 90");
        }

        [Test]
        public void Read_ClampsAtMinimum()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, -20, null);
            Assert.AreEqual(TripTravelRules.MinDifficulty, o.GetDifficulty(AB, 5));
        }

        [Test]
        public void NoChanges_ReturnsClampedBase()
        {
            var o = new CityPairDifficultyOverrides();
            Assert.AreEqual(40, o.GetDifficulty(AB, 40));
            CollectionAssert.IsEmpty(o.GetChanges(AB));
        }

        [Test]
        public void Timed_EachExpiresOnOwnDay()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, 20);
            o.Apply(AB, "E2", Mar12, 10, 14);   // 상한에서 효과 없음
            Assert.AreEqual(100, o.GetDifficulty(AB, 90));
            CollectionAssert.AreEqual(new[] { AB }, o.ExpireThrough(14));
            Assert.AreEqual(100, o.GetDifficulty(AB, 90), "E1 +10은 20일까지 남는다");
            CollectionAssert.AreEqual(new[] { AB }, o.ExpireThrough(20));
            Assert.AreEqual(90, o.GetDifficulty(AB, 90));
        }

        [Test]
        public void PermanentThenTimed_ExpiryKeepsPermanent()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, null);
            o.Apply(AB, "E1", Mar12, 10, 13);
            Assert.AreEqual(60, o.GetDifficulty(AB, 40));
            o.ExpireThrough(13);
            Assert.AreEqual(50, o.GetDifficulty(AB, 40), "영구분은 남는다");
        }

        [Test]
        public void Expire_RemovesOnlyDue_ThenDropsEmptyPair()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, 13);
            o.Apply(AB, "E2", Mar12, 5, 20);
            CollectionAssert.IsEmpty(o.ExpireThrough(12));
            o.ExpireThrough(13);
            Assert.AreEqual(1, o.GetChanges(AB).Count);
            Assert.AreEqual(5, o.GetChanges(AB)[0].Amount);
            o.ExpireThrough(20);
            CollectionAssert.IsEmpty(o.GetChanges(AB));
            Assert.AreEqual(0, o.TrackedPairCount, "빈 도시 쌍 기록은 지운다");
        }

        [Test]
        public void Id_FormatAndSequence()
        {
            var o = new CityPairDifficultyOverrides();
            Assert.AreEqual("E1-0312-1", o.Apply(AB, "E1", Mar12, 10, null));
            Assert.AreEqual("E1-0312-2", o.Apply(AC, "E1", Mar12, 10, 80), "순번은 도시 쌍과 무관");
            Assert.AreEqual("E2-0312-1", o.Apply(AB, "E2", Mar12, 10, null));
            Assert.AreEqual("debug-0312-1", o.Apply(AB, "debug", Mar12, 10, null));
            Assert.AreEqual("event-0312-1", o.Apply(AB, null, Mar12, 10, null), "이벤트 Id가 없으면 event");
        }

        [Test]
        public void Id_NextYearSameDate_DoesNotRepeat()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, null);
            var nextYear = new GameDate(Mar12.TotalDays + GameDate.DaysPerYear);
            Assert.AreEqual("E1-0312-2", o.Apply(AB, "E1", nextYear, 10, null));
        }

        [Test]
        public void ZeroChange_NotStored_NoId()
        {
            var o = new CityPairDifficultyOverrides();
            Assert.IsNull(o.Apply(AB, "E1", Mar12, 0, 13));
            Assert.AreEqual(0, o.TrackedPairCount);
            Assert.AreEqual("E1-0312-1", o.Apply(AB, "E1", Mar12, 10, null), "0은 순번을 쓰지 않는다");
        }

        [Test]
        public void GetChanges_KeepsIdAmountExpiry()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, null);
            o.Apply(AB, "debug", Mar12, -5, 80);
            var changes = o.GetChanges(AB);
            Assert.AreEqual("E1-0312-1", changes[0].Id);
            Assert.IsFalse(changes[0].IsTimed);
            Assert.AreEqual(-5, changes[1].Amount);
            Assert.AreEqual(80, changes[1].ExpiresOnDay);
        }

        [Test]
        public void GetChanges_IsReadOnlyView()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, null);
            var changes = o.GetChanges(AB);
            Assert.IsNotInstanceOf<System.Collections.Generic.List<CityPairDifficultyChange>>(changes, "캐스팅으로 저장 목록을 고칠 수 없어야 한다");
            Assert.AreSame(changes, o.GetChanges(AB), "조회마다 새로 만들지 않는다(디버그 OnGUI 매 프레임 조회)");
            o.Apply(AB, "E1", Mar12, 5, null);
            Assert.AreEqual(2, changes.Count, "보기는 저장 목록을 그대로 비춘다");
        }

        [Test]
        public void OtherPairSharingGate_Unaffected()
        {
            var o = new CityPairDifficultyOverrides();
            o.Apply(AB, "E1", Mar12, 10, null);
            Assert.AreEqual(40, o.GetDifficulty(CityPairKey.Of(4, 2), 40), "같은 관문을 지나는 도시d–도시b는 별개");
        }
    }
}

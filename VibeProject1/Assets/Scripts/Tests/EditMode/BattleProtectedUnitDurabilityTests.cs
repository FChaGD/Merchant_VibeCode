using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    public class BattleProtectedUnitDurabilityTests
    {
        private static BattleProtectedUnit Unit(ProtectedUnitKind kind, float max = 200f, float? start = null)
            => new(Vector2.zero, max, null, 0.35f, kind, "Id#1", start);

        [Test]
        public void StartingHp_SetsCurrent_MaxUnchanged()
        {
            var unit = Unit(ProtectedUnitKind.Wagon, start: 150f);
            Assert.AreEqual(200f, unit.MaxHp);
            Assert.AreEqual(150f, unit.CurrentHp);
        }

        [Test]
        public void StartingHp_ClampedToMax() => Assert.AreEqual(200f, Unit(ProtectedUnitKind.Wagon, start: 500f).CurrentHp);

        [Test]
        public void NoStartingHp_StartsFull() => Assert.AreEqual(120f, Unit(ProtectedUnitKind.Facility, max: 120f).CurrentHp);

        // 시설도 파괴 후 잔해로 길을 막는다(기획 90번 §3-7).
        [Test]
        public void DestroyedFacility_StillBlocksMovement()
        {
            var facility = Unit(ProtectedUnitKind.Facility);
            facility.TakeDamage(500f, null);
            Assert.IsFalse(facility.IsAlive);
            Assert.IsTrue(facility.BlocksMovement);
        }

        [Test]
        public void BattleResult_ProtectedUnits_DefaultEmpty()
        {
            Assert.IsNotNull(default(BattleResult).ProtectedUnits);
            Assert.AreEqual(0, new BattleResult(BattleOutcome.Victory).ProtectedUnits.Count);
        }

        [Test]
        public void BattleResult_CarriesRecords()
        {
            var result = new BattleResult(BattleOutcome.Victory, BattleDefeatCause.None, BattleCargoReport.Empty,
                new[] { new ProtectedUnitRecord("Facility01#1", ProtectedUnitKind.Facility, 0f, true) });
            Assert.AreEqual("Facility01#1", result.ProtectedUnits[0].UnitId);
            Assert.IsTrue(result.ProtectedUnits[0].Destroyed);
        }
    }
}

using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class OwnedCaravanAssetRegistryTests
    {
        [Test]
        public void Add_IssuesKindSerialIds_NeverReused()
        {
            var registry = new OwnedCaravanAssetRegistry();
            var a = registry.Add("Wagon01", FormationUnitKind.Wagon);
            var b = registry.Add("Wagon01", FormationUnitKind.Wagon);
            registry.Remove(b);
            var c = registry.Add("Wagon01", FormationUnitKind.Wagon);

            Assert.AreEqual("Wagon01#1", a);
            Assert.AreEqual("Wagon01#2", b);
            Assert.AreEqual("Wagon01#3", c);
        }

        [Test]
        public void GetIds_KeepsOwnedOrderPerKind()
        {
            var registry = new OwnedCaravanAssetRegistry();
            var w1 = registry.Add("Wagon03", FormationUnitKind.Wagon);
            var f1 = registry.Add("Facility01", FormationUnitKind.Facility);
            var w2 = registry.Add("Wagon01", FormationUnitKind.Wagon);

            CollectionAssert.AreEqual(new[] { w1, w2 }, registry.GetIds(FormationUnitKind.Wagon));
            CollectionAssert.AreEqual(new[] { f1 }, registry.GetIds(FormationUnitKind.Facility));
            Assert.AreEqual(2, registry.Count(FormationUnitKind.Wagon));
        }

        [Test]
        public void NumberOf_CountsWithinSameKindId()
        {
            var registry = new OwnedCaravanAssetRegistry();
            var a1 = registry.Add("Wagon01", FormationUnitKind.Wagon);
            var b1 = registry.Add("Wagon03", FormationUnitKind.Wagon);
            var a2 = registry.Add("Wagon01", FormationUnitKind.Wagon);

            Assert.AreEqual(1, registry.NumberOf(a1));
            Assert.AreEqual(1, registry.NumberOf(b1));
            Assert.AreEqual(2, registry.NumberOf(a2));
        }

        [Test]
        public void Rename_AfterRemoval_ShiftsNumbers()
        {
            var registry = new OwnedCaravanAssetRegistry();
            var a1 = registry.Add("Wagon01", FormationUnitKind.Wagon);
            var a2 = registry.Add("Wagon01", FormationUnitKind.Wagon);
            var a3 = registry.Add("Wagon01", FormationUnitKind.Wagon);

            Assert.IsTrue(registry.Remove(a1));

            Assert.AreEqual(1, registry.NumberOf(a2));
            Assert.AreEqual(2, registry.NumberOf(a3));
            Assert.AreEqual(0, registry.NumberOf(a1));
            Assert.IsFalse(registry.TryGetKindId(a1, out _));
        }

        [Test]
        public void Remove_Unknown_ReturnsFalse()
        {
            Assert.IsFalse(new OwnedCaravanAssetRegistry().Remove("Wagon01#1"));
        }

        [Test]
        public void Clear_ResetsSerials()
        {
            var registry = new OwnedCaravanAssetRegistry();
            registry.Add("Wagon01", FormationUnitKind.Wagon);
            registry.Clear();

            Assert.AreEqual("Wagon01#1", registry.Add("Wagon01", FormationUnitKind.Wagon));
        }

        [Test]
        public void Format_PutsNumberBeforeName()
        {
            var profile = new CaravanAssetProfile("Wagon01", "마차이름1", FormationUnitKind.Wagon, "마차", 1000);

            Assert.AreEqual("2번 마차이름1", OwnedCaravanAssetNames.Format(new OwnedCaravanAsset("Wagon01#5", profile, 2)));
        }
    }
}

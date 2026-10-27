using System.Collections.Generic;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    public class TownStockLedgerTests
    {
        private static TownStockEntry Row(int city, TownStockCategory category, string id, int quantity)
            => new() { CityId = city, Category = category, ItemId = id, Quantity = quantity };

        private static TownStockLedger Create(System.Func<int, TownStockCategory, bool> isSellable = null) => new(new List<TownStockEntry>
        {
            Row(4, TownStockCategory.Wagon, "Wagon01", 2),
            Row(4, TownStockCategory.TradeGoods, "placeholder-1x1", 1),
            Row(4, TownStockCategory.Wagon, "Wagon02", 1),
            Row(1, TownStockCategory.Wagon, "Wagon01", 3),
        }, isSellable);

        [Test]
        public void GetLines_ReturnsRowsOfCityAndCategory_InTableOrder()
        {
            var lines = Create().GetLines(4, TownStockCategory.Wagon);

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual("Wagon01", lines[0].ItemId);
            Assert.AreEqual(2, lines[0].Remaining);
            Assert.AreEqual("Wagon02", lines[1].ItemId);
        }

        [Test]
        public void GetLines_UnknownCity_IsEmpty()
        {
            Assert.AreEqual(0, Create().GetLines(99, TownStockCategory.Wagon).Count);
        }

        [Test]
        public void TryConsume_DecrementsOnlyThatCity()
        {
            var ledger = Create();

            Assert.IsTrue(ledger.TryConsume(4, TownStockCategory.Wagon, "Wagon01"));

            Assert.AreEqual(1, ledger.GetLines(4, TownStockCategory.Wagon)[0].Remaining);
            Assert.AreEqual(3, ledger.GetLines(1, TownStockCategory.Wagon)[0].Remaining);
        }

        [Test]
        public void TryConsume_AtZero_FailsAndKeepsRowAtZero()
        {
            var ledger = Create();
            Assert.IsTrue(ledger.TryConsume(4, TownStockCategory.TradeGoods, "placeholder-1x1"));

            Assert.IsFalse(ledger.TryConsume(4, TownStockCategory.TradeGoods, "placeholder-1x1"));
            var lines = ledger.GetLines(4, TownStockCategory.TradeGoods);
            Assert.AreEqual(1, lines.Count);
            Assert.AreEqual(0, lines[0].Remaining);
        }

        [Test]
        public void TryConsume_UnknownItem_Fails()
        {
            Assert.IsFalse(Create().TryConsume(4, TownStockCategory.Wagon, "Wagon05"));
        }

        [Test]
        public void Ledger_SkipsUnsellableCategory()
        {
            var asked = 0;
            var ledger = Create((city, category) => { asked++; return !(city == 1 && category == TownStockCategory.Wagon); });

            Assert.AreEqual(0, ledger.GetLines(1, TownStockCategory.Wagon).Count);
            Assert.AreEqual(0, ledger.GetLines(1, TownStockCategory.Wagon).Count);
            Assert.AreEqual(1, asked, "같은 마을·Category는 한 번만 판정(경고 1회)");
        }

        [Test]
        public void TryReturn_ExistingRow_AddsOne()
        {
            var ledger = Create();
            ledger.TryConsume(4, TownStockCategory.Wagon, "Wagon02");

            Assert.IsTrue(ledger.TryReturn(4, TownStockCategory.Wagon, "Wagon02"));
            Assert.AreEqual(1, ledger.GetLines(4, TownStockCategory.Wagon)[1].Remaining, "0에서 1로 - 다시 살 수 있다");
            Assert.IsTrue(ledger.TryConsume(4, TownStockCategory.Wagon, "Wagon02"));
        }

        [Test]
        public void TryReturn_MissingRow_AppendsWithOne()
        {
            var ledger = Create();

            Assert.IsTrue(ledger.TryReturn(4, TownStockCategory.Wagon, "Wagon05"));
            var lines = ledger.GetLines(4, TownStockCategory.Wagon);
            Assert.AreEqual(3, lines.Count);
            Assert.AreEqual("Wagon05", lines[2].ItemId);
            Assert.AreEqual(1, lines[2].Remaining);
        }

        [Test]
        public void TryReturn_UnsellableCityOrEmptyId_Fails()
        {
            var ledger = Create((city, category) => city != 1);

            Assert.IsFalse(ledger.TryReturn(1, TownStockCategory.Wagon, "Wagon01"));
            Assert.AreEqual(0, ledger.GetLines(1, TownStockCategory.Wagon).Count);
            Assert.IsFalse(ledger.TryReturn(4, TownStockCategory.Wagon, null));
        }
    }
}

using System.Linq;
using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>선택 버리기의 선택 목록(Docs/설계/102번 §4.5).</summary>
    public class InventoryDiscardSelectionTests
    {
        [Test]
        public void Toggle_AddsThenRemoves()
        {
            var selection = new InventoryDiscardSelection();

            Assert.IsTrue(selection.Toggle("a"));
            Assert.IsTrue(selection.Toggle("b"));
            Assert.AreEqual(2, selection.Count);
            Assert.IsTrue(selection.Contains("a"));

            Assert.IsFalse(selection.Toggle("a"));
            Assert.AreEqual(1, selection.Count);
            Assert.IsFalse(selection.Contains("a"));
        }

        [Test]
        public void Ids_KeepSelectionOrder()
        {
            var selection = new InventoryDiscardSelection();
            selection.Toggle("c");
            selection.Toggle("a");
            selection.Toggle("b");

            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, selection.Ids.ToArray());
        }

        [Test]
        public void Prune_RemovesMissing_ReturnsWhetherChanged()
        {
            var selection = new InventoryDiscardSelection();
            selection.Toggle("a");
            selection.Toggle("b");

            Assert.IsTrue(selection.Prune(id => id == "b"));
            CollectionAssert.AreEqual(new[] { "b" }, selection.Ids.ToArray());
            Assert.IsFalse(selection.Prune(id => true));
        }

        [Test]
        public void Clear_Empties()
        {
            var selection = new InventoryDiscardSelection();
            selection.Toggle("a");
            selection.Clear();

            Assert.AreEqual(0, selection.Count);
        }
    }
}

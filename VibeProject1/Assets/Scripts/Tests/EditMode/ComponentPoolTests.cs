using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>전투 뷰 풀(Docs/Refactor/2026-10-26_씬전용.md A-1) - 전투마다 생성·파괴하지 않고 반환한 인스턴스를 다시 쓴다.</summary>
    public class ComponentPoolTests
    {
        private GameObject prefabGo;
        private GameObject parentA;
        private GameObject parentB;

        [SetUp]
        public void SetUp()
        {
            prefabGo = new GameObject("Prefab", typeof(SpriteRenderer));
            parentA = new GameObject("A");
            parentB = new GameObject("B");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(prefabGo);
            Object.DestroyImmediate(parentA);
            Object.DestroyImmediate(parentB);
        }

        private SpriteRenderer Prefab => prefabGo.GetComponent<SpriteRenderer>();

        [Test]
        public void Rent_CreatesWhenEmpty()
        {
            var pool = new ComponentPool<SpriteRenderer>();
            var item = pool.Rent(Prefab, parentA.transform);
            Assert.IsNotNull(item);
            Assert.AreNotSame(Prefab, item);
            Assert.IsTrue(item.gameObject.activeSelf);
            Assert.AreEqual(1, pool.Active.Count);
        }

        [Test]
        public void Rent_ReusesReturnedInstance()
        {
            var pool = new ComponentPool<SpriteRenderer>();
            var first = pool.Rent(Prefab, parentA.transform);
            pool.Return(first);
            Assert.IsFalse(first.gameObject.activeSelf);
            Assert.AreEqual(0, pool.Active.Count);

            var second = pool.Rent(Prefab, parentA.transform);
            Assert.AreSame(first, second);
            Assert.IsTrue(second.gameObject.activeSelf);
        }

        [Test]
        public void Rent_ReparentsToGivenParent()
        {
            var pool = new ComponentPool<SpriteRenderer>();
            var item = pool.Rent(Prefab, parentA.transform);
            pool.Return(item);
            var again = pool.Rent(Prefab, parentB.transform);
            Assert.AreSame(parentB.transform, again.transform.parent);
        }

        [Test]
        public void ReturnAll_DeactivatesAll_AndCallsBeforeReturn()
        {
            var pool = new ComponentPool<SpriteRenderer>();
            var a = pool.Rent(Prefab, parentA.transform);
            var b = pool.Rent(Prefab, parentB.transform);
            var called = 0;
            pool.ReturnAll(_ => called++);
            Assert.AreEqual(2, called);
            Assert.IsFalse(a.gameObject.activeSelf);
            Assert.IsFalse(b.gameObject.activeSelf);
            Assert.AreEqual(0, pool.Active.Count);
        }

        [Test]
        public void Return_NotRented_Ignored()
        {
            var pool = new ComponentPool<SpriteRenderer>();
            var item = pool.Rent(Prefab, parentA.transform);
            pool.Return(item);
            pool.Return(item);
            var x = pool.Rent(Prefab, parentA.transform);
            var y = pool.Rent(Prefab, parentA.transform);
            Assert.AreNotSame(x, y, "같은 인스턴스를 두 번 반환해도 두 번 대여되지 않는다");
        }
    }
}

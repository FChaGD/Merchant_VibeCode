using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>평판 수치(Docs/설계/107번 §10).</summary>
    public class ReputationManagerTests
    {
        private GameObject go;

        [SetUp]
        public void SetUp() => go = new GameObject(nameof(ReputationManager));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(go);

        [Test]
        public void StartsAtZero_DefaultRange()
        {
            var reputation = go.AddComponent<ReputationManager>();
            Assert.AreEqual(0, reputation.Value);
            Assert.AreEqual(-100, reputation.Min);
            Assert.AreEqual(100, reputation.Max);
        }

        [Test]
        public void Adjust_ClampsAndReturnsActual()
        {
            var reputation = go.AddComponent<ReputationManager>();
            reputation.Adjust(95);
            Assert.AreEqual(5, reputation.Adjust(10));
            Assert.AreEqual(100, reputation.Value);
            Assert.AreEqual(-200, reputation.Adjust(-300));
            Assert.AreEqual(-100, reputation.Value);
        }

        [Test]
        public void NoChange_NoEvent()
        {
            var reputation = go.AddComponent<ReputationManager>();
            var count = 0;
            reputation.Changed += () => count++;
            reputation.Adjust(0);
            reputation.Adjust(5);
            Assert.AreEqual(1, count);
        }
    }
}

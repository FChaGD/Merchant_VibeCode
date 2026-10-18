using System;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    public class FieldMovementViewHiderTests
    {
        private sealed class Reader : IFieldUIHideReader
        {
            public bool IsHidden { get; set; }
            public event Action Changed;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public void Set(bool hidden) { IsHidden = hidden; Changed?.Invoke(); }
        }

        private GameObject go;
        private CanvasGroup group;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("MovementView", typeof(RectTransform), typeof(CanvasGroup));
            group = go.GetComponent<CanvasGroup>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(go);

        [Test]
        public void HidesAndRestores()
        {
            var reader = new Reader();
            using var hider = new FieldMovementViewHider(group, reader);
            Assert.AreEqual(1f, group.alpha);
            reader.Set(true);
            Assert.AreEqual(0f, group.alpha);
            Assert.IsFalse(group.interactable);
            Assert.IsFalse(group.blocksRaycasts);
            reader.Set(false);
            Assert.AreEqual(1f, group.alpha);
            Assert.IsTrue(group.interactable);
            Assert.IsTrue(group.blocksRaycasts);
        }

        [Test]
        public void AppliesCurrentStateOnCreate()
        {
            var reader = new Reader { IsHidden = true };
            using var hider = new FieldMovementViewHider(group, reader);
            Assert.AreEqual(0f, group.alpha, "Field 재로드 때 이미 가림 상태면 즉시 반영");
        }

        [Test]
        public void Dispose_Unsubscribes()
        {
            var reader = new Reader();
            var hider = new FieldMovementViewHider(group, reader);
            Assert.AreEqual(1, reader.Subscribers);
            hider.Dispose();
            Assert.AreEqual(0, reader.Subscribers);
        }
    }
}

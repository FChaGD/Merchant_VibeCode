using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 키 입력으로 버튼을 누를지 판정(Docs/설계/99번 §5.1). 모달·시설 화면 동안 부모 CanvasGroup이 막는 경우를 포함한다.
    /// </summary>
    public class ShortcutKeyGateTests
    {
        private GameObject parent;
        private Button button;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent.transform, false);
            button = go.GetComponent<Button>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void Null_ReturnsFalse()
        {
            Assert.IsFalse(ShortcutKeyGate.ShouldInvoke(null));
        }

        [Test]
        public void ActiveInteractable_ReturnsTrue()
        {
            Assert.IsTrue(ShortcutKeyGate.ShouldInvoke(button));
        }

        [Test]
        public void NotInteractable_ReturnsFalse()
        {
            button.interactable = false;
            Assert.IsFalse(ShortcutKeyGate.ShouldInvoke(button));
        }

        [Test]
        public void InactiveObject_ReturnsFalse()
        {
            button.gameObject.SetActive(false);
            Assert.IsFalse(ShortcutKeyGate.ShouldInvoke(button));
        }

        [Test]
        public void ParentCanvasGroupNotInteractable_ReturnsFalse()
        {
            var group = parent.AddComponent<CanvasGroup>();
            group.interactable = false;
            Assert.IsFalse(ShortcutKeyGate.ShouldInvoke(button));
        }
    }
}

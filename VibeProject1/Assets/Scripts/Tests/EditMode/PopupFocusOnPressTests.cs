using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>
    /// 누른 창 판정(Docs/설계/46번 §5). 포인터 포함 여부는 입력과 분리해 가짜 판정으로 넘긴다.
    /// </summary>
    public class PopupFocusOnPressTests
    {
        private GameObject parent;
        private RectTransform back;
        private RectTransform middle;
        private RectTransform front;

        [SetUp]
        public void SetUp()
        {
            parent = new GameObject("Parent", typeof(RectTransform));
            back = CreateWindow("Back");
            middle = CreateWindow("Middle");
            front = CreateWindow("Front");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent);
        }

        private RectTransform CreateWindow(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return (RectTransform)go.transform;
        }

        private IEnumerable<RectTransform> All => new[] { back, middle, front };

        [Test]
        public void OverlappedArea_SelectsTopmostContainingWindow()
        {
            // 포인터가 back/middle 겹친 영역에 있다 - 위에 그려진 middle이 선택된다.
            var selected = PopupFocusOnPress.SelectWindowToFront(All, window => window == back || window == middle);

            Assert.AreEqual(middle, selected);
        }

        [Test]
        public void ClosedWindow_IsSkipped()
        {
            middle.gameObject.SetActive(false);

            var selected = PopupFocusOnPress.SelectWindowToFront(All, window => window == back || window == middle);

            Assert.AreEqual(back, selected);
        }

        [Test]
        public void PointerOutsideAllWindows_SelectsNothing()
        {
            Assert.IsNull(PopupFocusOnPress.SelectWindowToFront(All, _ => false));
        }

        [Test]
        public void ListOrder_DoesNotMatter_OnlySiblingOrder()
        {
            var selected = PopupFocusOnPress.SelectWindowToFront(new[] { front, back }, _ => true);

            Assert.AreEqual(front, selected);
        }

        [Test]
        public void DestroyedWindow_IsSkipped()
        {
            var windows = new List<RectTransform>(All);
            Object.DestroyImmediate(front.gameObject);

            var selected = PopupFocusOnPress.SelectWindowToFront(windows, _ => true);

            Assert.AreEqual(middle, selected);
        }
    }
}

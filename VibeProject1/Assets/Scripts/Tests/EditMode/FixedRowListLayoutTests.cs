using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 고정 줄 수 목록 배치(Docs/Refactor/2026-10-26_공통.md D-4) - 상인조합·마구간 보유·목록형 구매 후보·무역품 판매 목록이 같은 코드를 반복하던 것을 모았다.
    /// 줄은 매번 새로 만들지 않고 재사용한다.
    /// </summary>
    public class FixedRowListLayoutTests
    {
        private GameObject content;
        private RectTransform template;

        [SetUp]
        public void SetUp()
        {
            content = new GameObject("Content", typeof(RectTransform));
            template = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            template.gameObject.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(content);
            Object.DestroyImmediate(template.gameObject);
        }

        [Test]
        public void RowHeight_DividesViewport()
        {
            Assert.AreEqual(50f, FixedRowListLayout.RowHeight(400f, 8), 0.001f);
        }

        [Test]
        public void RowHeight_ZeroViewport_UsesMinimum()
        {
            Assert.AreEqual(0.125f, FixedRowListLayout.RowHeight(0f, 8), 0.0001f, "뷰포트 높이가 0이면 1로 보고 나눈다");
        }

        [Test]
        public void Render_ReusesRows_AndHidesExtra()
        {
            var rows = new List<RectTransform>();
            var contentRect = (RectTransform)content.transform;
            var bound = new List<int>();

            FixedRowListLayout.Render(rows, 3, template, contentRect, 20f, (_, i) => bound.Add(i));
            var first = rows[0];
            FixedRowListLayout.Render(rows, 1, template, contentRect, 20f, (_, i) => bound.Add(i));

            Assert.AreEqual(3, rows.Count, "줄을 새로 만들지 않고 재사용한다");
            Assert.AreSame(first, rows[0]);
            Assert.IsTrue(rows[0].gameObject.activeSelf);
            Assert.IsFalse(rows[1].gameObject.activeSelf);
            Assert.IsFalse(rows[2].gameObject.activeSelf);
            Assert.AreEqual(20f, contentRect.sizeDelta.y, 0.001f);
            Assert.AreEqual(new Vector2(0f, -0f), rows[0].anchoredPosition);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 0 }, bound);
        }

        [Test]
        public void Render_PlacesRowsTopDown()
        {
            var rows = new List<RectTransform>();
            FixedRowListLayout.Render(rows, 2, template, (RectTransform)content.transform, 30f, (_, _) => { });

            Assert.AreEqual(-30f, rows[1].anchoredPosition.y, 0.001f);
            Assert.AreEqual(30f, rows[1].sizeDelta.y, 0.001f);
            Assert.AreEqual(new Vector2(0f, 1f), rows[1].anchorMin);
            Assert.AreEqual(new Vector2(1f, 1f), rows[1].anchorMax);
        }
    }
}

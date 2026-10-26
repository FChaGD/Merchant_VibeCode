using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 레이어 숨김 규칙의 판정(Docs/Refactor/2026-10-26_공통.md B-4) - 활성 규칙이 가리키는 레이어의 합집합이 숨는다.
    /// </summary>
    public class LayerHideResolverTests
    {
        [Test]
        public void NoActiveRule_NothingHidden()
        {
            var hidden = LayerHideResolver.HiddenLayers(new List<(bool, IReadOnlyList<string>)>
            {
                (false, new[] { "Depth", "Persistent" }),
            });
            Assert.AreEqual(0, hidden.Count);
        }

        [Test]
        public void ActiveRule_HidesItsLayers()
        {
            var hidden = LayerHideResolver.HiddenLayers(new List<(bool, IReadOnlyList<string>)>
            {
                (true, new[] { "Depth" }),
                (false, new[] { "Persistent" }),
            });
            CollectionAssert.AreEquivalent(new[] { "Depth" }, hidden);
        }

        [Test]
        public void TwoRulesSameLayer_UnionOnce()
        {
            var hidden = LayerHideResolver.HiddenLayers(new List<(bool, IReadOnlyList<string>)>
            {
                (true, new[] { "Depth", "Persistent" }),
                (true, new[] { "Depth" }),
            });
            CollectionAssert.AreEquivalent(new[] { "Depth", "Persistent" }, hidden);
        }
    }
}

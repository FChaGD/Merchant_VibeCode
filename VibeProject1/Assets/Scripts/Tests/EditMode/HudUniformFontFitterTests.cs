using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>재화 HUD 세 레이블 공통 글자 크기(Docs/설계/101번 §4.2).</summary>
    public class HudUniformFontFitterTests
    {
        [Test]
        public void FitsWithinWidth_ReturnsMax()
        {
            Assert.AreEqual(24f, HudUniformFontFitter.Fit(new[] { 100f, 20f, 30f }, 300f, 24f, 12f));
        }

        [Test]
        public void Overflow_ScalesProportionally()
        {
            Assert.AreEqual(18f, HudUniformFontFitter.Fit(new[] { 200f, 50f, 50f }, 225f, 24f, 12f), 0.001f);
        }

        [Test]
        public void Overflow_ClampsToMin()
        {
            Assert.AreEqual(12f, HudUniformFontFitter.Fit(new[] { 1000f, 50f, 50f }, 110f, 24f, 12f));
        }

        [Test]
        public void ZeroRequired_ReturnsMax()
        {
            Assert.AreEqual(24f, HudUniformFontFitter.Fit(new[] { 0f, 0f }, 100f, 24f, 12f));
            Assert.AreEqual(24f, HudUniformFontFitter.Fit(null, 100f, 24f, 12f));
        }

        [Test]
        public void NonPositiveAvailable_ReturnsMin()
        {
            Assert.AreEqual(12f, HudUniformFontFitter.Fit(new[] { 10f }, 0f, 24f, 12f));
        }
    }
}

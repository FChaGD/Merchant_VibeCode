using NUnit.Framework;

namespace Game.Core.Tests
{
    public class FieldViewOnlyEditPolicyTests
    {
        [Test]
        public void RejectsEveryEdit()
        {
            var policy = new FieldViewOnlyEditPolicy();
            Assert.IsFalse(policy.HandlePaletteDrop(null, 0));
            Assert.IsFalse(policy.HandleGridMove("u", 0, 1));
            Assert.IsFalse(policy.HandleRemove("u", 0));
            Assert.IsFalse(policy.HandleRedirectMove("u", 1));
            Assert.AreEqual(FormationVisibleMargin.Default, policy.GetVisibleMarginCells());
        }
    }
}

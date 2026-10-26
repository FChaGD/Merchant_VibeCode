using NUnit.Framework;
using Game.Core;

namespace Game.Core.Tests
{
    /// <summary>"상행 시작" 비활성 사유 목록(Docs/설계/100번 §4.2). 순서 B(도착지) → C(대열) → A(화면 전환).</summary>
    public class TripStartBlockReasonsTests
    {
        [TestCase(true, true, true, new string[0])]
        [TestCase(false, true, true, new[] { TripStartBlockReasons.SceneNotRevealed })]
        [TestCase(true, false, true, new[] { TripStartBlockReasons.NoDestination })]
        [TestCase(true, true, false, new[] { TripStartBlockReasons.Disconnected })]
        [TestCase(false, false, true, new[] { TripStartBlockReasons.NoDestination, TripStartBlockReasons.SceneNotRevealed })]
        [TestCase(false, true, false, new[] { TripStartBlockReasons.Disconnected, TripStartBlockReasons.SceneNotRevealed })]
        [TestCase(true, false, false, new[] { TripStartBlockReasons.NoDestination, TripStartBlockReasons.Disconnected })]
        [TestCase(false, false, false, new[] { TripStartBlockReasons.NoDestination, TripStartBlockReasons.Disconnected, TripStartBlockReasons.SceneNotRevealed })]
        public void Collect_OrdersReasons(bool sceneRevealed, bool hasDestination, bool connected, string[] expected)
        {
            CollectionAssert.AreEqual(expected, TripStartBlockReasons.Collect(sceneRevealed, hasDestination, connected));
        }

        [Test]
        public void Texts_MatchSpec()
        {
            Assert.AreEqual("화면 전환 중입니다.", TripStartBlockReasons.SceneNotRevealed);
            Assert.AreEqual("도착지를 지정하세요.", TripStartBlockReasons.NoDestination);
            Assert.AreEqual("대열이 끊어져 있습니다. 정비창에서 마차를 이어 붙이세요.", TripStartBlockReasons.Disconnected);
        }
    }
}

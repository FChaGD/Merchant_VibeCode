using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 활동 오버레이 남은 시간 표기(1분 미만 ss, 이상 m:ss, 2026-09-07 사용자 확정). 표시 정수가 바뀔 때만 문구를 만들기 위해 정수 변환과 문구를 나눴다.
    /// </summary>
    public class FormationRemainingSecondsTests
    {
        [Test]
        public void ToDisplaySeconds_CeilsAndClamps()
        {
            Assert.AreEqual(1, FormationActivityOverlayView.ToDisplaySeconds(0.4f));
            Assert.AreEqual(60, FormationActivityOverlayView.ToDisplaySeconds(59.2f));
            Assert.AreEqual(0, FormationActivityOverlayView.ToDisplaySeconds(-3f));
        }

        [Test]
        public void FormatDisplaySeconds_MatchesExistingFormat()
        {
            Assert.AreEqual("01", FormationActivityOverlayView.FormatDisplaySeconds(1));
            Assert.AreEqual("1:00", FormationActivityOverlayView.FormatDisplaySeconds(60));
            Assert.AreEqual("1:15", FormationActivityOverlayView.FormatDisplaySeconds(75));
        }
    }
}

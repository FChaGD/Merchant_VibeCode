using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Core.Tests
{
    /// <summary>
    /// 이동 아이콘 구간 가중치를 버퍼에 채우는 형태(Docs/Refactor/2026-10-26_씬전용.md B-1, FO-0) - 정비창이 열린 동안 매 프레임 불리므로
    /// 배열을 새로 만들지 않는다. 값은 기존 배열 반환형과 같아야 한다.
    /// </summary>
    public class FormationSegmentWeightsTests
    {
        private const int Columns = 5;

        [Test]
        public void FillSegmentWeights_MatchesCompute()
        {
            // 0 → 6(대각선) → 7(가로) → 12(세로), 두 번째 구간이 부분 구간(남은 0.4)
            var path = new[] { 0, 6, 7, 12 };
            var expected = FormationPathFinder.ComputeSegmentWeights(path, Columns, 1, 0.4f);
            var buffer = new List<float>();
            FormationPathFinder.FillSegmentWeights(path, Columns, 1, 0.4f, buffer);
            CollectionAssert.AreEqual(expected, buffer);
        }

        [Test]
        public void FillSegmentWeights_ClearsBuffer()
        {
            var buffer = new List<float> { 9f, 9f, 9f, 9f, 9f };
            FormationPathFinder.FillSegmentWeights(new[] { 0, 1 }, Columns, -1, 1f, buffer);
            Assert.AreEqual(1, buffer.Count);
        }

        [Test]
        public void FillSegmentWeights_SingleSlot_Empty()
        {
            var buffer = new List<float> { 1f };
            FormationPathFinder.FillSegmentWeights(new[] { 3 }, Columns, -1, 1f, buffer);
            Assert.AreEqual(0, buffer.Count);
        }
    }
}

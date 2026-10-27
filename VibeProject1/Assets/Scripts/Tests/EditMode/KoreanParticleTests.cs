using NUnit.Framework;
using UnityEngine;

namespace Game.Core.Tests
{
    /// <summary>
    /// 받침에 따른 조사 선택(Docs/Refactor/2026-10-26_씬전용.md I-2, SE-7) - 끝 글자가 한글이면 받침으로, 숫자면 한국어 읽기의 받침으로, 그 밖은 둘 다 표기.
    /// </summary>
    public class KoreanParticleTests
    {
        [Test]
        public void KoreanParticle_Batchim_Eul()
        {
            Assert.AreEqual("을", KoreanParticle.EulReul("마차 1번"));
            Assert.AreEqual("이", KoreanParticle.IGa("짐마차 1번"));
            Assert.AreEqual("은", KoreanParticle.EunNeun("시설"));
        }

        [Test]
        public void KoreanParticle_NoBatchim_Reul()
        {
            Assert.AreEqual("를", KoreanParticle.EulReul("1번 마차"));
            Assert.AreEqual("가", KoreanParticle.IGa("마차"));
            Assert.AreEqual("는", KoreanParticle.EunNeun("마차"));
        }

        [Test]
        public void KoreanParticle_DigitEnd()
        {
            Assert.AreEqual("를", KoreanParticle.EulReul("마차 2"), "이(2)는 받침 없음");
            Assert.AreEqual("을", KoreanParticle.EulReul("마차 3"), "삼(3)은 받침 있음");
            Assert.AreEqual("을", KoreanParticle.EulReul("마차 0"), "영(0)은 받침 있음");
            Assert.AreEqual("를", KoreanParticle.EulReul("마차 9"), "구(9)는 받침 없음");
        }

        [Test]
        public void KoreanParticle_NonHangul_Both()
        {
            Assert.AreEqual("을(를)", KoreanParticle.EulReul("Cart"));
            Assert.AreEqual("이(가)", KoreanParticle.IGa("Cart)"));
        }

        [Test]
        public void KoreanParticle_Empty_Both()
        {
            Assert.AreEqual("을(를)", KoreanParticle.EulReul(string.Empty));
            Assert.AreEqual("을(를)", KoreanParticle.EulReul(null));
        }

        [Test]
        public void TripCityNames_UsesNameOrFallback()
        {
            var map = new WorldMap();
            map.RestoreRegion(1, "지역1", WorldMap.DefaultRegionSize);
            map.RestoreCity(1, 1, Vector2.zero, "항구", null, string.Empty);
            map.RestoreCity(2, 1, Vector2.one, string.Empty, null, string.Empty);
            Assert.AreEqual("항구", TripCityNames.DisplayName(map, 1));
            Assert.AreEqual("디버그 도시 2", TripCityNames.DisplayName(map, 2), "이름 없는 도시");
            Assert.AreEqual("디버그 도시 9", TripCityNames.DisplayName(map, 9), "없는 도시");
            Assert.AreEqual("디버그 도시 1", TripCityNames.DisplayName(null, 1), "지도 없음");
        }
    }
}

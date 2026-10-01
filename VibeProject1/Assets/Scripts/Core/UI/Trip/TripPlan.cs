using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 상행 구간 하나 = 다음 도시까지(Docs/기획/75번 §4-15). 사이에 관문이 끼면 지역을 넘는 구간이다. 값은 출발 시점에 확정된다
    /// (설계 76번 §10-3 - 상행 중 도로 난이도가 바뀌어도 진행 중 여정은 그대로).
    /// </summary>
    public readonly struct TripLeg
    {
        public int FromCityId { get; }
        public int ToCityId { get; }
        public float Distance { get; }
        public int Difficulty { get; }
        public bool CrossesRegion { get; }
        public string BandName { get; }
        public string GradeName { get; }
        public float DurationSeconds { get; }
        public float EncountersPerFiveMinutes { get; }

        public TripLeg(int fromCityId, int toCityId, float distance, int difficulty, bool crossesRegion,
            string bandName, string gradeName, float durationSeconds, float encountersPerFiveMinutes)
        {
            FromCityId = fromCityId;
            ToCityId = toCityId;
            Distance = distance;
            Difficulty = difficulty;
            CrossesRegion = crossesRegion;
            BandName = bandName;
            GradeName = gradeName;
            DurationSeconds = durationSeconds;
            EncountersPerFiveMinutes = encountersPerFiveMinutes;
        }
    }

    /// <summary>현재 위치 → 도착지 최단 경로를 구간으로 나눈 결과(설계 76번 §4.3).</summary>
    public sealed class TripPlan
    {
        public IReadOnlyList<TripLeg> Legs { get; }
        public float TotalDurationSeconds { get; }

        public TripPlan(IReadOnlyList<TripLeg> legs)
        {
            Legs = legs;
            var total = 0f;
            foreach (var leg in legs) total += leg.DurationSeconds;
            TotalDurationSeconds = total;
        }
    }
}

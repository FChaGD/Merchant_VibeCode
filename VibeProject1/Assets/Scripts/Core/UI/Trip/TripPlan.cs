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
        // 패널 예상 일수 - 출발 시점 소요시간 기준(파괴 지연·궤주 미반영, Docs/기획/84번 §4.3). 생성자에서 필수로 받는다 - 기본값을 두면
        // 새 생성 경로가 값을 빠뜨려도 컴파일되고 패널에 "약 0일"이 조용히 뜬다.
        public int EstimatedDays { get; }
        /// <summary>도시 쌍 변경을 반영하기 전 구성 도로 기본값의 길이 가중 평균(설계 89 §5.2). 현재 구간 재계산·도시 쌍 변경의 기준.</summary>
        public int BaseDifficulty { get; }

        public TripLeg(int fromCityId, int toCityId, float distance, int difficulty, bool crossesRegion,
            string bandName, string gradeName, float durationSeconds, float encountersPerFiveMinutes, int estimatedDays, int? baseDifficulty = null)
        {
            BaseDifficulty = baseDifficulty ?? difficulty;
            EstimatedDays = estimatedDays;
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
        // 합계는 총 초를 환산하지 않고 구간 일수를 더한다 - 실제 가산도 구간마다 버림이라 같은 값이 된다(기획 84번 §4-9).
        public int TotalEstimatedDays { get; }

        public TripPlan(IReadOnlyList<TripLeg> legs)
        {
            Legs = legs;
            var total = 0f;
            var days = 0;
            foreach (var leg in legs)
            {
                total += leg.DurationSeconds;
                days += leg.EstimatedDays;
            }
            TotalDurationSeconds = total;
            TotalEstimatedDays = days;
        }
    }
}

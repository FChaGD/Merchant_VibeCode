using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 소요시간·난이도 등급 계산(Docs/기획/75번 §4.1·§4.2, 설계 76번 §4.1·§4.2). 숫자만으로 정해지는 순수 함수라 테스트로 고정한다.
    /// 소요시간(초) = 최소 시간 + (시간 폭 − n) × 거리 비율 + n × 난이도 비율 - n(난이도 몫)을 시간 폭에서 먼저 떼어 두고 나머지를 거리가
    /// 채우므로, 짧음·중간은 정한 범위를 꽉 채우고 난이도는 어느 거리에서든 같은 시간만큼 영향을 준다(사용자 제안 구조).
    /// </summary>
    public static class TripTravelRules
    {
        public const int MinDifficulty = 1;
        public const int MaxDifficulty = 100;
        // 인카운터 빈도 기준 시간(5분) - 등급 값이 "5분 평균 횟수"다(기획 75번 §3.2).
        public const float EncounterRateWindowSeconds = 300f;

        public static int ClampDifficulty(int difficulty) => Mathf.Clamp(difficulty, MinDifficulty, MaxDifficulty);

        /// <summary>지역을 넘는 구간은 거리와 무관하게 마지막(긴 거리) 구분이다(기획 75번 §4-15).</summary>
        public static int ResolveBandIndex(TripTravelSettings settings, float distance, bool crossesRegion)
        {
            var last = settings.DistanceBands.Count - 1;
            if (crossesRegion) return last;
            for (var i = last; i > 0; i--)
            {
                if (distance >= settings.DistanceBands[i].StartDistance) return i;
            }
            return 0;
        }

        public static float ComputeDurationSeconds(TripTravelSettings settings, float distance, int difficulty, bool crossesRegion)
        {
            var band = settings.DistanceBands[ResolveBandIndex(settings, distance, crossesRegion)];
            var difficultyShare = Mathf.Min(settings.DifficultyTimeSeconds, band.SpanSeconds);
            // 긴 거리는 상한이 없어 비율이 1을 넘어도 그대로 둔다(기획 75번 §4.1). 지역을 넘는 구간의 합이 시작 거리보다 짧으면 0.
            var distanceRatio = band.DistanceSpan > 0f ? Mathf.Max(0f, distance - band.StartDistance) / band.DistanceSpan : 0f;
            var difficultyRatio = (ClampDifficulty(difficulty) - MinDifficulty) / (float)(MaxDifficulty - MinDifficulty);
            return band.MinSeconds + (band.SpanSeconds - difficultyShare) * distanceRatio + difficultyShare * difficultyRatio;
        }

        public static TripDifficultyGrade ResolveGrade(TripTravelSettings settings, int difficulty)
        {
            var clamped = ClampDifficulty(difficulty);
            foreach (var grade in settings.DifficultyGrades)
            {
                if (clamped <= grade.MaxDifficulty) return grade;
            }
            return settings.DifficultyGrades[settings.DifficultyGrades.Count - 1];
        }

        /// <summary>판정 한 번의 인카운터 확률 = 5분 평균 횟수 × 판정 간격 ÷ 300초(기획 75번 §4-8).</summary>
        public static float ComputeCheckProbability(float encountersPerFiveMinutes, float checkIntervalSeconds)
            => Mathf.Clamp01(encountersPerFiveMinutes * checkIntervalSeconds / EncounterRateWindowSeconds);
    }
}

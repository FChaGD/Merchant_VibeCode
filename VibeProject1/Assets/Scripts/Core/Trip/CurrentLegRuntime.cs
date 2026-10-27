using System;

namespace Game.Core
{
    /// <summary>
    /// 현재 구간의 실행 값(설계 89 §4.3). 구간 계획(TripLeg)은 출발 시점 값이라(예상 일수 포함) 그대로 두고, 이벤트로 바뀌는 난이도·빈도·등급·기본 소요시간은
    /// 여기 따로 둔다. 실행 난이도 = 자르기(도시 쌍 보이는 값 + 이번 구간 증감). 시작은 계획 값 그대로이고 변경 신호가 올 때만 다시 계산한다.
    /// 이번 구간 증감도 도시 쌍 변경(설계 89 §5.2)과 같이 요청 변경량을 그대로 누적하고 읽을 때 자른다(95 +10 −10 = 95) - 세 방식의 결과가 같아야 한다
    /// (2026-10-16 사용자 결정, §4.3).
    /// </summary>
    public sealed class CurrentLegRuntime
    {
        private readonly Func<TripTravelSettings> settings;
        private readonly ICityPairDifficultyReader pairs; // 선택적 - 없으면 구간 기본값
        private readonly Func<int> questBonus; // 선택적 - 이번 상행의 퀘스트 가산(설계 107번 §9.2)

        public CurrentLegRuntime(Func<TripTravelSettings> settings, ICityPairDifficultyReader pairs, Func<int> questBonus = null)
        {
            this.settings = settings;
            this.pairs = pairs;
            this.questBonus = questBonus;
        }

        public bool HasLeg { get; private set; }
        public TripLeg Leg { get; private set; }
        public CityPairKey Pair => CityPairKey.Of(Leg.FromCityId, Leg.ToCityId);
        public int Difficulty { get; private set; }
        public int LegOnlyChange { get; private set; }
        public float EncountersPerFiveMinutes { get; private set; }
        public string GradeName { get; private set; }

        public float BaseDurationSeconds => TripTravelRules.ComputeDurationSeconds(settings(), Leg.Distance, Difficulty, Leg.CrossesRegion);

        /// <summary>
        /// 구간 시작. 계획 값으로 채운 뒤 현재 도시 쌍 값으로 한 번 다시 계산한다 - 출발 뒤 그 구간에 들어서기 전에 기간 변경이 만료됐거나 새로 생겼으면
        /// 계획 값이 낡았기 때문이다(최종 검토 지적). 출발 계획(예상 일수)은 건드리지 않는다.
        /// </summary>
        public void Reset(TripLeg leg)
        {
            HasLeg = true;
            Leg = leg;
            LegOnlyChange = 0;
            Difficulty = leg.Difficulty;
            EncountersPerFiveMinutes = leg.EncountersPerFiveMinutes;
            GradeName = leg.GradeName;
            Recompute();
        }

        public void Clear()
        {
            HasLeg = false;
            Leg = default;
            LegOnlyChange = 0;
        }

        /// <summary>이번 구간만 증감 - 요청 변경량을 그대로 누적하고 자르기는 다시 계산할 때 한다. 실행 난이도가 바뀌었으면 true.</summary>
        public bool AdjustLegOnly(int change)
        {
            if (!HasLeg || change == 0) return false;
            LegOnlyChange += change;
            return Recompute();
        }

        /// <summary>실행 난이도를 다시 계산한다. 바뀌었으면 true(빈도·등급 갱신).</summary>
        public bool Recompute()
        {
            if (!HasLeg) return false;
            var pairValue = pairs?.GetDifficulty(Pair, Leg.BaseDifficulty) ?? TripTravelRules.ClampDifficulty(Leg.BaseDifficulty);
            // 퀘스트 가산은 도시 쌍 값과 같은 단계에서 자른 뒤 이번 구간 증감을 더한다 - 계획(TripRoutePlanner)의 구간 난이도와 시작 값이 일치한다.
            var withQuests = TripTravelRules.ClampDifficulty(pairValue + (questBonus?.Invoke() ?? 0));
            var next = TripTravelRules.ClampDifficulty(withQuests + LegOnlyChange);
            if (next == Difficulty) return false;
            Difficulty = next;
            var grade = TripTravelRules.ResolveGrade(settings(), next);
            EncountersPerFiveMinutes = grade.EncountersPerFiveMinutes;
            GradeName = grade.Name;
            return true;
        }
    }
}

using System;

namespace Game.Core
{
    /// <summary>
    /// `&lt;&lt;difficulty 증감 leg|days N|permanent&gt;&gt;`(설계 89 §5.1·§5.2). 이번 구간만 = 현재 구간 실행 값만, 날짜 기간·영구 = 현재 구간의 도시 쌍. 재계산(빈도·남은
    /// 시간·구간 라벨)은 변경을 받은 여정 상태가 한다. 진행 중 구간이 없으면(여정 없음) 실패. 이벤트 디버그도 이 클래스를 그대로 써서 효과와 같은 경로로 적용한다.
    /// </summary>
    public sealed class EventRoadDifficultyEffects
    {
        public const string LegMode = "leg";
        public const string DaysMode = "days";
        public const string PermanentMode = "permanent";

        private readonly ICurrentLegDifficulty leg;
        private readonly ICityPairDifficultyEditor pairs;
        private readonly IDateReader date; // 선택적 - 없으면 날짜 기간 방식은 실패한다(만료시킬 날짜가 없어 영구로 남기 때문)
        private readonly Func<string> sourceEventId;

        /// <param name="sourceEventId">사건 Id의 이벤트 부분(설계 89 §5.2) - 이벤트 효과는 진행 중 이벤트 Id, 디버그는 CityPairDifficultyIds.DebugSource.</param>
        public EventRoadDifficultyEffects(ICurrentLegDifficulty leg, ICityPairDifficultyEditor pairs, IDateReader date, Func<string> sourceEventId)
        {
            this.leg = leg;
            this.pairs = pairs;
            this.date = date;
            this.sourceEventId = sourceEventId;
        }

        /// <summary>방식 이름 판정 - 대본 검증기도 이것을 쓴다(방식 목록을 한 곳에, 2026-10-16 보완 검진).</summary>
        public static bool IsMode(string mode) => mode == LegMode || mode == DaysMode || mode == PermanentMode;

        public bool TryApply(int change, string mode, int days, out string resultText)
        {
            resultText = null;
            if (leg == null || !leg.HasLeg) return false;
            switch (mode)
            {
                case LegMode:
                    leg.AdjustCurrentLeg(change);
                    resultText = EventEffectFormat.Difficulty(change, "이번 구간");
                    return true;
                case DaysMode:
                    if (pairs == null || days <= 0 || date == null) return false;
                    pairs.Apply(leg.CurrentPair, sourceEventId?.Invoke(), change, date.Today.TotalDays + days);
                    resultText = EventEffectFormat.Difficulty(change, $"{days}일");
                    return true;
                case PermanentMode:
                    if (pairs == null) return false;
                    pairs.Apply(leg.CurrentPair, sourceEventId?.Invoke(), change, null);
                    resultText = EventEffectFormat.Difficulty(change, null);
                    return true;
                default:
                    return false;
            }
        }
    }
}

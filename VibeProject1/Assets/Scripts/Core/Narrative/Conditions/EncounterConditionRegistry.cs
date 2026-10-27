using System;
using System.Collections.Generic;
using System.Globalization;

namespace Game.Core
{
    /// <summary>조건 키 하나의 판정기. 새 조건 종류는 키(EncounterConditionKeys)와 이 구현 하나를 추가한다(기획 86 §5.3).</summary>
    public interface IEncounterConditionEvaluator
    {
        string Key { get; }
        bool IsValidArgument(string argument);
        bool Evaluate(string argument, EncounterConditionContext context);
    }

    /// <summary>
    /// 키별 판정기 등록부(설계 87 §5.1). 임포터는 IsValidArgument로 인자 형식을 미리 막고, 실행 중엔 형식이 틀리거나 키를 모르면 예외 없이 거짓이다 -
    /// 손으로 고친 자산 하나가 상행 전체를 멈추지 않게 하기 위해서다.
    /// </summary>
    public sealed class EncounterConditionRegistry
    {
        private readonly Dictionary<string, IEncounterConditionEvaluator> evaluators = new();

        public static EncounterConditionRegistry CreateDefault()
        {
            var registry = new EncounterConditionRegistry();
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Month,
                a => EncounterRange.TryParseInt(a, out var lo, out var hi) && IsMonth(lo) && IsMonth(hi),
                (a, c) => EncounterRange.TryParseInt(a, out var lo, out var hi) && EncounterRange.InCyclicRange(c.Today.Month, lo, hi)));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Date,
                a => EncounterRange.TryParseDayOfYearRange(a, out _, out _),
                (a, c) => EncounterRange.TryParseDayOfYearRange(a, out var s, out var e)
                    && EncounterRange.InCyclicRange((c.Today.Month - 1) * GameDate.DaysPerMonth + (c.Today.Day - 1), s, e)));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Progress,
                a => EncounterRange.TryParseFloat(a, out var lo, out var hi) && lo <= hi,
                (a, c) => EncounterRange.TryParseFloat(a, out var lo, out var hi) && c.Progress >= lo && c.Progress <= hi));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Region, IsInt,
                (a, c) => c.HasLeg && TryInt(a, out var r) && (c.FromRegionId == r || c.ToRegionId == r)));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Road,
                a => TryRoad(a, out _, out _),
                (a, c) => c.HasLeg && TryRoad(a, out var x, out var y)
                    && ((c.FromCityId == x && c.ToCityId == y) || (c.FromCityId == y && c.ToCityId == x))));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.From, IsInt, (a, c) => c.HasLeg && TryInt(a, out var id) && c.FromCityId == id));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.To, IsInt, (a, c) => c.HasLeg && TryInt(a, out var id) && c.ToCityId == id));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Flag, IsNonEmpty, (a, c) => c.History != null && c.History.HasFlag(a)));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Seen, IsNonEmpty, (a, c) => c.History != null && c.History.HasOccurred(a)));
            registry.Register(new DelegateEvaluator(EncounterConditionKeys.Quest, IsNonEmpty, (a, c) => c.Quests != null && c.Quests.IsEventDue(a.Trim(), c.Progress)));
            return registry;
        }

        public void Register(IEncounterConditionEvaluator evaluator) => evaluators[evaluator.Key] = evaluator;

        public bool IsValidArgument(string key, string argument)
            => key != null && evaluators.TryGetValue(key, out var e) && e.IsValidArgument(argument);

        public bool Evaluate(EncounterCondition condition, EncounterConditionContext context)
        {
            if (condition.Key == null || !evaluators.TryGetValue(condition.Key, out var evaluator)) return false;
            if (!evaluator.IsValidArgument(condition.Argument)) return false;
            var result = evaluator.Evaluate(condition.Argument, context);
            return condition.Negated ? !result : result;
        }

        public bool EvaluateAll(IReadOnlyList<EncounterCondition> conditions, EncounterConditionContext context)
        {
            if (conditions == null) return true;
            for (var i = 0; i < conditions.Count; i++)
            {
                if (!Evaluate(conditions[i], context)) return false;
            }
            return true;
        }

        private static bool IsMonth(int value) => value >= 1 && value <= GameDate.MonthsPerYear;
        private static bool IsNonEmpty(string a) => !string.IsNullOrWhiteSpace(a);
        private static bool IsInt(string a) => TryInt(a, out _);
        private static bool TryInt(string a, out int value) => int.TryParse(a?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        // `a-b` = 두 도시를 잇는 도로(방향 무관).
        private static bool TryRoad(string a, out int x, out int y)
        {
            x = y = 0;
            var parts = a?.Split('-');
            return parts != null && parts.Length == 2 && TryInt(parts[0], out x) && TryInt(parts[1], out y);
        }

        private sealed class DelegateEvaluator : IEncounterConditionEvaluator
        {
            private readonly Func<string, bool> isValid;
            private readonly Func<string, EncounterConditionContext, bool> evaluate;

            public DelegateEvaluator(string key, Func<string, bool> isValid, Func<string, EncounterConditionContext, bool> evaluate)
            {
                Key = key;
                this.isValid = isValid;
                this.evaluate = evaluate;
            }

            public string Key { get; }
            public bool IsValidArgument(string argument) => isValid(argument);
            public bool Evaluate(string argument, EncounterConditionContext context) => evaluate(argument, context);
        }
    }
}

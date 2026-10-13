using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 조건 열 문자열(`region:2; !flag:x`)을 조건 목록으로 바꾼다(설계 87 §2.3). 인자 형식은 키별 판정기가 검사한다(EncounterConditionRegistry.IsValidArgument) -
    /// 여기서는 구문과 키만 본다.
    /// </summary>
    public static class EncounterConditionParser
    {
        public static List<EncounterCondition> Parse(string text)
        {
            var result = new List<EncounterCondition>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var raw in text.Split(';'))
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;

                var negated = part[0] == '!';
                if (negated) part = part.Substring(1).TrimStart();

                var colon = part.IndexOf(':');
                if (colon <= 0 || colon == part.Length - 1)
                {
                    throw new FormatException($"조건 '{raw.Trim()}'은(는) '키:값' 형식이어야 한다.");
                }

                var key = part.Substring(0, colon).Trim();
                var argument = part.Substring(colon + 1).Trim();
                if (!EncounterConditionKeys.IsKnown(key))
                {
                    throw new FormatException($"알 수 없는 조건 키 '{key}' - 알려진 키: {string.Join(", ", EncounterConditionKeys.All)}.");
                }
                if (argument.Length == 0)
                {
                    throw new FormatException($"조건 '{raw.Trim()}'의 값이 비어 있다.");
                }

                result.Add(new EncounterCondition(key, negated, argument));
            }
            return result;
        }
    }
}

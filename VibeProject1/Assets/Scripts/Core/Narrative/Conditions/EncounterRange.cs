using System.Globalization;

namespace Game.Core
{
    /// <summary>조건 인자의 범위 표기 해석(설계 87 §5.1). `3` = 3~3, `3-5` = 3~5. 형식이 틀리면 false - 실행 중엔 예외 대신 "거짓"으로 처리한다.</summary>
    public static class EncounterRange
    {
        public static bool TryParseInt(string text, out int min, out int max)
        {
            min = max = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split('-');
            if (parts.Length == 1)
            {
                if (!int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out min)) return false;
                max = min;
                return true;
            }
            return parts.Length == 2
                && int.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out min)
                && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out max);
        }

        public static bool TryParseFloat(string text, out float min, out float max)
        {
            min = max = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split('-');
            if (parts.Length == 1)
            {
                if (!float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out min)) return false;
                max = min;
                return true;
            }
            return parts.Length == 2
                && float.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out min)
                && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out max);
        }

        /// <summary>`월/일-월/일` → 해 안의 일 번호(0부터). 시작 > 끝이면 해를 넘는 범위다.</summary>
        public static bool TryParseDayOfYearRange(string text, out int start, out int end)
        {
            start = end = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Split('-');
            return parts.Length == 2 && TryParseMonthDay(parts[0], out start) && TryParseMonthDay(parts[1], out end);
        }

        /// <summary>순환 범위 포함 판정 - min ≤ max면 일반 범위, 아니면 끝을 넘어 처음으로 이어지는 범위.</summary>
        public static bool InCyclicRange(int value, int min, int max) => min <= max ? value >= min && value <= max : value >= min || value <= max;

        private static bool TryParseMonthDay(string text, out int dayOfYear)
        {
            dayOfYear = 0;
            var md = text.Trim().Split('/');
            if (md.Length != 2) return false;
            if (!int.TryParse(md[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)) return false;
            if (!int.TryParse(md[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var day)) return false;
            if (month < 1 || month > GameDate.MonthsPerYear || day < 1 || day > GameDate.DaysPerMonth) return false;
            dayOfYear = (month - 1) * GameDate.DaysPerMonth + (day - 1);
            return true;
        }
    }
}

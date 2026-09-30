namespace Game.Core
{
    /// <summary>
    /// 칸 마스크 문자열(`/`로 줄 구분, 위 줄부터)의 공통 파싱(Docs/설계/66번 §2.1). 적재 모양(InventoryShape, `0`/`1`)과
    /// 대열 모양(FormationAreaShape, `0`/`1`/`2`)이 줄 나누기·줄 길이·허용 문자 검사를 같이 쓰도록 한 곳에 둔다 - 각 모양 타입은
    /// 자기 규칙(적재 칸 1개 이상 / 기준 칸 정확히 1개)만 더한다.
    /// </summary>
    internal static class CellMaskParser
    {
        public const char RowSeparator = '/';

        /// <summary>성공하면 cells[x, y]에 문자를 담는다(x = 왼쪽부터, y = 위 줄부터).</summary>
        public static bool TryParse(string mask, string allowedChars, out char[,] cells, out string error)
        {
            cells = null;
            if (string.IsNullOrWhiteSpace(mask))
            {
                error = "모양 문자열이 비어 있다.";
                return false;
            }

            var rows = mask.Trim().Split(RowSeparator);
            var width = rows[0].Length;
            var parsed = new char[width, rows.Length];
            for (var y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != width || width == 0)
                {
                    error = $"{y + 1}번째 줄의 길이({rows[y].Length})가 첫 줄({width})과 다르거나 비어 있다.";
                    return false;
                }

                for (var x = 0; x < width; x++)
                {
                    var c = rows[y][x];
                    if (allowedChars.IndexOf(c) < 0)
                    {
                        error = $"허용되지 않는 문자 '{c}'({y + 1}번째 줄) - '{allowedChars}' 중에서만 쓴다.";
                        return false;
                    }
                    parsed[x, y] = c;
                }
            }

            cells = parsed;
            error = null;
            return true;
        }
    }
}

using System.Text;

namespace Game.Core
{
    /// <summary>
    /// 그리드 한 구역의 모양(외곽 가로×세로 + 칸별 적재 가능 여부, Docs/기획/63번 §3.1, 설계 64번 §2.1). 마차 적재 공간은
    /// 테이블 마스크 문자열에서 오고, 나머지 인벤토리 3종은 직사각형이다. 파서를 런타임 어셈블리에 두는 이유는 임포터(검증)와
    /// 카탈로그(변환)가 같은 규칙 하나를 쓰게 하기 위해서다. 줄 파싱은 대열 모양과 공용(CellMaskParser, 설계 66번 §2.1). 불변이라 여러 곳에서 공유해도 안전하다.
    /// </summary>
    public sealed class InventoryShape
    {
        private const char UsableCell = '1';
        private const char BlockedCell = '0';
        private const string AllowedChars = "10";

        private readonly bool[,] usable;

        public int Width { get; }
        public int Height { get; }
        public int UsableCount { get; }

        private InventoryShape(bool[,] usable)
        {
            this.usable = usable;
            Width = usable.GetLength(0);
            Height = usable.GetLength(1);
            for (var x = 0; x < Width; x++)
            {
                for (var y = 0; y < Height; y++)
                {
                    if (usable[x, y]) UsableCount++;
                }
            }
        }

        public static InventoryShape Rectangle(int width, int height)
        {
            var cells = new bool[width < 0 ? 0 : width, height < 0 ? 0 : height];
            for (var x = 0; x < cells.GetLength(0); x++)
            {
                for (var y = 0; y < cells.GetLength(1); y++) cells[x, y] = true;
            }
            return new InventoryShape(cells);
        }

        /// <summary>
        /// 마스크 문자열을 읽는다. `/`로 줄을 나누고(위 줄부터) `1` = 적재 가능, `0` = 막힌 칸. 줄 길이가 모두 같아야 하고
        /// 적재 가능 칸이 1개 이상이어야 한다. 실패하면 error에 이유를 담는다.
        /// </summary>
        public static bool TryParse(string mask, out InventoryShape shape, out string error)
        {
            shape = null;
            if (!CellMaskParser.TryParse(mask, AllowedChars, out var chars, out error)) return false;

            var cells = new bool[chars.GetLength(0), chars.GetLength(1)];
            for (var x = 0; x < cells.GetLength(0); x++)
            {
                for (var y = 0; y < cells.GetLength(1); y++) cells[x, y] = chars[x, y] == UsableCell;
            }

            var parsed = new InventoryShape(cells);
            if (parsed.UsableCount == 0)
            {
                error = "적재 가능 칸이 하나도 없다.";
                return false;
            }

            shape = parsed;
            error = null;
            return true;
        }

        public bool IsUsable(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height && usable[x, y];

        /// <summary>좌상단 topLeft에서 width×height 범위의 모든 칸이 적재 가능인지 - 외곽 밖이나 막힌 칸이 하나라도 있으면 false.</summary>
        public bool ContainsRect(GridPosition topLeft, int width, int height)
        {
            if (width <= 0 || height <= 0) return false;
            if (topLeft.X < 0 || topLeft.Y < 0 || topLeft.X + width > Width || topLeft.Y + height > Height) return false;

            for (var x = topLeft.X; x < topLeft.X + width; x++)
            {
                for (var y = topLeft.Y; y < topLeft.Y + height; y++)
                {
                    if (!usable[x, y]) return false;
                }
            }
            return true;
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            for (var y = 0; y < Height; y++)
            {
                if (y > 0) builder.Append(CellMaskParser.RowSeparator);
                for (var x = 0; x < Width; x++) builder.Append(usable[x, y] ? UsableCell : BlockedCell);
            }
            return builder.ToString();
        }
    }
}

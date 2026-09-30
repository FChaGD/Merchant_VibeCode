using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 마차·시설·디버그 핀이 여는 대열 영역의 모양(Docs/기획/65번 §3.1, 설계 66번 §2). 테이블 마스크 문자열(`/` 줄 구분,
    /// `1` = 대열 칸, `0` = 대열 아님, `2` = 기준 칸 = 유닛이 놓인 칸)에서 만든다. 방향은 정비창 격자 화면 기준이고 회전하지 않는다.
    /// 영역 계산은 배치할 때마다 여러 번 돌아가므로 마스크를 매번 훑지 않도록 **기준 칸 기준 오프셋 목록**으로 한 번만 바꿔 둔다.
    /// 대열 칸이 기준 칸과 떨어져 있어도 유효하다(기획 65번 §3.5 - 연결 판정이 칸 단위라 그대로 동작). 불변이라 공유해도 안전하다.
    /// </summary>
    public sealed class FormationAreaShape
    {
        private const char AreaCell = '1';
        private const char EmptyCell = '0';
        private const char AnchorCell = '2';
        private const string AllowedChars = "012";

        private readonly Vector2Int[] offsets;
        private readonly int width;
        private readonly int height;
        private readonly Vector2Int anchor;

        /// <summary>기준 칸 기준 (열 차, 행 차). 행 차가 음수면 위쪽. 기준 칸 자신(0, 0)을 포함한다.</summary>
        public IReadOnlyList<Vector2Int> Offsets => offsets;
        /// <summary>기준 칸에서 가장 먼 대열 칸까지의 가로·세로 거리 중 최댓값 - 마을 정비창 칸 표시 범위(기획 65번 §3.4).</summary>
        public int MaxReach { get; }

        /// <summary>기준 칸 1칸 - 모양이 없는 유닛, 임포트 전 자산의 대체값(설계 66번 §8-3).</summary>
        public static FormationAreaShape Single { get; } = new(new[] { Vector2Int.zero }, 1, 1, Vector2Int.zero);

        private FormationAreaShape(Vector2Int[] offsets, int width, int height, Vector2Int anchor)
        {
            this.offsets = offsets;
            this.width = width;
            this.height = height;
            this.anchor = anchor;
            foreach (var offset in offsets) MaxReach = Mathf.Max(MaxReach, Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y)));
        }

        /// <summary>(2 × reach + 1)² 전부 대열, 가운데 기준 칸 - 디버그 핀 기본값·테스트용.</summary>
        public static FormationAreaShape Square(int reach)
        {
            reach = Mathf.Max(0, reach);
            var list = new List<Vector2Int>();
            for (var dy = -reach; dy <= reach; dy++)
            {
                for (var dx = -reach; dx <= reach; dx++) list.Add(new Vector2Int(dx, dy));
            }
            return new FormationAreaShape(list.ToArray(), reach * 2 + 1, reach * 2 + 1, new Vector2Int(reach, reach));
        }

        /// <summary>마스크를 읽는다. 줄 길이가 같고 `0`/`1`/`2`만 쓰며 `2`가 정확히 1개여야 한다. 실패하면 error에 이유를 담는다.</summary>
        public static bool TryParse(string mask, out FormationAreaShape shape, out string error)
        {
            shape = null;
            if (!CellMaskParser.TryParse(mask, AllowedChars, out var cells, out error)) return false;

            var w = cells.GetLength(0);
            var h = cells.GetLength(1);
            var anchors = 0;
            var anchorAt = Vector2Int.zero;
            for (var x = 0; x < w; x++)
            {
                for (var y = 0; y < h; y++)
                {
                    if (cells[x, y] != AnchorCell) continue;
                    anchors++;
                    anchorAt = new Vector2Int(x, y);
                }
            }

            if (anchors != 1)
            {
                error = $"기준 칸('{AnchorCell}')은 정확히 1개여야 한다(현재 {anchors}개).";
                return false;
            }

            var list = new List<Vector2Int>();
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    if (cells[x, y] != EmptyCell) list.Add(new Vector2Int(x - anchorAt.x, y - anchorAt.y));
                }
            }

            shape = new FormationAreaShape(list.ToArray(), w, h, anchorAt);
            return true;
        }

        public override string ToString()
        {
            var grid = new char[width, height];
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++) grid[x, y] = EmptyCell;
            }
            foreach (var offset in offsets) grid[anchor.x + offset.x, anchor.y + offset.y] = AreaCell;
            grid[anchor.x, anchor.y] = AnchorCell;

            var builder = new StringBuilder();
            for (var y = 0; y < height; y++)
            {
                if (y > 0) builder.Append(CellMaskParser.RowSeparator);
                for (var x = 0; x < width; x++) builder.Append(grid[x, y]);
            }
            return builder.ToString();
        }
    }
}

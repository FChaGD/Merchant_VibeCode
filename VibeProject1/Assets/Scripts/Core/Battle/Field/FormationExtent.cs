namespace Game.Core
{
    /// <summary>
    /// 전투로 옮길 대열의 범위(정비창 판 좌표 기준 중심 + 열·행 반폭, 단위는 칸, Docs/설계/60번 §7). 아군 전장 좌표와 스폰·전장·
    /// 활동 반경이 모두 이 값에서 나온다. 예전엔 "열 수"만 넘기고 행은 2개로 가정해, 행이 늘면 대형이 한쪽으로 치우치고 반경에
    /// 행 방향 폭이 빠졌다 - 중심과 반폭을 행·열 모두 명시해 판 크기·대열 모양과 무관하게 대칭이 되게 한다.
    /// </summary>
    public readonly struct FormationExtent
    {
        public float CenterColumn { get; }
        public float CenterRow { get; }
        public float HalfColumnSpan { get; }
        public float HalfRowSpan { get; }

        public FormationExtent(float centerColumn, float centerRow, float halfColumnSpan, float halfRowSpan)
        {
            CenterColumn = centerColumn;
            CenterRow = centerRow;
            HalfColumnSpan = halfColumnSpan;
            HalfRowSpan = halfRowSpan;
        }

        /// <summary>격자 전체를 대열 범위로 본다 - 판 전체가 곧 대열이던 기존 계산과 같은 결과를 낸다(설계 60번 §11.1 1단계).</summary>
        public static FormationExtent FromGrid(int columnCount, int rowCount)
        {
            var halfColumns = (columnCount - 1) / 2f;
            var halfRows = (rowCount - 1) / 2f;
            return new FormationExtent(halfColumns, halfRows, halfColumns, halfRows);
        }
    }
}

using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 - 대열 범위를 정비창 배치가 아니라 열/행 수 인스턴스 상태(ColumnCount/RowCount)로 들고 있고,
    /// 좌표·반경 계산은 실제 게임과 같은 BattleFieldLayout에 위임한다(Docs/설계/60번 §7.3 - 예전엔 "행 수 가변"만 다른 같은
    /// 공식을 따로 들고 있었으나, 계약이 FormationExtent로 바뀌면서 차이가 사라졌다). 이 상태는 Extent로 변환해 넘긴다.
    /// 대열 범위 기즈모(BattleTestExtentGizmoView)가 매 프레임 ColumnCount/RowCount를 읽어 박스를
    /// 그리고, 모서리 드래그/숫자 입력이 이 값을 직접 바꾼다.
    /// </summary>
    public class BattleTestFieldLayout : IAllyPositionLayout, IBattleFieldGeometry
    {
        // 배틀 테스트 기본 대열 범위 - 정비창 외곽 판(FormationLayout, 50 × 50)과 무관한 이 씬만의 값이다(예전 정비창 기본 8 × 2를 유지).
        private const int DefaultColumnCount = 8;
        private const int DefaultRowCount = 2;

        private readonly BattleFieldLayout formulas = new();

        public int ColumnCount { get; set; } = DefaultColumnCount;
        public int RowCount { get; set; } = DefaultRowCount;

        /// <summary>배틀 테스트의 대열 범위 - 격자 전체(원점 중심 대칭).</summary>
        public FormationExtent Extent => FormationExtent.FromGrid(ColumnCount, RowCount);

        public Vector2 ComputeAllyPosition(int column, int row, FormationExtent extent) => formulas.ComputeAllyPosition(column, row, extent);

        public Vector2 ComputeSpawnPoint(int spawnPointIndex, FormationExtent extent) => formulas.ComputeSpawnPoint(spawnPointIndex, extent);

        public float ComputeFleeTravelDistance(FormationExtent extent) => formulas.ComputeFleeTravelDistance(extent);

        public float ComputeFieldRadius(FormationExtent extent) => formulas.ComputeFieldRadius(extent);

        public float ComputeStandardActivityRadius(FormationExtent extent) => formulas.ComputeStandardActivityRadius(extent);

        public float ComputeSpawnRadius(FormationExtent extent) => formulas.ComputeSpawnRadius(extent);

        // 대열 범위 기즈모(BattleTestExtentGizmoView)가 그릴 사각형의 네 모서리(월드 좌표, 원점 중심).
        // ComputeAllyPosition과 같은 축 대응을 쓴다 - column→Y축, row→X축(클래스 요약 주석의 "반시계
        // 90도 회전" 그대로). "가로/세로"로 이름 붙이면 이 축 반전과 헷갈리므로 축을 직접 반환한다.
        public Vector2 ExtentMin => new(-HalfExtentX, -HalfExtentY);
        public Vector2 ExtentMax => new(HalfExtentX, HalfExtentY);

        private float HalfExtentX => (RowCount - 1) / 2f * BattleFieldRadiusFormulas.RowSpacing;
        private float HalfExtentY => (ColumnCount - 1) / 2f * BattleFieldRadiusFormulas.ColumnSpacing;

        // 모서리 드래그(BattleTestExtentGizmoView)가 호출한다 - 원점에서 대칭이라 절댓값의 2배가
        // 전체 폭/높이다. HalfExtent = (Count-1)/2*Spacing 공식의 역산(Count = 2*Half/Spacing + 1).
        public void SetExtentFromCorner(Vector2 worldCorner)
        {
            RowCount = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.Abs(worldCorner.x) / BattleFieldRadiusFormulas.RowSpacing + 1f));
            ColumnCount = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.Abs(worldCorner.y) / BattleFieldRadiusFormulas.ColumnSpacing + 1f));
        }
    }
}

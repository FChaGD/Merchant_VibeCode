using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 정비창(Formation) 슬롯 좌표를 전장 좌표로 바꾼다 - 열이 상행 진행 방향(전방/후방)을
    /// 나타내는 세로축, 행이 대형 폭을 나타내는 가로축이 되도록 배치 UI를 반시계 90도 회전시킨
    /// 규칙이다(Docs/기획/08-2026-09-01-전투_해석로직_기획.md §2). 간격은 Field 전투 뷰 실제 크기가 정해지기 전
    /// 임시값이라 상수로 뒀다 - 이 클래스는 MonoBehaviour가 아닌 순수 C# 객체라 SerializeField를
    /// 붙여도 인스펙터에 노출되지 않는다(Docs/설계/06-2026-08-31-전투_핵심루프_아키텍처.md §4).
    /// 스폰 반지름은 더 이상 고정값이 아니다 - 대형(아군 배치) 크기가 커지면 적도 그만큼 더 바깥에서
    /// 스폰해야 "전장을 벗어난 곳에서 스폰"이라는 전제가 대형 크기와 무관하게 항상 성립한다. 도주
    /// 이탈 거리도 같은 스폰 반지름에서 파생시켜, 두 값이 서로 다른 임의의 상수로 따로 놀지 않게 한다.
    /// 아군 좌표 변환(IAllyPositionLayout)과 스폰/반지름 계산(IBattleFieldGeometry)을 인터페이스
    /// 레벨에서 분리했다(Docs/설계/12번 §5.2) - 구현은 대열 반지름 계산을 공유해야 해서 그대로 하나다.
    /// 대열 범위(FormationExtent)의 중심이 전장 원점에 오도록 행·열 양방향으로 계산한다 - 예전의 "행 2개 고정" 가정을
    /// 없앴다(Docs/설계/60번 §7). 배틀 테스트 씬(BattleTestFieldLayout)도 이 계산을 그대로 쓴다.
    /// </summary>
    public class BattleFieldLayout : IAllyPositionLayout, IBattleFieldGeometry
    {
        public Vector2 ComputeAllyPosition(int column, int row, FormationExtent extent)
        {
            var y = (column - extent.CenterColumn) * BattleFieldRadiusFormulas.ColumnSpacing;
            var x = (row - extent.CenterRow) * BattleFieldRadiusFormulas.RowSpacing;
            return new Vector2(x, y);
        }

        public Vector2 ComputeSpawnPoint(int spawnPointIndex, FormationExtent extent)
            => BattleFieldRadiusFormulas.ComputeSpawnPoint(spawnPointIndex, ComputeSpawnRadius(extent));

        // 도주 유닛이 이만큼 이동하면 "전장을 완전히 벗어났다"고 본다 - 전장(실제 교전이 벌어지는
        // 범위)의 반지름과 같은 값이다.
        public float ComputeFleeTravelDistance(FormationExtent extent) => ComputeFieldRadius(extent);

        public float ComputeFieldRadius(FormationExtent extent) => BattleFieldRadiusFormulas.ComputeFieldRadius(ComputeSpawnRadius(extent));

        // 대형 중심 기준 "활동 반경 - 표준" 프리셋(Docs/기획/12번 §2.2) - 스폰/전장 반지름과 같은
        // 대열 반지름 파생 패밀리지만 마진 값(TacticsTuning)이 다르다.
        public float ComputeStandardActivityRadius(FormationExtent extent) => BattleFieldRadiusFormulas.ComputeStandardActivityRadius(FormationExtentRadius(extent));

        public float ComputeSpawnRadius(FormationExtent extent) => BattleFieldRadiusFormulas.ComputeSpawnRadius(FormationExtentRadius(extent));

        // 대형 중심에서 가장 먼 모서리까지의 거리 - 행·열 반폭을 각 간격으로 환산한 대각 거리.
        private static float FormationExtentRadius(FormationExtent extent)
        {
            var halfColumnExtent = extent.HalfColumnSpan * BattleFieldRadiusFormulas.ColumnSpacing;
            var halfRowExtent = extent.HalfRowSpan * BattleFieldRadiusFormulas.RowSpacing;
            return Mathf.Sqrt(halfColumnExtent * halfColumnExtent + halfRowExtent * halfRowExtent);
        }
    }
}

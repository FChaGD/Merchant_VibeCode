using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 방진선 하나를 그리는 데 필요한 기하 계산 결과를 한 번에 묶는다(Docs/설계/12번 §12.5) -
    /// anchorCenter/enemyCenter/axisDir/range 각각을 out 매개변수로 흩어놓지 않기 위한 값 객체.
    /// </summary>
    public readonly struct FrontlineLineGeometry
    {
        // 보호대상 후보군 중 이 군집이 실제로 위협하는 대상들의 위치 평균(§12.3).
        public Vector2 AnchorCenter { get; }
        // 이 군집(적들)의 위치 평균.
        public Vector2 EnemyCenter { get; }
        // anchorCenter→enemyCenter 방향(정규화). 두 점이 겹치면(0벡터) Vector2.zero.
        public Vector2 AxisDir { get; }
        // 군집 중 최대 사거리 - "적이 보호대상을 때리기 시작하는 경계선" 계산 기준.
        public float Range { get; }
        // anchorCenter에서 axisDir 방향으로 range만큼 나간 뒤 상행 전체 반경으로 clamp한 지점.
        public Vector2 CanonicalPoint { get; }
        // axisDir에 수직인 방향 - 슬롯을 늘어놓는 축(§12.6).
        public Vector2 LineDir { get; }

        public FrontlineLineGeometry(Vector2 anchorCenter, Vector2 enemyCenter, Vector2 axisDir, float range, Vector2 canonicalPoint, Vector2 lineDir)
        {
            AnchorCenter = anchorCenter;
            EnemyCenter = enemyCenter;
            AxisDir = axisDir;
            Range = range;
            CanonicalPoint = canonicalPoint;
            LineDir = lineDir;
        }
    }
}

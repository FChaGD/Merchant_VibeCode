using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 정비창 격자 여백(대열 경계 상자 바깥 칸 수, 설계 60번 §15.3) 계산. 마차를 판 어디든 놓을 수 있는 편집(마을 정비창, 상행 중 정리 모드 -
    /// 설계 79번 §8)은 연결 가능한 가장 먼 칸까지 보여야 해서 같은 계산을 쓴다 - 한 패널의 메서드를 다른 패널이 빌려 쓰지 않도록 여기에 둔다.
    /// </summary>
    internal static class FormationVisibleMargin
    {
        public const int Default = 2;

        // 연결 가능한 가장 먼 칸 = 대열 경계에서 (새 마차 범위 + 1)칸 - 팔레트의 마차까지 포함해 로스터 마차 범위 최댓값으로 정한다(설계 60번 §15.3).
        // 범위 = 기준 칸에서 가장 먼 대열 칸 거리(기획 65번 §3.4). 방향별로 따로 계산하지 않고 사방에 똑같이 쓴다 - 일부 방향에 칸이 더
        // 깔리는 대신 규칙이 단순하다.
        public static int ForAnywhereWagons(ICaravanRosterProvider rosterProvider)
        {
            var maxReach = -1;
            if (rosterProvider != null)
            {
                foreach (var unit in rosterProvider.GetRoster())
                {
                    if (unit.Kind == FormationUnitKind.Wagon && unit is IAreaAnchorUnit anchor) maxReach = Mathf.Max(maxReach, anchor.AreaShape.MaxReach);
                }
            }
            return maxReach < 0 ? Default : Mathf.Max(Default, maxReach + 1);
        }
    }
}

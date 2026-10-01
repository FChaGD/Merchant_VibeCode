namespace Game.Core
{
    /// <summary>
    /// 마차·시설 1기 + 정비창 칸(열·행). 묶음 판정(8방향 이웃, 기획 71번 §4-6)을 월드 좌표 거리가 아니라 칸으로
    /// 하려고 전투 생성 시점의 칸 정보를 함께 넘긴다(설계 72번 §3.2).
    /// </summary>
    public readonly struct ProtectedPlacement
    {
        public readonly BattleProtectedUnit Unit;
        public readonly int Column;
        public readonly int Row;

        public ProtectedPlacement(BattleProtectedUnit unit, int column, int row)
        {
            Unit = unit;
            Column = column;
            Row = row;
        }
    }
}

namespace Game.Core
{
    /// <summary>
    /// 시계방향 90° 회전 계산. 드래그 중 Q 회전 시 "잡은 칸"이 포인터 아래를 벗어나지 않도록 잡은 칸의
    /// 아이템 내부 좌표를 같은 회전으로 변환하는 데 쓴다(Docs/기획/39번 §3.4, 설계 40번 §5.2).
    /// 로컬 좌표는 x = 오른쪽, y = 아래.
    /// </summary>
    public static class InventoryRotation
    {
        public static int Normalize(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

        public static int RotateClockwise(int quarterTurns) => Normalize(quarterTurns + 1);

        /// <summary>
        /// 회전 전 세로 칸 수가 heightBeforeRotation인 아이템의 내부 칸 좌표를 시계방향 90° 회전 후 좌표로
        /// 변환한다. (x, y) → (height − 1 − y, x). 회전 후 크기는 가로·세로가 뒤바뀐다.
        /// </summary>
        public static GridPosition RotateLocalCellClockwise(GridPosition local, int heightBeforeRotation)
        {
            return new GridPosition(heightBeforeRotation - 1 - local.Y, local.X);
        }
    }
}

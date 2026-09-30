namespace Game.Core
{
    /// <summary>
    /// 재배치 한 건의 목표 상태. Position이 null이면 임시 보관 영역이다. 이동·교환·임시 보관·자동 정렬이
    /// 전부 이 목록 하나로 표현되어 InventoryGrid.TryApplyPlacements에서 원자적으로 처리된다(Docs/설계/40번 §3.2).
    /// SectionId는 목표 구역(마차 간 이동, 설계 64번 §3.2). null이면 첫 섹션이며, 임시 보관이면 무시한다.
    /// </summary>
    public readonly struct ItemPlacement
    {
        public string InstanceId { get; }
        public GridPosition? Position { get; }
        public int QuarterTurns { get; }
        public string SectionId { get; }

        public ItemPlacement(string instanceId, GridPosition? position, int quarterTurns, string sectionId = null)
        {
            InstanceId = instanceId;
            Position = position;
            QuarterTurns = InventoryRotation.Normalize(quarterTurns);
            SectionId = sectionId;
        }

        public static ItemPlacement ToStaging(string instanceId, int quarterTurns) => new(instanceId, null, quarterTurns);
    }
}

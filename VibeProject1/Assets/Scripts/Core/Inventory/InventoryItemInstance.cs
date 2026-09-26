namespace Game.Core
{
    /// <summary>
    /// 인벤토리에 들어 있는 아이템 한 개(배치 인스턴스). 같은 Definition이라도 배치마다 새 InstanceId를
    /// 발급한다 - 같은 종류를 여러 번 놓아도 서로 구분해 제거할 수 있어야 하기 때문이다.
    /// 회전은 "가로·세로 뒤바뀜" 여부가 아니라 시계방향 90° 회전 횟수로 저장한다 - 점유 모양은 짝/홀만으로
    /// 정해지지만, 드래그 중 잡은 칸 추적과 이후 아이콘 방향 표시에 네 방향 구분이 필요하다(Docs/설계/40번 §3.1).
    /// 임시 보관(IsStaged)은 그리드 칸을 점유하지 않는 상태이며, 그때 Position은 의미가 없다.
    /// </summary>
    public readonly struct InventoryItemInstance
    {
        public string InstanceId { get; }
        public IInventoryItemDefinition Definition { get; }
        public GridPosition Position { get; }
        public int QuarterTurns { get; }
        public bool IsStaged { get; }

        public int Width => QuarterTurns % 2 == 1 ? Definition.FootprintHeight : Definition.FootprintWidth;
        public int Height => QuarterTurns % 2 == 1 ? Definition.FootprintWidth : Definition.FootprintHeight;

        public InventoryItemInstance(string instanceId, IInventoryItemDefinition definition, GridPosition position)
            : this(instanceId, definition, position, 0, false)
        {
        }

        public InventoryItemInstance(string instanceId, IInventoryItemDefinition definition, GridPosition position, int quarterTurns, bool isStaged)
        {
            InstanceId = instanceId;
            Definition = definition;
            Position = position;
            QuarterTurns = InventoryRotation.Normalize(quarterTurns);
            IsStaged = isStaged;
        }
    }
}

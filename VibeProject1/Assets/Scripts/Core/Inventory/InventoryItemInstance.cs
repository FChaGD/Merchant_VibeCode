namespace Game.Core
{
    /// <summary>
    /// 그리드에 실제로 놓인 아이템 한 개(배치 인스턴스). 같은 Definition이라도 배치마다 새 InstanceId를
    /// 발급한다 - 같은 종류를 여러 번 놓아도 서로 구분해 제거할 수 있어야 하기 때문이다.
    /// </summary>
    public readonly struct InventoryItemInstance
    {
        public string InstanceId { get; }
        public IInventoryItemDefinition Definition { get; }
        public GridPosition Position { get; }

        public InventoryItemInstance(string instanceId, IInventoryItemDefinition definition, GridPosition position)
        {
            InstanceId = instanceId;
            Definition = definition;
            Position = position;
        }
    }
}

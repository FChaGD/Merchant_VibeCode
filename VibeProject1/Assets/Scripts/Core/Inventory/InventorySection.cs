namespace Game.Core
{
    /// <summary>
    /// 그리드의 한 구역(Docs/설계/64번 §3.3). 상단 물류품은 마차 1대 = 섹션 1개이고 Id는 마차 Id다 - 목록 순번을 쓰면 마차가
    /// 빠질 때 뒤쪽 아이템 위치가 밀린다(§10-4). 나머지 인벤토리 3종은 섹션 1개이며 표시명이 비어 있다.
    /// 상단 물류품 섹션의 표시명은 "n번 이름"이라 다른 마차가 빠지면 바뀐다(설계 81번 §5.3) - Id는 바뀌지 않는다.
    /// </summary>
    public sealed class InventorySection
    {
        public string Id { get; }
        public string DisplayName { get; private set; }
        public InventoryShape Shape { get; }

        public InventorySection(string id, string displayName, InventoryShape shape)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Shape = shape;
        }

        internal void Rename(string displayName) => DisplayName = displayName ?? string.Empty;
    }
}

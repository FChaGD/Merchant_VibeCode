namespace Game.Core
{
    /// <summary>
    /// 그리드의 한 구역(Docs/설계/64번 §3.3). 상단 물류품은 마차 1대 = 섹션 1개이고 Id는 마차 Id다 - 목록 순번을 쓰면 마차가
    /// 빠질 때 뒤쪽 아이템 위치가 밀린다(§10-4). 나머지 인벤토리 3종은 섹션 1개이며 표시명이 비어 있다.
    /// </summary>
    public sealed class InventorySection
    {
        public string Id { get; }
        public string DisplayName { get; }
        public InventoryShape Shape { get; }

        public InventorySection(string id, string displayName, InventoryShape shape)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            Shape = shape;
        }
    }
}

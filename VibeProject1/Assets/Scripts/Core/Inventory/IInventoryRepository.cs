namespace Game.Core
{
    public interface IInventoryRepository : IInventoryReader
    {
        // quarterTurns: 시계방향 90° 회전 횟수. 구매 자동 배치처럼 회전한 자리에 들이는 유입 경로가 쓴다(Docs/설계/50번 §5.1).
        // sectionId: 놓을 구역(상단 물류품은 마차 Id, 설계 64번 §4.1). null이면 첫 섹션.
        bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed, int quarterTurns = 0, string sectionId = null);
        bool RemoveItem(string instanceId);
    }
}

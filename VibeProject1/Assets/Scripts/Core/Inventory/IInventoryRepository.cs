namespace Game.Core
{
    public interface IInventoryRepository : IInventoryReader
    {
        bool TryPlaceItem(IInventoryItemDefinition definition, GridPosition position, out InventoryItemInstance placed);
        bool RemoveItem(string instanceId);
    }
}

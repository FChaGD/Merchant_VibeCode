using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// "카탈로그에 어떤 아이템이 존재하는가"(정적 정의 열람) - "그리드에 지금 뭐가 놓여있는가"
    /// (IInventoryReader, 동적 배치 상태)와 책임이 달라 별도 인터페이스로 뽑는다(ISP,
    /// IFormationReader/IFormationRepository 분리 사례와 동일 이유, Docs/설계/35번 §6).
    /// </summary>
    public interface IItemCatalogReader
    {
        IReadOnlyList<IInventoryItemDefinition> CatalogItems { get; }
        bool TryGetDefinition(string id, out IInventoryItemDefinition definition);
    }
}

using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리에 놓일 수 있는 아이템의 정의(카탈로그 레코드). 실제 아이템 데이터 시스템이 없어
    /// 최소 계약만 정의한다(Docs/설계/32번 §2 - PlaceholderFormationUnit/IFormationUnit과 같은 성격).
    /// </summary>
    public interface IInventoryItemDefinition
    {
        string Id { get; }
        string DisplayName { get; }
        Sprite Icon { get; }
        int FootprintWidth { get; }
        int FootprintHeight { get; }
    }
}

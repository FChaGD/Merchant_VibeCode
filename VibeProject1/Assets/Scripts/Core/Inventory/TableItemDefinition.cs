using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ItemDefinitionEntry(테이블 행) + 표시명(String 테이블 조회 결과)을 합쳐 IInventoryItemDefinition
    /// 계약을 만족시키는 어댑터(Docs/설계/35번 §6) - GoldBoxItemDefinition과 같은 역할이지만 특정
    /// 아이템 하나가 아니라 테이블 행 전체에 범용으로 쓰인다.
    /// </summary>
    public readonly struct TableItemDefinition : IInventoryItemDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Sprite Icon { get; }
        public int FootprintWidth { get; }
        public int FootprintHeight { get; }

        public TableItemDefinition(ItemDefinitionEntry entry, string displayName)
        {
            Id = entry.Id;
            DisplayName = displayName;
            Icon = entry.Icon;
            FootprintWidth = entry.FootprintWidth;
            FootprintHeight = entry.FootprintHeight;
        }
    }
}

using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 적재 재화(골드 상자) 아이템 정의(Docs/기획/31번 §3.4, 설계 32번 §4.2). 아이템 카탈로그가 아직
    /// 없는 지금 PlaceholderTradeGoodsInventoryRepository가 직접 소유하는 유일한 특수 정의다.
    /// </summary>
    internal class GoldBoxItemDefinition : IInventoryItemDefinition
    {
        public string Id => "gold-box";
        public string DisplayName => "골드 상자";
        public Sprite Icon { get; }
        public int FootprintWidth => 1;
        public int FootprintHeight => 1;

        public GoldBoxItemDefinition(Sprite icon)
        {
            Icon = icon;
        }
    }
}

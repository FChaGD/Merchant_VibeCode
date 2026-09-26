using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Placeholder 저장소 4종이 공유하는 검증용 초기 배치(Docs/설계/44번 §4.2). 품목은 실제 아이템 테이블의
    /// "placeholder-*" 행이며, 실제 아이템 획득 시스템이 생기면 Placeholder 저장소들과 함께 제거한다.
    /// </summary>
    internal static class PlaceholderInventorySeeder
    {
        /// <summary>
        /// 테이블에 행이 없으면(임포트 전) 건너뛰고, 자리가 없으면 경고만 남긴다 - 검증용 데이터라 실패해도 저장소
        /// 자체는 정상 동작해야 한다. 초기화 중에 호출되므로 호출자는 OnChanged를 발행하지 않는다.
        /// </summary>
        public static void PlaceAll(InventoryGrid grid, IItemCatalogReader catalog, IEnumerable<(string itemId, int x, int y)> items, string ownerName)
        {
            foreach (var (itemId, x, y) in items)
            {
                if (!catalog.TryGetDefinition(itemId, out var definition)) continue;
                if (!grid.TryPlace(definition, new GridPosition(x, y), out _))
                {
                    Debug.LogWarning($"{ownerName}: 초기 아이템 '{itemId}'을(를) ({x}, {y})에 배치하지 못했다.");
                }
            }
        }
    }
}

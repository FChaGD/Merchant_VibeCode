using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 그리드 조회 계약(Docs/설계/32번 §3). 카테고리별 읽기 전용 소비자가 실제로 생기기
    /// 전까지는 카테고리 마커 인터페이스에서 이 계약과 IInventoryRepository를 함께 묶어 쓴다 - 지금
    /// 당장 필요하지 않은 IFormationReader류 분리를 미리 하지 않는다(과잉 설계 방지).
    /// </summary>
    public interface IInventoryReader
    {
        int GridWidth { get; }
        int GridHeight { get; }
        IReadOnlyCollection<InventoryItemInstance> Items { get; }
        bool TryGetItemAt(GridPosition position, out InventoryItemInstance item);

        // Update 폴링 금지(CLAUDE.md 최적화 컨벤션) - 그리드 뷰는 이 이벤트로만 갱신한다.
        event Action OnChanged;
    }
}

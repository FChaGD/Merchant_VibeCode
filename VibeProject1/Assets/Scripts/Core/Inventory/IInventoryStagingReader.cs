using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 임시 보관 영역 조회 전용 계약. "상행 시작" 버튼처럼 "임시 보관에 아이템이 남아 있는가"만 알면 되는
    /// 소비자에게 정리 조작(IInventoryArrangement)까지 노출하지 않기 위해 분리했다(ISP, Docs/설계/40번 §5.5).
    /// OnChanged는 IInventoryReader.OnChanged와 같은 이벤트 멤버 하나로 구현된다.
    /// </summary>
    public interface IInventoryStagingReader
    {
        IReadOnlyList<InventoryItemInstance> StagedItems { get; }
        event Action OnChanged;
    }
}

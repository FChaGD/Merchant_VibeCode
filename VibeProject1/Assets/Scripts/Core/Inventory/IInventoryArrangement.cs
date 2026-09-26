using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업의 정리 조작(이동·교환·회전·임시 보관·자동 정렬) 계약. 인벤토리 유입/유출
    /// (IInventoryRepository의 TryPlaceItem/RemoveItem - 구매·매각 등 시설 쪽 소비자)과 소비자가 달라
    /// 분리했다(ISP, Docs/설계/40번 §3.3). 재배치는 유입/유출이 아니므로 골드 상자 지갑 연동을 거치지 않는다.
    /// </summary>
    public interface IInventoryArrangement : IInventoryStagingReader
    {
        bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements);

        /// <summary>TryApplyPlacements와 같은 검사만 하고 상태는 바꾸지 않는다(드래그 미리보기용).</summary>
        bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements);
    }
}

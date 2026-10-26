using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 인벤토리 팝업의 정리 조작(이동·교환·회전·임시 보관·자동 정렬·버리기) 계약. 인벤토리 유입/유출
    /// (IInventoryRepository의 TryPlaceItem/RemoveItem - 구매·매각 등 시설 쪽 소비자)과 소비자가 달라
    /// 분리했다(ISP, Docs/설계/40번 §3.3).
    /// </summary>
    public interface IInventoryArrangement : IInventoryStagingDiscarder
    {
        bool TryApplyPlacements(IReadOnlyList<ItemPlacement> placements);

        /// <summary>TryApplyPlacements와 같은 검사만 하고 상태는 바꾸지 않는다(드래그 미리보기용).</summary>
        bool CanApplyPlacements(IReadOnlyList<ItemPlacement> placements);

        /// <summary>
        /// 그리드·임시 보관에서 함께 찾아 버린다(Docs/설계/102번 §4.1). 하나라도 없으면 아무것도 지우지 않고 false.
        /// 성공 시 OnChanged 1회. 환급 없음 - 골드 상자도 그냥 사라진다.
        /// </summary>
        bool TryDiscard(IReadOnlyCollection<string> instanceIds);
    }
}

namespace Game.Core
{
    /// <summary>
    /// 임시 보관 조회 + 전부 버리기. 상행 출발은 임시 보관에 물품이 남아 있으면 버림 확인 후 버리고 떠난다(2026-10-15 사용자 결정) -
    /// 출발 화면에 이동·교환 같은 정리 조작까지 노출하지 않으려고 정리 계약(IInventoryArrangement)에서 이 부분만 뽑았다(ISP).
    /// </summary>
    public interface IInventoryStagingDiscarder : IInventoryStagingReader
    {
        /// <summary>임시 보관을 전부 버린다(환급 없음).</summary>
        void DiscardStaged();
    }
}

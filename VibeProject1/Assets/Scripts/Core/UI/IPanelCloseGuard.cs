namespace Game.Core
{
    /// <summary>
    /// 닫기 전에 정리 작업을 하고, 닫을 수 없으면 false를 돌려주는 패널 계약(Docs/설계/40번 §5.4). 인벤토리
    /// 팝업은 임시 보관 아이템을 그리드에 되돌리지 못하면 닫히지 않아야 한다(기획 39번 §3.5). 인벤토리 팝업은
    /// 코디네이터가, depth 패널·모달 팝업(무역품 구매 화면 등)은 PanelChannel이 닫기 직전에 확인한다(설계 50번 §6.3).
    /// </summary>
    public interface IPanelCloseGuard
    {
        bool TryPrepareClose();
    }
}

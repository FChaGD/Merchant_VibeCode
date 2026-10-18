namespace Game.Core
{
    /// <summary>
    /// 이벤트 중 정비창 열람 모드(설계 89 §3·§6.3) - 보기만 하고 편집 요청은 전부 거절한다. 팔레트·여백은 평소와 같게 보여 화면이 바뀌지 않게 한다.
    /// </summary>
    internal sealed class FieldViewOnlyEditPolicy : IFieldFormationEditPolicy
    {
        public bool ShowsInPalette(IFormationUnit unit) => unit != null && unit.Kind != FormationUnitKind.Wagon;
        public int GetVisibleMarginCells() => FormationVisibleMargin.Default;
        public bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex) => false;
        public bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex) => false;
        public bool HandleRemove(string unitId, int slotIndex) => false;
        public bool HandleRedirectMove(string unitId, int newTargetSlotIndex) => false;
    }
}

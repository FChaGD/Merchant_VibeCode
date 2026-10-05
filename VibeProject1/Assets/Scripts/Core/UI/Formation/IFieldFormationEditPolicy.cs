namespace Game.Core
{
    /// <summary>
    /// 상행 중 정비창(FieldFormationPanel)의 편집 정책(설계 79번 §8, §15-12). 평소(배치 활동·소요시간)와 전투 후 정리 모드(즉시 반영·판 어디든
    /// 마차 이동·덩어리 수 기준)는 편집 판정과 반영 방식이 메서드마다 달라, 패널 메서드 안에 모드 분기를 넣지 않고 정책 객체를 바꿔 끼운다.
    /// 패널은 IFormationEditingHandler·IFormationActivityHandler의 편집 요청을 현재 정책에 위임만 한다.
    /// </summary>
    internal interface IFieldFormationEditPolicy
    {
        bool ShowsInPalette(IFormationUnit unit);
        int GetVisibleMarginCells();
        bool HandlePaletteDrop(IFormationUnit unit, int targetSlotIndex);
        bool HandleGridMove(string unitId, int originSlotIndex, int targetSlotIndex);
        bool HandleRemove(string unitId, int slotIndex);

        // 이동 중인 유닛의 도착 고스트(또는 출발 칸 아이콘)를 끌어 놓았을 때 - 평소엔 목적지 재조정, 정리 모드엔 활동 취소 + 즉시 이동.
        // 반영 여부를 돌려준다(정리 모드에서 패널이 대열 상태 표시를 갱신할지 판단).
        bool HandleRedirectMove(string unitId, int newTargetSlotIndex);
    }
}

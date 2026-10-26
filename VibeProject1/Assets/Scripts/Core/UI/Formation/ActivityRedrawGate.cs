namespace Game.Core
{
    /// <summary>
    /// 정비창 활동 표시(오버레이·경로선·이동 아이콘)를 이번 프레임에 다시 그릴지 판정한다(Docs/Refactor/2026-10-26_공통.md O-2).
    /// Field 정비창이 열린 동안 매 프레임 불리므로 활동이 계속 0건이면 건너뛴다. 0건이 된 첫 프레임은 남은 표시를 숨겨야 하므로 그린다.
    /// </summary>
    public sealed class ActivityRedrawGate
    {
        private int previousCount;

        public bool ShouldRedraw(int activityCount)
        {
            var redraw = activityCount > 0 || previousCount > 0;
            previousCount = activityCount;
            return redraw;
        }
    }
}

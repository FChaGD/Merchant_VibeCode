namespace Game.Core
{
    /// <summary>
    /// 숙소 휴식 1회의 진행 상태(Docs/설계/105번 §3). 부수효과 없이 "지금 하루를 더 보낼 수 있는가"만 판정한다 -
    /// 날짜·비용 반영은 InnRestService가 이 판정을 통과한 하루마다 한다.
    /// </summary>
    public sealed class InnRestSession
    {
        public bool IsActive { get; private set; }
        public int PlannedDays { get; private set; }
        public int ElapsedDays { get; private set; }

        public bool CanAdvance => IsActive && ElapsedDays < PlannedDays;

        /// <summary>1일 이상만 시작한다. 진행 중이면 이전 휴식을 덮어쓴다(종료 누락 방어는 호출자가 경고).</summary>
        public bool Begin(int plannedDays)
        {
            if (plannedDays <= 0) return false;
            IsActive = true;
            PlannedDays = plannedDays;
            ElapsedDays = 0;
            return true;
        }

        public bool TryAdvance()
        {
            if (!CanAdvance) return false;
            ElapsedDays++;
            return true;
        }

        public void End() => IsActive = false;
    }
}

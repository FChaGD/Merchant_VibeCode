namespace Game.Core
{
    /// <summary>
    /// 게임 날짜(Docs/기획/84번 §3). 누적 일수 하나만 상태로 갖고 월·일은 계산한다 - 저장/불러오기가 생겨도 정수 하나만 저장하면 된다.
    /// 1개월 30일·1년 6개월은 기획 고정 규칙이라 상수다. 절대 연도는 없고 경과 해 수는 내부에서만 쓴다(표시하지 않음).
    /// </summary>
    public readonly struct GameDate
    {
        public const int DaysPerMonth = 30;
        public const int MonthsPerYear = 6;
        public const int DaysPerYear = DaysPerMonth * MonthsPerYear;

        public int TotalDays { get; }

        public GameDate(int totalDays)
        {
            TotalDays = totalDays < 0 ? 0 : totalDays;
        }

        public int ElapsedYears => TotalDays / DaysPerYear;
        public int Month => TotalDays % DaysPerYear / DaysPerMonth + 1;
        public int Day => TotalDays % DaysPerMonth + 1;

        public string ToDisplayString() => $"{Month}월 {Day}일";
    }
}

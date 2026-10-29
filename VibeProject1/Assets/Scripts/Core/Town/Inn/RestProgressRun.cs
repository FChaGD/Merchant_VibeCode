namespace Game.Core
{
    /// <summary>
    /// 휴식 하루치 대기 시간 측정(설계 109 §4.1). 하루 대기가 끝나면 경계에서 멈추고, 흐름 객체가 그날 이벤트·결제를 끝내 BeginNextDay를 부를 때까지
    /// 진행하지 않는다 - 한 프레임이 길어도(창 비활성 후 복귀) 여러 날을 넘기면 그 사이 이벤트 점검이 빠진다. 휴식 완료 판정은 성립 일수를 아는 흐름 객체가 한다.
    /// 게이지는 성립 일수 + 오늘 대기 비율 - 미성립 날(이벤트 강제 종료·결제 부족)의 대기는 휴식에 넣지 않는다.
    /// </summary>
    public sealed class RestProgressRun
    {
        private readonly float secondsPerDay;
        private float todaySeconds;
        private int settledDays;

        public RestProgressRun(int totalDays, float secondsPerDay)
        {
            TotalDays = totalDays < 0 ? 0 : totalDays;
            this.secondsPerDay = secondsPerDay > 0f ? secondsPerDay : 1f;
        }

        public int TotalDays { get; }
        public bool IsDayReady { get; private set; }
        public bool IsStopped { get; private set; }

        public float Progress => TotalDays == 0 ? 1f : System.Math.Min(1f, (settledDays + todaySeconds / secondsPerDay) / TotalDays);

        /// <summary>오늘 대기 시간을 더한다. 이번 호출로 그날 대기가 끝났으면 true(이후 BeginNextDay 전까지 무시).</summary>
        public bool Advance(float deltaSeconds)
        {
            if (IsStopped || IsDayReady || TotalDays == 0 || deltaSeconds <= 0f) return false;

            todaySeconds += deltaSeconds;
            if (todaySeconds < secondsPerDay) return false;
            todaySeconds = secondsPerDay;
            IsDayReady = true;
            return true;
        }

        /// <summary>경계 다음 날 대기를 시작한다. 성립 일수는 흐름 객체가 넘긴다(게이지 기준).</summary>
        public void BeginNextDay(int settledDays)
        {
            this.settledDays = settledDays < 0 ? 0 : settledDays;
            todaySeconds = 0f;
            IsDayReady = false;
        }

        public void Stop() => IsStopped = true;
    }
}

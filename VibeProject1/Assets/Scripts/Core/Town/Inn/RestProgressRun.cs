namespace Game.Core
{
    /// <summary>
    /// 휴식 진행 한 번의 상태(Docs/설계/103번 §4.2). 경과 시간을 누적해 지난 일수를 계산하므로 프레임이 끊겨 한 번에 여러 날을 넘어도
    /// "1일 경과"를 빠뜨리지 않는다 - 호출자는 반환된 일수만큼 경과 계약을 부른다. 중단·완료 뒤의 경과는 무시한다.
    /// </summary>
    public sealed class RestProgressRun
    {
        private readonly float secondsPerDay;
        private float elapsedSeconds;

        public RestProgressRun(int totalDays, float secondsPerDay)
        {
            TotalDays = totalDays < 0 ? 0 : totalDays;
            this.secondsPerDay = secondsPerDay > 0f ? secondsPerDay : 1f;
        }

        public int TotalDays { get; }
        public int ElapsedDays { get; private set; }
        public bool IsStopped { get; private set; }
        public bool IsFinished => ElapsedDays >= TotalDays;
        public float Progress => TotalDays == 0 ? 1f : System.Math.Min(1f, elapsedSeconds / (TotalDays * secondsPerDay));

        /// <summary>경과 시간을 더하고 이번에 새로 지난 일수를 돌려준다.</summary>
        public int Advance(float deltaSeconds)
        {
            if (IsStopped || IsFinished || deltaSeconds <= 0f) return 0;

            elapsedSeconds += deltaSeconds;
            var days = (int)(elapsedSeconds / secondsPerDay);
            if (days > TotalDays) days = TotalDays;
            var newDays = days - ElapsedDays;
            ElapsedDays = days;
            if (IsFinished) elapsedSeconds = TotalDays * secondsPerDay;
            return newDays;
        }

        public void Stop() => IsStopped = true;
    }
}

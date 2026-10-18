using System;

namespace Game.Core
{
    /// <summary>
    /// 구간 하나의 시간 계산(설계 89 §4.3). 총 시간 = 기본(거리·난이도) + 추가(파괴 마차 지연·이벤트 지연·단축). 둘을 나눠 두는 이유는 도로 난이도가
    /// 바뀌어 기본만 다시 계산할 때 이미 더해진 지연·단축을 잃지 않기 위해서다. 기본을 바꿀 때는 진행 비율을 유지한다(게이지가 튀지 않게).
    /// 단축은 지나온 시간 아래로 내려가지 않는다 - 남은 시간이 0이 되면 재개 직후 도착한다. 순수 C#이라 SessionStateTracker 없이 검증한다.
    /// </summary>
    public sealed class TripLegClock
    {
        private const float MinDurationSeconds = 0.01f;

        public float BaseSeconds { get; private set; }
        public float AddedSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float DurationSeconds => Math.Max(MinDurationSeconds, BaseSeconds + AddedSeconds);
        public float Progress => Math.Min(1f, Math.Max(0f, ElapsedSeconds / DurationSeconds));

        public TripLegClock(float initialSeconds) => Begin(initialSeconds);

        public void Begin(float baseSeconds)
        {
            BaseSeconds = Math.Max(MinDurationSeconds, baseSeconds);
            AddedSeconds = 0f;
            ElapsedSeconds = 0f;
        }

        public void Tick(float delta)
        {
            if (delta > 0f) ElapsedSeconds += delta;
        }

        public void Extend(float seconds)
        {
            if (seconds > 0f) AddedSeconds += seconds;
        }

        /// <summary>실제로 줄어든 초를 돌려준다(결과 줄 「이동 단축 n초」용).</summary>
        public float Shorten(float seconds)
        {
            if (seconds <= 0f) return 0f;
            var current = DurationSeconds;
            var target = Math.Max(Math.Max(ElapsedSeconds, MinDurationSeconds), current - seconds);
            var actual = Math.Max(0f, current - target);
            AddedSeconds -= actual;
            return actual;
        }

        public void Rebase(float baseSeconds)
        {
            var ratio = Progress;
            BaseSeconds = Math.Max(MinDurationSeconds, baseSeconds);
            ElapsedSeconds = ratio * DurationSeconds;
        }
    }
}

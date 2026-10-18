using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// GameManager 산하 컴포넌트(전역 DI 미등록 - GameManager가 ISessionState/ISessionPauseControl로
    /// 대신 등록한다). 구간 하나의 소요시간을 받아 경과 시간으로 진행도를 계산한다 - 소요시간 계산(거리·난이도)은
    /// 상행 계획(TripRoutePlanner)이 맡고, 여기는 시간만 센다(Docs/설계/76번 §6.1). 시간 계산(기본·추가·단축·기본 교체)은
    /// TripLegClock(설계 89 §4.3)이 맡는다.
    /// </summary>
    public class SessionStateTracker : MonoBehaviour, ISessionState, ITripClockAdjuster
    {
        private readonly TripLegClock clock = new(TripTravelSettings.LegacyDurationSeconds);
        private bool paused = true;
        private bool arrived;

        public float Progress { get; private set; }
        public float ElapsedSeconds => clock.ElapsedSeconds;
        public float DurationSeconds => clock.DurationSeconds;
        public float AddedSeconds => clock.AddedSeconds;

        public event Action<float> OnProgressChanged;
        public event Action OnArrived;

        public void Begin(float durationSeconds)
        {
            clock.Begin(durationSeconds);
            arrived = false;
            paused = false;
            Notify();
        }

        public void Pause()
        {
            paused = true;
        }

        public void Resume()
        {
            if (arrived)
            {
                return;
            }

            paused = false;
        }

        public void ExtendDuration(float seconds)
        {
            if (arrived || seconds <= 0f)
            {
                return;
            }

            clock.Extend(seconds);
            Notify();
        }

        public float ShortenDuration(float seconds)
        {
            if (arrived || seconds <= 0f)
            {
                return 0f;
            }

            var actual = clock.Shorten(seconds);
            Notify();
            return actual;
        }

        public void RebaseDuration(float baseSeconds)
        {
            if (arrived)
            {
                return;
            }

            clock.Rebase(baseSeconds);
            Notify();
        }

        private void Notify()
        {
            Progress = clock.Progress;
            OnProgressChanged?.Invoke(Progress);
        }

        private void Update()
        {
            if (paused || arrived)
            {
                return;
            }

            clock.Tick(Time.deltaTime);
            Notify();

            if (Progress >= 1f)
            {
                arrived = true;
                paused = true;
                OnArrived?.Invoke();
            }
        }
    }
}

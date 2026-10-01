using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// GameManager 산하 컴포넌트(전역 DI 미등록 - GameManager가 ISessionState/ISessionPauseControl로
    /// 대신 등록한다). 구간 하나의 소요시간을 받아 경과 시간으로 진행도를 계산한다 - 소요시간 계산(거리·난이도)은
    /// 상행 계획(TripRoutePlanner)이 맡고, 여기는 시간만 센다(Docs/설계/76번 §6.1).
    /// </summary>
    public class SessionStateTracker : MonoBehaviour, ISessionState
    {
        private float durationSeconds = TripTravelSettings.LegacyDurationSeconds;
        private float elapsed;
        private bool paused = true;
        private bool arrived;

        public float Progress { get; private set; }

        public event Action<float> OnProgressChanged;
        public event Action OnArrived;

        public void Begin(float durationSeconds)
        {
            this.durationSeconds = Mathf.Max(0.01f, durationSeconds);
            elapsed = 0f;
            Progress = 0f;
            arrived = false;
            paused = false;
            OnProgressChanged?.Invoke(Progress);
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

        private void Update()
        {
            if (paused || arrived)
            {
                return;
            }

            elapsed += Time.deltaTime;
            Progress = Mathf.Clamp01(elapsed / durationSeconds);
            OnProgressChanged?.Invoke(Progress);

            if (Progress >= 1f)
            {
                arrived = true;
                paused = true;
                OnArrived?.Invoke();
            }
        }
    }
}

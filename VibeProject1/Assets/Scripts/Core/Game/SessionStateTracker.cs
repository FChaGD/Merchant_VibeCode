using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// GameManager 산하 컴포넌트(전역 DI 미등록 - GameManager가 ISessionState/ISessionPauseControl로
    /// 대신 등록한다). 테스트 단계에서는 고정 소요시간을 시간 경과로 나눠 진행도를 계산한다 - 실제
    /// 거리+변수 기반 계산은 후속 설계 대상이며, 이 클래스 내부 구현만 교체하면 된다
    /// (Docs/설계/04_Field씬_아키텍처.md 3절).
    /// </summary>
    public class SessionStateTracker : MonoBehaviour, ISessionState
    {
        [SerializeField] private float testDurationSeconds = 30f;

        private float elapsed;
        private bool paused = true;
        private bool arrived;

        public float Progress { get; private set; }

        public event Action<float> OnProgressChanged;
        public event Action OnArrived;

        public void Begin()
        {
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
            Progress = Mathf.Clamp01(elapsed / testDurationSeconds);
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

using System;

namespace Game.Core
{
    /// <summary>
    /// 정지 원인 전이를 상행 시계·배치 활동에 적용한다(설계 87 §6.2). 원인이 하나라도 남으면 활동을 다시 전부 멈춘다 - 전투는 시작 시 배치 활동만
    /// 재개하므로(LiveBattleSimulationRule), 이벤트 전투가 끝나 "전투"만 풀렸을 때 그 재개를 되돌려야 이벤트 진행 중 배치가 흐르지 않는다.
    /// 순수 C#이라 EditMode에서 검증하고, MonoBehaviour 껍데기(TripHaltCoordinator)가 FieldManager 하위에서 들고 있다.
    /// </summary>
    public sealed class TripHaltController : ITripHaltControl
    {
        private readonly TripHaltState state = new();
        private readonly ISessionState session;
        private readonly IFieldFormationActivityRepository activities; // 선택적

        public TripHaltController(ISessionState session, IFieldFormationActivityRepository activities)
        {
            this.session = session;
            this.activities = activities;
        }

        public bool IsHalted => state.IsHalted;
        public bool IsHeld(TripHaltReason reason) => state.IsHeld(reason);
        public event Action HaltStarted;
        public event Action HaltEnded;

        public void Acquire(TripHaltReason reason)
        {
            switch (state.Acquire(reason))
            {
                case TripHaltTransition.Started:
                    session.Pause();
                    activities?.PauseAll();
                    HaltStarted?.Invoke();
                    break;
                case TripHaltTransition.StillHeld:
                    activities?.PauseAll();
                    break;
            }
        }

        public void Release(TripHaltReason reason)
        {
            switch (state.Release(reason))
            {
                case TripHaltTransition.Ended:
                    activities?.ResumeAll();
                    session.Resume();
                    HaltEnded?.Invoke();
                    break;
                case TripHaltTransition.StillHeld:
                    activities?.PauseAll();
                    break;
            }
        }

        public void ClearAll() => state.Clear();
    }
}

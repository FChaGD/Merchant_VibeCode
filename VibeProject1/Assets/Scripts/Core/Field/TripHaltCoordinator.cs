using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// FieldManager 하위 컴포넌트 - 정지 규칙(TripHaltController)을 Bootstrap 수명에 붙인다. 상행 시작·종료 신호에 원인을 비운다 - 궤주처럼 재개 없이
    /// 끝난 전투 정지가 다음 상행으로 넘어가지 않게.
    /// </summary>
    public class TripHaltCoordinator : MonoBehaviour, ITripHaltControl
    {
        private TripHaltController controller;
        private ITripMilestoneSource milestones;

        public bool IsHalted => controller != null && controller.IsHalted;
        public bool IsHeld(TripHaltReason reason) => controller != null && controller.IsHeld(reason);
        public event Action HaltStarted;
        public event Action HaltEnded;

        public void Bind(ISessionState session, IFieldFormationActivityRepository activities, ITripMilestoneSource milestones)
        {
            if (controller != null)
            {
                controller.HaltStarted -= RaiseStarted;
                controller.HaltEnded -= RaiseEnded;
            }
            controller = new TripHaltController(session, activities);
            controller.HaltStarted += RaiseStarted;
            controller.HaltEnded += RaiseEnded;

            if (this.milestones != null) this.milestones.Raised -= HandleMilestone;
            this.milestones = milestones;
            if (milestones != null) milestones.Raised += HandleMilestone;
        }

        public void Acquire(TripHaltReason reason) => controller?.Acquire(reason);
        public void Release(TripHaltReason reason) => controller?.Release(reason);
        public void ClearAll() => controller?.ClearAll();

        private void RaiseStarted() => HaltStarted?.Invoke();
        private void RaiseEnded() => HaltEnded?.Invoke();

        private void HandleMilestone(TripMilestone milestone)
        {
            if (milestone == TripMilestone.TripEnded || milestone == TripMilestone.TripStarted) ClearAll();
        }

        private void OnDestroy()
        {
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }
    }
}

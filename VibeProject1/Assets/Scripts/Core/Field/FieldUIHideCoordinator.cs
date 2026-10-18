using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// FieldManager 하위 컴포넌트 - 이벤트 중 Field UI 가림 원인(설계 89 §13)을 Bootstrap 수명에 둔다(전역 DI 대상 아님 - FieldManager가 계약 단위로
    /// 대신 등록). Field 화면은 씬 로드마다 바뀌지만 가림 여부는 이벤트 진행자(Bootstrap 상주)가 정하므로 여기 둔다. 상행 시작·종료 신호에 원인을 비운다 -
    /// 패배처럼 이벤트가 종료 처리 없이 끝나도 다음 상행으로 가림이 넘어가지 않게(정지 조율자와 같은 규칙).
    /// </summary>
    public class FieldUIHideCoordinator : MonoBehaviour, IFieldUIHideControl, IFieldUIHideReader
    {
        private readonly FieldUIHideState state = new();
        private ITripMilestoneSource milestones;

        public bool IsHidden => state.IsHidden;
        public event Action Changed;

        public void Bind(ITripMilestoneSource source)
        {
            if (milestones != null) milestones.Raised -= HandleMilestone;
            milestones = source;
            if (milestones != null) milestones.Raised += HandleMilestone;
        }

        public void Acquire(FieldUIHideReason reason)
        {
            if (state.Acquire(reason)) Changed?.Invoke();
        }

        public void Release(FieldUIHideReason reason)
        {
            if (state.Release(reason)) Changed?.Invoke();
        }

        private void HandleMilestone(TripMilestone milestone)
        {
            if ((milestone == TripMilestone.TripStarted || milestone == TripMilestone.TripEnded) && state.Clear()) Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }
    }
}

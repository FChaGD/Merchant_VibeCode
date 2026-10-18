using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>판정 신호 수신(설계 87 §6.3).</summary>
    public interface ITripCheckSignal
    {
        event Action BattleCheck;
        event Action DialogueCheck;
        event Action EventCheck;
        float SecondsUntil(TripCheckKind kind);
    }

    /// <summary>
    /// FieldManager 하위 컴포넌트 - 구간 시계의 이동 신호(실제 이동 초)에 얹어 시간을 센다. 이동 신호는 정지 중엔 오지 않으므로 전투·이벤트 중 판정이
    /// 별도 코드 없이 멈춘다. 진행 알림이 아니라 이동 신호인 이유: 진행 알림은 재계산·지연·단축에서도 와서 그 프레임 시간을 두 번 셌다(설계 93 §9-1).
    /// 도착 틱은 세지 않는다 - 예전 EncounterManager의 "도착과 인카운터 동시 처리" 버그 방지를 그대로 옮겼다.
    /// </summary>
    public class TripCheckClock : MonoBehaviour, ITripCheckSignal
    {
        private readonly TripCheckSchedule schedule = new();
        private readonly List<TripCheckKind> fired = new();
        private ISessionState session;
        private ITripMilestoneSource milestones;

        public event Action BattleCheck;
        public event Action DialogueCheck;
        public event Action EventCheck;

        public float SecondsUntil(TripCheckKind kind) => schedule.SecondsUntil(kind);

        public void Bind(ISessionState session, ITripMilestoneSource milestones)
        {
            Unbind();
            this.session = session;
            this.milestones = milestones;
            if (session != null) session.OnMoved += HandleMoved;
            if (milestones != null) milestones.Raised += HandleMilestone;
        }

        private void HandleMoved(float seconds)
        {
            // 이동 신호는 진행 알림보다 먼저 와서 진행도가 아직 갱신 전이다 - 도착 틱은 구간 시계의 경과·소요로 직접 판정한다.
            if (session.ElapsedSeconds >= session.DurationSeconds) return;
            schedule.Advance(seconds, fired);
            foreach (var kind in fired)
            {
                switch (kind)
                {
                    case TripCheckKind.Battle: BattleCheck?.Invoke(); break;
                    case TripCheckKind.Dialogue: DialogueCheck?.Invoke(); break;
                    default: EventCheck?.Invoke(); break;
                }
            }
        }

        private void HandleMilestone(TripMilestone milestone)
        {
            if (milestone == TripMilestone.TripStarted) schedule.Reset();
        }

        private void Unbind()
        {
            if (session != null) session.OnMoved -= HandleMoved;
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }

        private void OnDestroy() => Unbind();
    }
}

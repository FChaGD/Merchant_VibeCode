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
    /// FieldManager 하위 컴포넌트 - 상행 진행 이벤트(OnProgressChanged)에 얹어 시간을 센다. 진행 이벤트는 정지 중엔 오지 않으므로 전투·이벤트 중 판정이
    /// 별도 코드 없이 멈춘다. 도착 틱(진행도 ≥ 1)은 세지 않는다 - 예전 EncounterManager의 "도착과 인카운터 동시 처리" 버그 방지를 그대로 옮겼다.
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
            if (session != null) session.OnProgressChanged += HandleProgressChanged;
            if (milestones != null) milestones.Raised += HandleMilestone;
        }

        private void HandleProgressChanged(float progress)
        {
            if (progress >= 1f) return;
            schedule.Advance(Time.deltaTime, fired);
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
            if (session != null) session.OnProgressChanged -= HandleProgressChanged;
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }

        private void OnDestroy() => Unbind();
    }
}

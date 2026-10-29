using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>하루 흐름 단계(설계 109 §4.2). 점검·하루 처리는 즉시 끝나는 단계라 상태로 두지 않는다.</summary>
    public enum RestDayPhase { Waiting, FixedEvent, RandomEvent, Ended }

    /// <summary>
    /// 숙소 휴식 하루 순서 상태 기계(설계 109 §4.2, 기획 108 §3.3). 대기 → 확정 이벤트 → 랜덤 이벤트 → 하루 처리(결제·날짜·회복)를 이벤트가 없는 한
    /// 즉시 이어 간다. 이벤트 강제 종료·결제 부족일은 미성립 - 결제·날짜·회복 없이 끝낸다(그날 이벤트 효과는 이미 적용돼 남는다). 대본이 도중에 끊긴
    /// 이벤트(정상 완료 아님)도 다음 단계로 가지 않고 미성립·중단으로 끝낸다.
    /// 중단은 대기 중에만 받는다(기획 108 - 예외 경로 차단). Unity·화면을 모르고 서비스·실행기를 계약으로 받아 순서·사유를 EditMode로 검증한다.
    /// 실행기 끝 알림은 이벤트마다 토큰으로 묶는다 - 중단·다음 단계 이후 늦게 온 옛 알림이 흐름을 다시 움직이지 않게.
    /// </summary>
    public sealed class InnRestDayFlow
    {
        private readonly IInnRestService service;
        private readonly IRestEventRunner runner;
        private readonly List<RestEventRecord> records = new();
        private int eventToken;
        private bool started;

        public InnRestDayFlow(int plannedDays, IInnRestService service, IRestEventRunner runner)
        {
            PlannedDays = plannedDays < 0 ? 0 : plannedDays;
            this.service = service;
            this.runner = runner;
        }

        public int PlannedDays { get; }
        public RestDayPhase Phase { get; private set; } = RestDayPhase.Waiting;
        public bool CanStop => started && Phase == RestDayPhase.Waiting;
        /// <summary>확정·랜덤 이벤트가 진행 중 - 진행 화면은 이 동안 숨는다(이벤트 화면을 가리지 않게).</summary>
        public bool IsEventRunning => Phase == RestDayPhase.FixedEvent || Phase == RestDayPhase.RandomEvent;
        public int SettledDays { get; private set; }
        public int PaidTotal { get; private set; }
        public RestEndReason? EndReason { get; private set; }
        public IReadOnlyList<RestEventRecord> Records => records;
        public event Action Changed;

        public void Start()
        {
            if (started) return;
            started = true;
            service.BeginRest(PlannedDays);
            runner?.BeginRestSpan();
            if (PlannedDays == 0) End(RestEndReason.Completed);
            else Changed?.Invoke();
        }

        /// <summary>그날 대기가 끝났다 - 확정 → 랜덤 → 하루 처리로 이벤트가 없는 한 즉시 진행한다.</summary>
        public void OnWaitFinished()
        {
            if (!started || Phase != RestDayPhase.Waiting) return;
            RunFixed();
        }

        /// <summary>중단 버튼. 대기 중에만 받는다 - 이벤트·하루 처리 중엔 false.</summary>
        public bool Stop()
        {
            if (!CanStop) return false;
            End(RestEndReason.Stopped);
            return true;
        }

        /// <summary>비정상 종료(진행 화면 닫힘·Hub 언로드). 실행 중 이벤트는 실행기에 중단을 요청한다 - 그날은 미성립.</summary>
        public void Abort()
        {
            if (!started || Phase == RestDayPhase.Ended) return;
            if (Phase == RestDayPhase.FixedEvent || Phase == RestDayPhase.RandomEvent) runner?.Abort();
            End(RestEndReason.Stopped);
        }

        private void RunFixed()
        {
            Phase = RestDayPhase.FixedEvent;
            var token = ++eventToken;
            if (runner != null && runner.TryStartFixed(outcome => OnFixedDone(token, outcome)))
            {
                if (token == eventToken && Phase == RestDayPhase.FixedEvent) Changed?.Invoke();
                return;
            }
            if (token == eventToken) RunRandom();
        }

        private void OnFixedDone(int token, RestEventOutcome outcome)
        {
            if (token != eventToken || Phase != RestDayPhase.FixedEvent || EndIfIncomplete(outcome)) return;
            Record(outcome);
            if (outcome.ForcedEnd) End(RestEndReason.ForcedByEvent);
            else RunRandom();
        }

        private void RunRandom()
        {
            Phase = RestDayPhase.RandomEvent;
            var token = ++eventToken;
            if (runner != null && runner.TryStartRandom(outcome => OnRandomDone(token, outcome)))
            {
                if (token == eventToken && Phase == RestDayPhase.RandomEvent) Changed?.Invoke();
                return;
            }
            if (token == eventToken) Settle();
        }

        private void OnRandomDone(int token, RestEventOutcome outcome)
        {
            if (token != eventToken || Phase != RestDayPhase.RandomEvent || EndIfIncomplete(outcome)) return;
            Record(outcome);
            if (outcome.ForcedEnd) End(RestEndReason.ForcedByEvent);
            else Settle();
        }

        // 대본이 도중에 끊긴 이벤트는 다음 단계로 넘기지 않는다 - 기록 없이 그날 미성립, 중단으로 끝낸다(진행 화면 닫힘과 같은 처리, 사용자 결정 2026-10-29).
        private bool EndIfIncomplete(RestEventOutcome outcome)
        {
            if (outcome.Completed) return false;
            End(RestEndReason.Stopped);
            return true;
        }

        private void Settle()
        {
            ++eventToken;   // 하루 처리 이후로는 이번 날 이벤트 알림을 받지 않는다
            // 휴식 중·예정 일수 안일 때만 하루 처리를 부른다. 흐름의 일수 판정과 서비스 상태가 어긋났으면(밖에서 휴식을 끝낸 경우) 그날 미성립으로 끝낸다.
            if (!service.CanSettleDay)
            {
                End(RestEndReason.Stopped);
                return;
            }
            if (service.SettleDay() == RestDaySettlement.InsufficientGold)
            {
                End(RestEndReason.InsufficientGold);
                return;
            }
            SettledDays++;
            PaidTotal += service.CostPerDay;
            if (SettledDays >= PlannedDays)
            {
                End(RestEndReason.Completed);
                return;
            }
            Phase = RestDayPhase.Waiting;
            Changed?.Invoke();
        }

        private void Record(RestEventOutcome outcome) => records.Add(new RestEventRecord(SettledDays + 1, outcome.Title, outcome.ResultSummary));

        private void End(RestEndReason reason)
        {
            ++eventToken;
            Phase = RestDayPhase.Ended;
            EndReason = reason;
            service.EndRest(SettledDays);
            Changed?.Invoke();
        }
    }
}

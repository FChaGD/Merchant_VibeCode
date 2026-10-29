using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>휴식 종료 사유(설계 109 §4.4).</summary>
    public enum RestEndReason { Completed, Stopped, ForcedByEvent, InsufficientGold }

    /// <summary>
    /// 휴식 이벤트 하나의 끝(설계 109 §5.3). 결과 요약 = 효과 결과 줄을 ", "로 이은 것(없으면 빈 문자열).
    /// Completed = 대본이 끝까지 진행됐다. 거짓(대본 도중 실행기 중단)이면 흐름은 다음 단계로 가지 않고 휴식을 중단으로 끝낸다(사용자 결정 2026-10-29).
    /// </summary>
    public readonly struct RestEventOutcome
    {
        public string EventId { get; }
        public string Title { get; }
        public string ResultSummary { get; }
        public bool ForcedEnd { get; }
        public bool Completed { get; }

        public RestEventOutcome(string eventId, string title, string resultSummary, bool forcedEnd, bool completed = true)
        {
            EventId = eventId;
            Title = title ?? string.Empty;
            ResultSummary = resultSummary ?? string.Empty;
            ForcedEnd = forcedEnd;
            Completed = completed;
        }
    }

    /// <summary>진행 화면 이벤트 기록 한 줄(설계 109 §6.3). 일차 = 성립 일수 + 1(그날).</summary>
    public readonly struct RestEventRecord
    {
        public int Day { get; }
        public string Title { get; }
        public string ResultSummary { get; }

        public RestEventRecord(int day, string title, string resultSummary)
        {
            Day = day;
            Title = title ?? string.Empty;
            ResultSummary = resultSummary ?? string.Empty;
        }
    }

    /// <summary>
    /// 휴식 이벤트 실행기(설계 109 §5.3) - 하루 흐름(InnRestDayFlow)이 Unity·화면을 모른 채 이벤트를 요청한다. 시작 실패(후보 없음·화면 없음·대본 없음)는
    /// false라 흐름은 "발생 없음"으로 넘어간다. 시작에 성공하면 끝날 때 onDone이 한 번 온다(Abort 후에는 오지 않는다).
    /// </summary>
    public interface IRestEventRunner
    {
        bool IsRunning { get; }
        /// <summary>휴식 1회 시작 - "이번 회차" 발생 기록을 비운다(설계 109 §3.3).</summary>
        void BeginRestSpan();
        bool TryStartFixed(Action<RestEventOutcome> onDone);
        bool TryStartRandom(Action<RestEventOutcome> onDone);
        void Abort();
    }

    /// <summary>Hub 로드마다 이번 씬의 이벤트 화면을 휴식 진행자에 연결한다 - HubUIWiring만 쓴다(설계 109 §6.2).</summary>
    public interface IRestEventViewBinder
    {
        void BindScreens(IEventEncounterScreens screens);
    }

    /// <summary>디버그 전용(설계 109 §7) - 다음 확정 점검에서 조건·횟수를 무시하고 그 휴식 이벤트를 시작한다(1회).</summary>
    public interface IRestEventDebug
    {
        string ForcedNextId { get; }
        IReadOnlyList<EventEntry> RestEvents { get; }
        /// <summary>휴식 이벤트 Id가 아니면 false. null이면 지정 해제.</summary>
        bool ForceNext(string eventId);
    }
}

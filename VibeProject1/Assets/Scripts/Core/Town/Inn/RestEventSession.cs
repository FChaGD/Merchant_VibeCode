using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>휴식 이벤트 세션이 쓰는 값·서비스 묶음. 테스트가 필요한 것만 채우도록 기본값을 둔다.</summary>
    public sealed class RestEventSessionSources
    {
        public IEncounterContentReader Content;
        public IEncounterHistoryReader History;
        public IEncounterHistoryWriter HistoryWriter;
        public IDialogueScriptRunner Runner;
        public IEventEffectRouter Router;
        /// <summary>공용 효과 서비스(골드·무역품·명단). 휴식 종료 요청은 세션이 더한다.</summary>
        public EventEffectServices Services = new();
        public EncounterConditionRegistry Conditions = EncounterConditionRegistry.CreateDefault();
        public Func<EncounterConditionContext> Context = () => default;
        public Func<float> Random01 = () => 0.99f;
        public Func<string, string> NameOf = _ => null;
        public float RandomChancePerDay = 0.35f;
        public Action<string> Warn = _ => { };
    }

    /// <summary>
    /// 휴식 이벤트 실행 규칙(설계 109 §5.3). 상행과 같은 이벤트 진행자를 휴식용으로 하나 두고(상행 정지·계기·전투·Field 가림 없음) 후보 고르기·강제 종료 요청·
    /// 디버그 강제 지정만 더한다. Unity를 모르는 순수 C# - Bootstrap 껍데기(InnRestEventCoordinator)가 의존성·프레임을 넘긴다.
    /// 강제 종료 요청은 이벤트 끝 알림에서 읽고 바로 지운다 - 다음 이벤트로 새지 않게. 휴식 이벤트는 `quest` 조건을 쓸 수 없어 시작 신호를 중계하지 않는다(§5.3).
    /// 디버그 강제 지정은 휴식 시작에 지우지 않는다 - 휴식 전에 골라 두고 시작하는 사용 순서를 허용한다(한 번 쓰면 지워짐).
    /// </summary>
    public sealed class RestEventSession : IRestEventRunner, IRestEndRequest, IRestEventViewBinder, IRestEventDebug
    {
        private readonly RestEventSessionSources sources;
        private readonly EventEncounterDirector director;
        private readonly IReadOnlyList<EventEntry> restEvents;
        private Action<RestEventOutcome> pending;
        private bool endRequested;

        public RestEventSession(RestEventSessionSources sources)
        {
            this.sources = sources;
            restEvents = sources.Content.EventsAt(EventPlace.Rest);
            var services = sources.Services ?? new EventEffectServices();
            services.RestEnd = this;
            director = new EventEncounterDirector(new EventEncounterSources
            {
                Content = sources.Content,
                History = sources.History,
                HistoryWriter = sources.HistoryWriter,
                Runner = sources.Runner,
                Conditions = sources.Conditions,
                Context = sources.Context,
                Random01 = sources.Random01,
                NameOf = sources.NameOf,
                Router = sources.Router,
                Services = services,
                Candidates = restEvents,
                Warn = sources.Warn,
            });
            director.Finished += HandleFinished;
        }

        public bool IsRunning => pending != null || director.IsRunning;
        public string CurrentEventId => director.CurrentEventId;
        public string ForcedNextId { get; private set; }
        public IReadOnlyList<EventEntry> RestEvents => restEvents;

        public void BindScreens(IEventEncounterScreens screens) => director.BindScreens(screens);

        public void Tick(float deltaSeconds) => director.Tick(deltaSeconds);

        public void BeginRestSpan()
        {
            sources.HistoryWriter?.ResetTrip();   // "이번 회차" = 이번 휴식(설계 109 §3.3)
            endRequested = false;
        }

        public bool TryStartFixed(Action<RestEventOutcome> onDone)
        {
            var entry = TakeForced() ?? RestEventSelector.PickFixed(restEvents, sources.History, sources.Conditions, sources.Context());
            return TryRun(entry, onDone);
        }

        public bool TryStartRandom(Action<RestEventOutcome> onDone)
            => TryRun(RestEventSelector.PickRandom(restEvents, sources.History, sources.Conditions, sources.Context(), sources.Random01, sources.RandomChancePerDay), onDone);

        public void Abort()
        {
            pending = null;
            endRequested = false;
            director.Abort();
        }

        // IRestEndRequest - `<<rest_end>>`.
        public void RequestEnd() => endRequested = true;

        public bool ForceNext(string eventId)
        {
            if (eventId == null)
            {
                ForcedNextId = null;
                return true;
            }
            if (!IsRestEvent(eventId, out _)) return false;
            ForcedNextId = eventId;
            return true;
        }

        private EventEntry TakeForced()
        {
            var id = ForcedNextId;
            if (id == null) return null;
            ForcedNextId = null;
            return IsRestEvent(id, out var entry) ? entry : null;
        }

        private bool IsRestEvent(string id, out EventEntry entry)
        {
            entry = null;
            foreach (var e in restEvents)
            {
                if (e.Id != id) continue;
                entry = e;
                return true;
            }
            return false;
        }

        private bool TryRun(EventEntry entry, Action<RestEventOutcome> onDone)
        {
            if (entry == null || pending != null) return false;
            endRequested = false;
            pending = onDone;
            if (director.TryBegin(entry)) return true;
            pending = null;
            return false;
        }

        private void HandleFinished(EventFinish finish)
        {
            var done = pending;
            pending = null;
            var forced = endRequested;
            endRequested = false;
            sources.Content.TryGetEvent(finish.EventId, out var entry);
            // 진행자는 대본이 끝까지 간 경우만 기록한다 - 그 여부를 정상 완료로 넘긴다.
            done?.Invoke(new RestEventOutcome(finish.EventId, entry?.Title, string.Join(", ", finish.ResultLines), forced, finish.Recorded));
        }
    }
}

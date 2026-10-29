using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Core
{
    /// <summary>
    /// FieldManager 하위 컴포넌트 - 이벤트 진행 규칙(EventEncounterDirector)을 Bootstrap 수명에 붙인다(설계 89 §3). 의존성 조회·상행 효과 서비스 조립·신호 구독·
    /// 프레임 진행만 맡는다. 화면은 Field 로드마다 BindScreens로 바뀌고, 씬이 언로드되면 다음 프레임에 연결을 끊는다. 대사 실행기·콘텐츠·이력 중 하나라도
    /// 없으면(Bootstrap 미재빌드) 이벤트 없이 상행만 진행한다. 효과 서비스는 전부 선택적 - 없는 효과 명령만 경고 후 건너뛴다.
    /// Yarn 명령 등록은 효과 명령 라우터(EventEffectCommandHub)가 한 번만 한다 - 진행자가 이벤트 시작·끝에 라우터 맥락을 넣고 뺀다(설계 109 §5.1).
    /// </summary>
    public class EventEncounterCoordinator : MonoBehaviour, IEventEncounterViewBinder, IEventEncounterDebug, IEventBattleOutcomeSink
    {
        private static readonly IReadOnlyList<EventEntry> NoEvents = new EventEntry[0];

        private EventEncounterDirector director;
        private ITripCheckSignal checks;
        private ITripMilestoneSource milestones;
        private IUIVisibilitySignal visibility;
        private IEventEncounterScreens boundScreens;

        public bool IsRunning => director != null && director.IsRunning;
        public string CurrentEventId => director?.CurrentEventId;
        public string PendingEventId => director?.PendingEventId;
        public IReadOnlyList<EventEntry> QueuedEvents => director != null ? director.QueuedEvents : NoEvents;
        // 진행자는 Configure마다 새로 만들어지므로 구독자(Bootstrap 상주 퀘스트)가 끊기지 않게 여기서 중계한다.
        public event Action<string> EventStarted;

        public void Configure(IDependencyResolver registrar, ITripCheckSignal checks, ITripMilestoneSource milestones, ITripHaltControl halt, ITripMilestoneSink milestoneSink)
        {
            Unsubscribe();
            director = null;

            registrar.TryResolve(out IEncounterContentReader content);
            registrar.TryResolve(out IEncounterHistoryReader history);
            registrar.TryResolve(out IEncounterHistoryWriter historyWriter);
            registrar.TryResolve(out IDialogueScriptRunner runner);
            if (content == null || history == null || historyWriter == null || runner == null)
            {
                Debug.LogWarning($"{nameof(EventEncounterCoordinator)}: 콘텐츠·이력·대사 실행기 중 없는 것이 있어 이벤트가 발생하지 않는다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            registrar.TryResolve(out ICharacterCatalogReader catalog);
            registrar.TryResolve(out IEventBattleRequester battle);
            registrar.TryResolve(out IUIVisibilitySignal uiManager);
            registrar.TryResolve(out IFieldUIHideControl uiHide); // 이벤트 중 Field UI 가림(설계 89 §13) - 없으면 숨기지 않는다
            if (!registrar.TryResolve(out IEventEffectRouter router))
            {
                Debug.LogWarning($"{nameof(EventEncounterCoordinator)}: 효과 명령 라우터가 없어 이벤트 효과가 적용되지 않는다(Tools > Game > Build Bootstrap Scene).");
            }
            var context = new EncounterContextSource(registrar);

            this.checks = checks;
            this.milestones = milestones;
            visibility = uiManager;

            director = new EventEncounterDirector(new EventEncounterSources
            {
                Content = content,
                History = history,
                HistoryWriter = historyWriter,
                Runner = runner,
                Context = context.Build,
                Random01 = () => Random.value,
                NameOf = id => NameOf(id, catalog, content),
                Halt = halt,
                Milestones = milestoneSink,
                Battle = battle,
                UIHide = uiHide,
                Router = router,
                Services = BuildTripServices(registrar),
                Candidates = content.EventsAt(EventPlace.Trip),
                Warn = message => Debug.LogWarning(message),
            });

            director.EventStarted += RelayEventStarted;
            if (checks != null) checks.EventCheck += director.OnEventCheck;
            if (milestones != null) milestones.Raised += director.OnMilestone;
            if (visibility != null)
            {
                visibility.ModalPopupOpenChanged += director.SetModalOpen;
                director.SetModalOpen(visibility.IsModalPopupOpen);
            }
            director.BindScreens(boundScreens);
        }

        public void BindScreens(IEventEncounterScreens screens)
        {
            boundScreens = screens;
            director?.BindScreens(screens);
        }

        public bool DebugStart(string eventId) => director != null && director.DebugStart(eventId);

        public void OnEventBattleFinished(EventBattleOutcome outcome) => director?.OnEventBattleFinished(outcome);

        private void Update()
        {
            if (director == null) return;
            if (boundScreens != null && !boundScreens.IsAvailable)
            {
                BindScreens(null);
                return;
            }
            director.Tick(Time.deltaTime);
        }

        // 공용 묶음(골드·무역품·명단)에 상행 전용(상행 시계·도로 난이도)을 더한다(설계 109 §5.1).
        private EventEffectServices BuildTripServices(IDependencyResolver registrar)
        {
            registrar.TryResolve(out ISessionState session);
            registrar.TryResolve(out ITripClockAdjuster clock);
            registrar.TryResolve(out ICurrentLegDifficulty leg);
            registrar.TryResolve(out ICityPairDifficultyEditor pairs);
            registrar.TryResolve(out IDateReader date);

            var services = EventEffectCommandHub.CreateBaseServices(registrar);
            services.Session = session;
            services.Clock = clock;
            services.Road = leg != null ? new EventRoadDifficultyEffects(leg, pairs, date, () => director?.CurrentEventId) : null;
            return services;
        }

        private static string NameOf(string id, ICharacterCatalogReader catalog, IEncounterContentReader content)
        {
            if (catalog != null && catalog.TryGet(id, out var profile)) return profile.Name;
            return content.TryGetPerson(id, out var person) ? person.Name : null;
        }

        private void RelayEventStarted(string eventId) => EventStarted?.Invoke(eventId);

        private void Unsubscribe()
        {
            if (director == null) return;
            director.EventStarted -= RelayEventStarted;
            if (checks != null) checks.EventCheck -= director.OnEventCheck;
            if (milestones != null) milestones.Raised -= director.OnMilestone;
            if (visibility != null) visibility.ModalPopupOpenChanged -= director.SetModalOpen;
        }

        private void OnDestroy() => Unsubscribe();
    }
}

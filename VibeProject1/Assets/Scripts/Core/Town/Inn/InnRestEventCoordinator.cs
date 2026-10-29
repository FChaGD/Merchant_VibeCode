using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Core
{
    /// <summary>
    /// 휴식 이벤트 진행 규칙(RestEventSession)을 Bootstrap 수명에 붙인다(설계 109 §5.3) - 의존성 조회·프레임 진행·화면 연결 해제만 맡는다. 화면은 Hub 로드마다
    /// BindScreens로 바뀌고, 씬이 언로드되면 다음 프레임에 연결을 끊는다(Field 껍데기와 같은 방식). 콘텐츠·이력·대사 실행기·라우터 중 하나라도 없으면
    /// 시작이 항상 실패해 휴식은 이벤트 없이 진행한다.
    /// </summary>
    public class InnRestEventCoordinator : MonoBehaviour, IManagedComponent, IRestEventRunner, IRestEventViewBinder, IRestEventDebug
    {
        private static readonly IReadOnlyList<EventEntry> NoEvents = new EventEntry[0];

        [SerializeField] private float randomChancePerDay = 0.35f;

        private RestEventSession session;
        private IEventEncounterScreens boundScreens;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IRestEventRunner>(this);
            registrar.Register<IRestEventViewBinder>(this);
            registrar.Register<IRestEventDebug>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            session = null;
            registrar.TryResolve(out IEncounterContentReader content);
            registrar.TryResolve(out IEncounterHistoryReader history);
            registrar.TryResolve(out IEncounterHistoryWriter historyWriter);
            registrar.TryResolve(out IDialogueScriptRunner runner);
            registrar.TryResolve(out IEventEffectRouter router);
            if (content == null || history == null || historyWriter == null || runner == null || router == null)
            {
                Debug.LogWarning($"{nameof(InnRestEventCoordinator)}: 콘텐츠·이력·대사 실행기·효과 명령 라우터 중 없는 것이 있어 휴식 이벤트가 발생하지 않는다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            registrar.TryResolve(out ICharacterCatalogReader catalog);
            var context = new RestEncounterContextSource(registrar);
            session = new RestEventSession(new RestEventSessionSources
            {
                Content = content,
                History = history,
                HistoryWriter = historyWriter,
                Runner = runner,
                Router = router,
                Services = EventEffectCommandHub.CreateBaseServices(registrar),
                Context = context.Build,
                Random01 = () => Random.value,
                NameOf = id => NameOf(id, catalog, content),
                RandomChancePerDay = randomChancePerDay,
                Warn = message => Debug.LogWarning(message),
            });
            session.BindScreens(boundScreens);
        }

        public bool IsRunning => session != null && session.IsRunning;
        public string ForcedNextId => session?.ForcedNextId;
        public IReadOnlyList<EventEntry> RestEvents => session != null ? session.RestEvents : NoEvents;

        public void BeginRestSpan() => session?.BeginRestSpan();
        public bool TryStartFixed(Action<RestEventOutcome> onDone) => session != null && session.TryStartFixed(onDone);
        public bool TryStartRandom(Action<RestEventOutcome> onDone) => session != null && session.TryStartRandom(onDone);
        public void Abort() => session?.Abort();
        public bool ForceNext(string eventId) => session != null && session.ForceNext(eventId);

        public void BindScreens(IEventEncounterScreens screens)
        {
            boundScreens = screens;
            session?.BindScreens(screens);
        }

        private void Update()
        {
            if (session == null) return;
            if (boundScreens != null && !boundScreens.IsAvailable)
            {
                // Hub 언로드 - 진행 중 이벤트는 휴식 진행 화면 정리(흐름 Abort)가 먼저 끝낸다. 남았으면 여기서 끝낸다.
                if (session.IsRunning) session.Abort();
                BindScreens(null);
                return;
            }
            session.Tick(Time.unscaledDeltaTime);
        }

        private static string NameOf(string id, ICharacterCatalogReader catalog, IEncounterContentReader content)
        {
            if (catalog != null && catalog.TryGet(id, out var profile)) return profile.Name;
            return content.TryGetPerson(id, out var person) ? person.Name : null;
        }
    }
}

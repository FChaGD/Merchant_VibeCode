using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Core
{
    /// <summary>
    /// FieldManager 하위 컴포넌트 - 이벤트 진행 규칙(EventEncounterDirector)을 Bootstrap 수명에 붙인다(설계 89 §3). 의존성 조회·효과 명령 등록·신호 구독·
    /// 프레임 진행만 맡는다. 화면은 Field 로드마다 BindScreens로 바뀌고, 씬이 언로드되면 다음 프레임에 연결을 끊는다. 대사 실행기·콘텐츠·이력 중 하나라도
    /// 없으면(Bootstrap 미재빌드) 이벤트 없이 상행만 진행한다. 효과 서비스는 전부 선택적 - 없는 효과 명령만 경고 후 건너뛴다.
    /// Yarn 명령은 실행기 하나에 한 번만 등록한다(같은 이름 재등록 오류 방지) - 진행자는 Configure마다 새로 만들므로 명령은 중계(HostRelay)를 거쳐 항상 현재
    /// 진행자를 본다.
    /// </summary>
    public class EventEncounterCoordinator : MonoBehaviour, IEventEncounterViewBinder, IEventEncounterDebug, IEventBattleOutcomeSink
    {
        private static readonly IReadOnlyList<EventEntry> NoEvents = new EventEntry[0];

        private EventEncounterDirector director;
        private ITripCheckSignal checks;
        private ITripMilestoneSource milestones;
        private IUIVisibilitySignal visibility;
        private IEventEncounterScreens boundScreens;
        private IDialogueCommandRegistry registeredOn;

        public bool IsRunning => director != null && director.IsRunning;
        public string CurrentEventId => director?.CurrentEventId;
        public string PendingEventId => director?.PendingEventId;
        public IReadOnlyList<EventEntry> QueuedEvents => director != null ? director.QueuedEvents : NoEvents;

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
                Warn = message => Debug.LogWarning(message),
            });

            if (registrar.TryResolve(out IDialogueCommandRegistry commands) && commands != registeredOn)
            {
                new EventEffectCommands(BuildServices(registrar, catalog), new HostRelay(this)).Register(commands);
                registeredOn = commands;
            }

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

        // 서비스 대상(저장소들)은 Bootstrap 상주라 Configure를 다시 해도 같은 인스턴스다 - 첫 등록 때 만든 묶음을 명령이 계속 쓴다.
        private EventEffectServices BuildServices(IDependencyResolver registrar, ICharacterCatalogReader catalog)
        {
            registrar.TryResolve(out IGoldEventAdjuster gold);
            registrar.TryResolve(out ITradeGoodsInventoryRepository inventory);
            registrar.TryResolve(out ITradeGoodsCargoSettlement settlement);
            registrar.TryResolve(out IHiredCharacterRoster roster);
            registrar.TryResolve(out IHiredCharacterRemover remover);
            registrar.TryResolve(out IFormationRepository formation);
            registrar.TryResolve(out IFieldFormationActivityRepository activities);
            registrar.TryResolve(out ISessionState session);
            registrar.TryResolve(out ITripClockAdjuster clock);
            registrar.TryResolve(out ICurrentLegDifficulty leg);
            registrar.TryResolve(out ICityPairDifficultyEditor pairs);
            registrar.TryResolve(out IDateReader date);

            return new EventEffectServices
            {
                Gold = gold,
                Goods = inventory != null && settlement != null
                    ? new EventCargoGoods(inventory, settlement, id => gold != null && gold.IsGoldItem(id), n => Random.Range(0, n))
                    : null,
                Roster = roster != null
                    ? new EventRosterEffects(catalog, roster, remover, formation, id => activities != null && activities.IsUnitBusy(id), id => activities?.Cancel(id))
                    : null,
                Session = session,
                Clock = clock,
                Road = leg != null ? new EventRoadDifficultyEffects(leg, pairs, date, () => director?.CurrentEventId) : null,
                Warn = message => Debug.LogWarning(message),
            };
        }

        private static string NameOf(string id, ICharacterCatalogReader catalog, IEncounterContentReader content)
        {
            if (catalog != null && catalog.TryGet(id, out var profile)) return profile.Name;
            return content.TryGetPerson(id, out var person) ? person.Name : null;
        }

        private void Unsubscribe()
        {
            if (director == null) return;
            if (checks != null) checks.EventCheck -= director.OnEventCheck;
            if (milestones != null) milestones.Raised -= director.OnMilestone;
            if (visibility != null) visibility.ModalPopupOpenChanged -= director.SetModalOpen;
        }

        private void OnDestroy() => Unsubscribe();

        // 명령은 실행기에 한 번만 등록하지만 진행자는 Configure마다 새로 만든다 - 명령이 항상 현재 진행자를 보게 한 단계 거친다.
        private sealed class HostRelay : IEventEffectHost
        {
            private readonly EventEncounterCoordinator owner;

            public HostRelay(EventEncounterCoordinator owner) => this.owner = owner;

            private IEventEffectHost Host => owner.director;

            public bool IsRunning => Host != null && Host.IsRunning;

            public void PresentResult(string text, Action onDone)
            {
                if (Host != null) Host.PresentResult(text, onDone);
                else onDone?.Invoke();
            }

            public bool TryShowCargoRecovery(Action onDone) => Host != null && Host.TryShowCargoRecovery(onDone);

            public void RequestBattle(Action onVictory) => Host?.RequestBattle(onVictory);
        }
    }
}

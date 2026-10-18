using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// FieldManager 하위 컴포넌트 - 대화 진행 규칙(TripDialogueDirector)을 Bootstrap 수명에 붙인다(설계 88 §2). 의존성 조회·신호 구독·프레임 진행만 맡는다.
    /// 화면은 Field 로드마다 BindViews로 바뀐다. 씬이 언로드돼 화면이 파괴되면 다음 프레임에 연결을 끊는다 - 파괴된 화면에 그리지 않게.
    /// 대사 실행기·콘텐츠·이력 중 하나라도 없으면(Bootstrap 미재빌드) 대화 없이 상행만 진행한다.
    /// </summary>
    public class TripDialogueCoordinator : MonoBehaviour, ITripDialogueViewBinder, ITripDialogueDebug
    {
        private static readonly IReadOnlyList<DialogueTrigger> NoTriggers = new DialogueTrigger[0];

        private TripDialogueDirector director;
        private ITripCheckSignal checks;
        private ITripMilestoneSource milestones;
        private ITripHaltReader halt;
        private IUIVisibilitySignal visibility;
        private ITripDialogueView boundView;
        private IManualTalkView boundManual;

        private ISessionState session;
        private IEncounterHistoryReader history;

        public bool IsRunning => director != null && director.IsRunning;
        public string CurrentDialogueId => director?.CurrentDialogueId;
        public IReadOnlyList<DialogueTrigger> PendingTriggers => director != null ? director.PendingTriggers : NoTriggers;
        public float AfterBattleSecondsRemaining => director != null ? director.AfterBattleSecondsRemaining : -1f;

        public void Configure(IDependencyResolver registrar, ITripCheckSignal checks, ITripMilestoneSource milestones, ITripHaltReader halt)
        {
            Unsubscribe();
            director = null;

            registrar.TryResolve(out IEncounterContentReader content);
            registrar.TryResolve(out history);
            registrar.TryResolve(out IEncounterHistoryWriter historyWriter);
            registrar.TryResolve(out IDialogueScriptRunner runner);
            if (content == null || history == null || historyWriter == null || runner == null)
            {
                Debug.LogWarning($"{nameof(TripDialogueCoordinator)}: 콘텐츠·이력·대사 실행기 중 없는 것이 있어 상행 대화가 발생하지 않는다(Tools > Game > Build Bootstrap Scene).");
                return;
            }

            registrar.TryResolve(out IHiredCharacterRoster hired);
            registrar.TryResolve(out ICaravanRosterProvider roster);
            registrar.TryResolve(out ICharacterCatalogReader catalog);
            registrar.TryResolve(out IUIManager uiManager);
            // 판정 문맥은 이벤트 진행자(89)와 같은 공용 구성을 쓴다 - 같은 조건이 두 쪽에서 다르게 판정되지 않게.
            var context = new EncounterContextSource(registrar);
            session = registrar.Resolve<ISessionState>();

            this.checks = checks;
            this.milestones = milestones;
            this.halt = halt;
            visibility = uiManager;

            director = new TripDialogueDirector(new TripDialogueSources
            {
                Content = content,
                History = history,
                HistoryWriter = historyWriter,
                Runner = runner,
                Context = context.Build,
                IsHired = id => hired != null && hired.IsHired(id),
                Crew = () => CrewOf(roster),
                NameOf = id => NameOf(id, catalog, content),
                Random01 = () => Random.value,
                IsHalted = () => halt != null && halt.IsHalted,
            });

            if (checks != null) checks.DialogueCheck += director.OnDialogueCheck;
            if (session != null) session.OnProgressChanged += HandleProgressChanged;
            if (milestones != null) milestones.Raised += director.OnMilestone;
            if (halt != null)
            {
                halt.HaltStarted += director.OnHaltStarted;
                halt.HaltEnded += director.OnHaltEnded;
            }
            if (visibility != null)
            {
                visibility.ModalPopupOpenChanged += director.SetModalOpen;
                director.SetModalOpen(visibility.IsModalPopupOpen);
            }
            director.BindViews(boundView, boundManual);
        }

        public void BindViews(ITripDialogueView dialogueView, IManualTalkView manualTalkView)
        {
            boundView = dialogueView;
            boundManual = manualTalkView;
            director?.BindViews(dialogueView, manualTalkView);
        }

        public bool DebugStart(string dialogueId) => director != null && director.DebugStart(dialogueId);

        private void Update()
        {
            if (director == null) return;
            if (boundView is Object view && view == null)
            {
                BindViews(null, null);
                return;
            }
            director.Tick(Time.deltaTime);
        }

        // 판정 시계(TripCheckClock)와 같은 기준 - 진행 이벤트는 정지 중엔 오지 않고, 도착 틱은 세지 않는다.
        private void HandleProgressChanged(float progress)
        {
            if (progress >= 1f) return;
            director.AdvanceTripTime(Time.deltaTime);
        }

        private static IReadOnlyList<CrewMember> CrewOf(ICaravanRosterProvider roster)
        {
            var list = new List<CrewMember>();
            if (roster == null) return list;
            foreach (var unit in roster.GetRoster())
            {
                if (unit.Kind == FormationUnitKind.Character) list.Add(new CrewMember(unit.Id, unit.DisplayName));
            }
            return list;
        }

        private static string NameOf(string id, ICharacterCatalogReader catalog, IEncounterContentReader content)
        {
            if (catalog != null && catalog.TryGet(id, out var profile)) return profile.Name;
            return content.TryGetPerson(id, out var person) ? person.Name : null;
        }

        private void Unsubscribe()
        {
            if (director == null) return;
            if (checks != null) checks.DialogueCheck -= director.OnDialogueCheck;
            if (session != null) session.OnProgressChanged -= HandleProgressChanged;
            if (milestones != null) milestones.Raised -= director.OnMilestone;
            if (halt != null)
            {
                halt.HaltStarted -= director.OnHaltStarted;
                halt.HaltEnded -= director.OnHaltEnded;
            }
            if (visibility != null) visibility.ModalPopupOpenChanged -= director.SetModalOpen;
        }

        private void OnDestroy() => Unsubscribe();
    }
}

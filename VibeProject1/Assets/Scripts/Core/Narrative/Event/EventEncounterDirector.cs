using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>진행자가 쓰는 값·서비스 묶음. 테스트가 필요한 것만 채우도록 기본값을 둔다.</summary>
    public sealed class EventEncounterSources
    {
        public IEncounterContentReader Content;
        public IEncounterHistoryReader History;
        public IEncounterHistoryWriter HistoryWriter;
        public IDialogueScriptRunner Runner;
        public EncounterConditionRegistry Conditions = EncounterConditionRegistry.CreateDefault();
        public Func<EncounterConditionContext> Context = () => default;
        public Func<float> Random01 = () => 0.99f;
        public Func<string, string> NameOf = _ => null;
        public ITripHaltControl Halt;
        public ITripMilestoneSink Milestones;
        public IEventBattleRequester Battle;
        public IFieldUIHideControl UIHide;
        /// <summary>효과 명령 맥락(설계 109 §5.1) - 시작에 Enter, 끝에 Exit. 없으면 효과 명령이 이 진행자를 보지 못한다(테스트·명령 없는 구성).</summary>
        public IEventEffectRouter Router;
        public EventEffectServices Services;
        /// <summary>이 진행자가 고르는 이벤트(장소별 목록, 설계 109 §3.1). null이면 전체.</summary>
        public IReadOnlyList<EventEntry> Candidates;
        public Action<string> Warn = _ => { };
    }

    /// <summary>이벤트 실행 하나의 끝(설계 109 §5.2) - 휴식 흐름 재개용. 결과 줄은 효과 결과 문구를 나온 순서대로 모은 것.</summary>
    public readonly struct EventFinish
    {
        public string EventId { get; }
        public bool Recorded { get; }
        public IReadOnlyList<string> ResultLines { get; }

        public EventFinish(string eventId, bool recorded, IReadOnlyList<string> resultLines)
        {
            EventId = eventId;
            Recorded = recorded;
            ResultLines = resultLines ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 이벤트 진행 규칙(설계 89 §4). Unity를 모르는 순수 C# - FieldManager 하위 껍데기(EventEncounterCoordinator)가 신호·프레임을 넘긴다.
    /// - 판정 → (정비창·방향성 지시가 열려 있으면 "이벤트 대기" 정지 후 닫힌 다음 프레임에) "이벤트" 정지 → 상단 버튼 비활성·열람 모드 → 대화 화면 → 대본.
    ///   닫힘 신호 처리 중에 바로 시작하지 않는 이유: 그 신호 안에서 모달을 다시 열면 같은 신호의 나머지 구독자가 이미 뒤집힌 상태를 받는다.
    /// - 이벤트는 중단되지 않는다(이벤트 중엔 판정·대화 검사가 상행 정지로 멈춘다). 끝까지 진행했을 때만 기록하되, 도주·패배로 흐름이 끝난 것도 "끝"이다.
    /// - 전투: 대화 화면을 닫고 인카운터 흐름에 요청 → 승리면 화면을 다시 열고 대본 계속(이벤트 정지 유지), 도주면 종료 처리, 패배는 기록만 하고 정지·신호는
    ///   기존 패배 처리(상행 종료 신호가 원인을 비운다)에 맡긴다. 도주·패배·상행 종료 뒤 남은 대본은 Yarn을 멈추지 않고 화면 없이 흘려보낸다(Drain).
    /// 대사 재생 상태는 이벤트마다 새로 만든다 - 이전 이벤트의 일시정지가 남지 않게.
    /// 이벤트 중 Field UI 가림(§13): 시작·승리 재오픈 때 걸고 전투 요청·종료 때 푼다 - 정비창 보기로 이벤트 화면이 잠시 닫혀도 유지한다.
    /// </summary>
    public sealed class EventEncounterDirector : IEventEncounterDebug, IEventBattleOutcomeSink, IEventEffectHost
    {
        private static readonly object FormationPauseReason = new();

        private enum Phase { Idle, Pending, Running }

        private sealed class Run
        {
            public EventEntry Entry;
            public DialoguePlaybackController Playback;
            public EventPresentationTarget Target;
            public bool HadBattle;
            public bool Started;   // 대본 실행까지 갔는지 - 실행 실패는 끝 알림을 내지 않는다(TryBegin이 false를 돌려준다)
            public readonly List<string> ResultLines = new();
        }

        private readonly EventEncounterSources sources;
        private readonly SpeakerPresentation speakers;
        private readonly FixedEventQueue queue = new();
        private IEventEncounterScreens screens;
        private Phase phase = Phase.Idle;
        private EventEntry pendingEntry;
        private bool startRequested;
        private bool modalOpen;
        private Run current;
        private Action battleWaiter;
        private bool lastRunStarted;   // TryBegin 결과 - 대본 실행 실패는 시작 직후 Idle로 돌아와 phase만으로 구분되지 않는다

        public EventEncounterDirector(EventEncounterSources sources)
        {
            this.sources = sources;
            speakers = new SpeakerPresentation(sources.NameOf);
        }

        public bool IsRunning => phase == Phase.Running;
        public string CurrentEventId => current?.Entry.Id;
        public event Action<string> EventStarted;
        /// <summary>실행된 이벤트가 끝났다(정상·도주·패배). 실행 실패와 Abort는 알리지 않는다 - 부른 쪽이 이미 안다.</summary>
        public event Action<EventFinish> Finished;
        public string PendingEventId => phase == Phase.Pending ? pendingEntry?.Id : null;
        public IReadOnlyList<EventEntry> QueuedEvents => queue.Snapshot();

        private bool HasScreens => screens != null && screens.IsAvailable;
        private IReadOnlyList<EventEntry> CandidateEvents => sources.Candidates ?? sources.Content.Events;

        public void BindScreens(IEventEncounterScreens newScreens)
        {
            if (screens != null)
            {
                screens.Clicked -= Click;
                screens.FormationViewRequested -= HandleFormationViewRequested;
                screens.Reshown -= HandleReshown;
            }
            screens = newScreens;
            if (screens != null)
            {
                screens.Clicked += Click;
                screens.FormationViewRequested += HandleFormationViewRequested;
                screens.Reshown += HandleReshown;
            }
        }

        public void OnEventCheck()
        {
            if (phase != Phase.Idle || !HasScreens) return;
            var entry = EventEncounterSelector.PickForCheck(queue, CandidateEvents, sources.History, sources.Conditions, sources.Context(), sources.Random01);
            if (entry != null) Begin(entry);
        }

        public bool DebugStart(string eventId)
            => sources.Content.TryGetEvent(eventId, out var entry) && Contains(CandidateEvents, entry) && TryBegin(entry);

        /// <summary>
        /// 고른 이벤트를 조건·횟수 판정 없이 시작한다(설계 109 §5.2 - 휴식 흐름·디버그). 대본을 실행하지 못하면 false. 모달이 열려 있으면 대기로 들어가 true.
        /// </summary>
        public bool TryBegin(EventEntry entry)
        {
            if (entry == null || phase != Phase.Idle || !HasScreens) return false;
            lastRunStarted = false;
            Begin(entry);
            return phase != Phase.Idle || lastRunStarted;
        }

        /// <summary>
        /// 실행 중 이벤트를 기록·신호·끝 알림 없이 끝낸다(상행 종료 방어, 휴식 비정상 종료 - 설계 109 §5.2). 남은 대본은 Yarn을 멈추지 않고 흘려보낸다(Drain).
        /// 대기 중이면 대기만 푼다.
        /// </summary>
        public void Abort()
        {
            if (phase == Phase.Pending)
            {
                phase = Phase.Idle;
                pendingEntry = null;
                startRequested = false;
                sources.Halt?.Release(TripHaltReason.EventPending);
                return;
            }
            if (phase != Phase.Running) return;
            var run = current;
            var waiter = battleWaiter;
            End(run, record: false, signal: false, notify: false);
            Drain(run, waiter);
        }

        public void SetModalOpen(bool open)
        {
            modalOpen = open;
            if (!open && phase == Phase.Pending) startRequested = true;
        }

        public void OnMilestone(TripMilestone milestone)
        {
            if (milestone != TripMilestone.TripStarted && milestone != TripMilestone.TripEnded) return;
            queue.Clear();
            if (phase == Phase.Pending)
            {
                phase = Phase.Idle;
                pendingEntry = null;
                startRequested = false;
            }
            else if (phase == Phase.Running && milestone == TripMilestone.TripEnded)
            {
                // 정상 흐름에선 오지 않는다(이벤트 중 상행은 정지) - 방어적으로 끝낸다. 도주와 같은 종료 정리(가림·화면·상단 버튼)를 하되 기록·신호는 내지
                // 않는다(끝까지 진행하지 않았고 상행도 끝났다).
                Abort();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (phase == Phase.Pending && startRequested && !modalOpen)
            {
                startRequested = false;
                StartPending();
            }
            current?.Playback.Tick(deltaSeconds);
        }

        public void Click() => current?.Playback.Click();

        // IEventEffectHost
        public void PresentResult(string text, Action onDone)
        {
            if (current == null)
            {
                onDone?.Invoke();
                return;
            }
            current.ResultLines.Add(text);
            current.Target.PresentResult(text, onDone);
        }

        public bool TryShowCargoRecovery(Action onDone) => HasScreens && screens.TryShowCargoRecovery(onDone);

        public void RequestBattle(Action onVictory)
        {
            if (current == null) return;
            if (sources.Battle == null)
            {
                sources.Warn("<<battle>>: 전투 입구가 없다 - 승리로 보고 계속한다(Tools > Game > Build Bootstrap Scene).");
                onVictory?.Invoke();
                return;
            }
            current.HadBattle = true;
            battleWaiter = onVictory;
            screens?.HideDialogue();
            sources.UIHide?.Release(FieldUIHideReason.Event);   // 전투 동안은 이동 뷰·디버그가 보인다(설계 89 §13.3)
            sources.Battle.RequestEventBattle();
        }

        // IEventBattleOutcomeSink - 전투 마무리(FinishStep)가 이동 뷰 복귀 뒤에 부른다.
        public void OnEventBattleFinished(EventBattleOutcome outcome)
        {
            var run = current;
            if (run == null) return;
            var waiter = battleWaiter;
            battleWaiter = null;

            switch (outcome)
            {
                case EventBattleOutcome.Victory:
                    sources.UIHide?.Acquire(FieldUIHideReason.Event);
                    if (HasScreens)
                    {
                        screens.SetEventControls(true);
                        screens.ShowDialogue();
                    }
                    waiter?.Invoke();
                    break;
                case EventBattleOutcome.Flee:
                    End(run, record: true, signal: true);
                    Drain(run, waiter);
                    break;
                default:
                    // 패배 - 기록만 하고 정지·신호는 기존 패배 처리(상행 종료 신호가 원인을 비운다)에 맡긴다.
                    sources.HistoryWriter?.RecordCompleted(run.Entry.Id);
                    Reset();
                    Finished?.Invoke(new EventFinish(run.Entry.Id, true, run.ResultLines));
                    Drain(run, waiter);
                    break;
            }
        }

        private void Begin(EventEntry entry)
        {
            pendingEntry = entry;
            phase = Phase.Pending;
            startRequested = false;
            if (modalOpen)
            {
                sources.Halt?.Acquire(TripHaltReason.EventPending);
                return;
            }
            StartPending();
        }

        private void StartPending()
        {
            var entry = pendingEntry;
            pendingEntry = null;
            if (entry == null || !HasScreens)
            {
                phase = Phase.Idle;
                sources.Halt?.Release(TripHaltReason.EventPending);
                return;
            }

            // 이벤트 정지를 먼저 걸고 대기 정지를 푼다 - 반대면 그 사이 상행이 한 번 재개된다.
            sources.Halt?.Acquire(TripHaltReason.Event);
            sources.Halt?.Release(TripHaltReason.EventPending);

            var playback = new DialoguePlaybackController(new StagedDialogueView(screens.DialogueStage, speakers), DialoguePlaybackOptions.Event);
            var run = new Run { Entry = entry, Playback = playback, Target = new EventPresentationTarget(playback, screens, speakers) };
            current = run;
            phase = Phase.Running;
            sources.Router?.Enter(this, sources.Services);
            screens.SetEventControls(true);
            sources.UIHide?.Acquire(FieldUIHideReason.Event);
            screens.ShowDialogue();
            EventStarted?.Invoke(entry.Id);

            run.Started = true;
            lastRunStarted = true;
            if (!sources.Runner.TryRun(entry.YarnNode, run.Target, completed => HandleScriptFinished(run, completed)))
            {
                sources.Warn($"이벤트 '{entry.Id}': Yarn 노드 '{entry.YarnNode}'를 실행하지 못했다.");
                run.Started = false;
                lastRunStarted = false;
                End(run, record: false, signal: false);
            }
        }

        private void HandleScriptFinished(Run run, bool completed)
        {
            if (current != run) return;
            End(run, record: completed, signal: true);
        }

        private void End(Run run, bool record, bool signal, bool notify = true)
        {
            if (current != run) return;
            Reset();
            sources.UIHide?.Release(FieldUIHideReason.Event);
            if (record) sources.HistoryWriter?.RecordCompleted(run.Entry.Id);
            if (HasScreens)
            {
                screens.HideChoice();
                screens.HideDialogue();
                screens.SetEventControls(false);
            }
            sources.Halt?.Release(TripHaltReason.Event);
            if (signal)
            {
                sources.Milestones?.Raise(TripMilestone.EventEnded);
                if (run.HadBattle) sources.Milestones?.Raise(TripMilestone.BattleEnded);
            }
            if (notify && run.Started) Finished?.Invoke(new EventFinish(run.Entry.Id, record, run.ResultLines));
        }

        // 끝난 이벤트의 남은 대본을 흘려보낸다(EventPresentationTarget.Abort 요약 참고) - 전투 명령을 끝내 대본이 진행되게 하고, 남은 줄·선택지는 화면 없이 넘기며,
        // 효과 명령은 진행자가 실행 중이 아니라 아무것도 하지 않는다. 대본 완료 콜백은 current가 바뀌어 무시된다.
        private static void Drain(Run run, Action battleWaiter)
        {
            run.Target.Abort();
            battleWaiter?.Invoke();
        }

        // 효과 명령 맥락도 여기서 뺀다 - 끝난 이벤트의 남은 대본(Drain)이 효과를 내지 않게 하는 것이 이 Exit다.
        private void Reset()
        {
            sources.Router?.Exit(this);
            current = null;
            phase = Phase.Idle;
            battleWaiter = null;
        }

        private static bool Contains(IReadOnlyList<EventEntry> list, EventEntry entry)
        {
            for (var i = 0; i < list.Count; i++) if (list[i] == entry) return true;
            return false;
        }

        private void HandleFormationViewRequested()
        {
            if (current == null || !HasScreens) return;
            current.Playback.SetPaused(FormationPauseReason, true);
            screens.OpenFormationViewer();
        }

        private void HandleReshown() => current?.Playback.SetPaused(FormationPauseReason, false);
    }
}

using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>진행자가 쓰는 값·서비스 묶음. 테스트가 필요한 것만 채우도록 기본값을 둔다.</summary>
    public sealed class TripDialogueSources
    {
        public IEncounterContentReader Content;
        public IEncounterHistoryReader History;
        public IEncounterHistoryWriter HistoryWriter;
        public IDialogueScriptRunner Runner;
        public EncounterConditionRegistry Conditions = EncounterConditionRegistry.CreateDefault();
        public Func<EncounterConditionContext> Context = () => default;
        public Func<string, bool> IsHired = _ => true;
        public Func<IReadOnlyList<CrewMember>> Crew = () => Array.Empty<CrewMember>();
        public Func<string, string> NameOf = _ => null;
        public Func<float> Random01 = () => 0.99f;
        public Func<bool> IsHalted = () => false;
    }

    /// <summary>
    /// 상행 대화 진행 규칙(설계 88 §3·§4). Unity를 모르는 순수 C# - FieldManager 하위 껍데기(TripDialogueCoordinator)가 신호·프레임을 넘긴다.
    /// - 상황 신호는 다음 20초 검사까지 쌓아 두고, 검사 때 대화가 진행 중이면 그냥 버린다(소멸).
    /// - 예외: 전투 후 대화는 20초 검사를 기다리지 않는다. 상행 재개(전투 종료 신호)부터 상행 시간 5초 뒤에 전투 후 대화만 찾는다 - 랜덤 대화로 대체하지 않는다.
    ///   상행 시간으로 세는 이유는 판정 시계와 같이 정지 중엔 멈추게 하기 위해서다. 그 사이 정지·상행 종료가 오면 취소한다.
    /// - 상행 정지(전투·이벤트)·상행 종료 = 즉시 종료(기록 없음). 모달 화면(정비창·방향성 지시) = 일시정지.
    /// - 끝까지 진행한 대화는 종류와 무관하게 기록한다(조건 seen용 - 횟수 제한은 후보 거르기가 종류별로 판단).
    /// 대사 재생 상태(DialoguePlaybackController)는 대화마다 새로 만든다 - 이전 대화의 일시정지·선택지 상태가 남지 않게.
    /// </summary>
    public sealed class TripDialogueDirector : ITripDialogueStatus
    {
        private static readonly object ModalPauseReason = new();
        private static readonly DialogueTrigger[] AfterBattleOnly = { DialogueTrigger.AfterBattle };
        public const float AfterBattleDelaySeconds = 5f;

        private sealed class Run
        {
            public DialogueEntry Entry;
            public DialoguePlaybackController Controller;
        }

        private readonly TripDialogueSources sources;
        private readonly SpeakerPresentation speakers;
        private readonly List<DialogueTrigger> pending = new();
        private ITripDialogueView dialogueView;
        private IManualTalkView manualView;
        private Run current;
        private bool modalOpen;
        private float afterBattleRemaining = -1f; // 음수 = 대기 없음

        public TripDialogueDirector(TripDialogueSources sources)
        {
            this.sources = sources;
            speakers = new SpeakerPresentation(sources.NameOf);
        }

        public bool IsRunning => current != null;
        public string CurrentDialogueId => current?.Entry.Id;
        public IReadOnlyList<DialogueTrigger> PendingTriggers => pending;
        public float AfterBattleSecondsRemaining => afterBattleRemaining;

        /// <summary>이전 씬 화면으로 진행 중이던 대화는 화면을 건드리지 않고 끝낸다 - 그 화면은 씬과 함께 이미 파괴됐을 수 있다.</summary>
        public void BindViews(ITripDialogueView newDialogueView, IManualTalkView newManualView)
        {
            StopCurrent(hideView: false);
            if (dialogueView != null) dialogueView.Clicked -= Click;
            if (manualView != null)
            {
                manualView.OpenRequested -= HandleOpenRequested;
                manualView.MemberChosen -= HandleMemberChosen;
            }

            dialogueView = newDialogueView;
            manualView = newManualView;
            if (dialogueView != null) dialogueView.Clicked += Click;
            if (manualView != null)
            {
                manualView.OpenRequested += HandleOpenRequested;
                manualView.MemberChosen += HandleMemberChosen;
            }
            RefreshTalkAllowed();
        }

        public void OnMilestone(TripMilestone milestone)
        {
            switch (milestone)
            {
                case TripMilestone.TripStarted:
                    pending.Clear();
                    afterBattleRemaining = -1f;
                    pending.Add(DialogueTrigger.Departure);
                    break;
                case TripMilestone.TripEnded:
                    StopCurrent(hideView: true);
                    pending.Clear();
                    afterBattleRemaining = -1f;
                    manualView?.HideMembers();
                    break;
                case TripMilestone.BattleEnded:
                    afterBattleRemaining = AfterBattleDelaySeconds;
                    break;
                default:
                    if (TripDialogueSelector.TryMapMilestone(milestone, out var trigger)) pending.Add(trigger);
                    break;
            }
        }

        public void OnDialogueCheck()
        {
            if (IsRunning || dialogueView == null)
            {
                pending.Clear();
                return;
            }
            var entry = TripDialogueSelector.PickForCheck(pending, sources.Content.Dialogues, sources.History, IsSpeakerAvailable, sources.Conditions, sources.Context(), sources.Random01);
            pending.Clear();
            if (entry != null) Start(entry);
        }

        /// <summary>상행 시간 진행(정지 중·도착 틱엔 오지 않는다). 전투 후 대화 대기만 센다.</summary>
        public void AdvanceTripTime(float deltaSeconds)
        {
            if (afterBattleRemaining < 0f) return;
            afterBattleRemaining -= deltaSeconds;
            if (afterBattleRemaining > 0f) return;
            afterBattleRemaining = -1f;
            if (IsRunning || dialogueView == null) return;
            var entry = TripDialogueSelector.PickTriggered(AfterBattleOnly, sources.Content.Dialogues, sources.History, IsSpeakerAvailable, sources.Conditions, sources.Context());
            if (entry != null) Start(entry);
        }

        public void OnHaltStarted()
        {
            afterBattleRemaining = -1f;
            StopCurrent(hideView: true);
            manualView?.HideMembers();
            RefreshTalkAllowed();
        }

        public void OnHaltEnded() => RefreshTalkAllowed();

        public void SetModalOpen(bool open)
        {
            modalOpen = open;
            current?.Controller.SetPaused(ModalPauseReason, open);
        }

        public void Tick(float deltaSeconds) => current?.Controller.Tick(deltaSeconds);

        public void Click() => current?.Controller.Click();

        public bool DebugStart(string dialogueId)
        {
            if (!sources.Content.TryGetDialogue(dialogueId, out var entry)) return false;
            StopCurrent(hideView: true);
            return Start(entry);
        }

        private bool Start(DialogueEntry entry)
        {
            if (dialogueView == null) return false;
            var controller = new DialoguePlaybackController(new StagedDialogueView(dialogueView, speakers), DialoguePlaybackOptions.Trip);
            if (modalOpen) controller.SetPaused(ModalPauseReason, true);
            var run = new Run { Entry = entry, Controller = controller };
            current = run;
            if (!sources.Runner.TryRun(entry.YarnNode, controller, completed => Finish(run, completed)))
            {
                if (current == run) current = null;
                return false;
            }
            manualView?.HideMembers();
            RefreshTalkAllowed();
            return true;
        }

        private void Finish(Run run, bool completed)
        {
            if (completed) sources.HistoryWriter?.RecordCompleted(run.Entry.Id);
            if (current != run) return;
            current = null;
            RefreshTalkAllowed();
        }

        private void StopCurrent(bool hideView)
        {
            if (current == null) return;
            var run = current;
            current = null;
            sources.Runner.Stop();
            if (hideView) run.Controller.Stop();
            RefreshTalkAllowed();
        }

        private void HandleOpenRequested()
        {
            if (!CanTalk) return;
            manualView.ShowMembers(BuildMembers());
        }

        private void HandleMemberChosen(string characterId)
        {
            manualView?.HideMembers();
            if (!CanTalk) return;
            var entry = TripDialogueSelector.PickManual(characterId, sources.Content.Dialogues, sources.History, IsSpeakerAvailable, sources.Conditions, sources.Context(), sources.Random01);
            if (entry != null) Start(entry);
        }

        private IReadOnlyList<ManualTalkMember> BuildMembers()
        {
            var context = sources.Context();
            var crew = sources.Crew();
            var list = new List<ManualTalkMember>(crew.Count);
            foreach (var member in crew)
            {
                var hasTalk = TripDialogueSelector.HasManualTalk(member.CharacterId, sources.Content.Dialogues, sources.History, IsSpeakerAvailable, sources.Conditions, context);
                list.Add(new ManualTalkMember(member.CharacterId, member.Name, hasTalk));
            }
            return list;
        }

        private bool CanTalk => dialogueView != null && manualView != null && !IsRunning && !sources.IsHalted();

        private void RefreshTalkAllowed() => manualView?.SetTalkAllowed(CanTalk);

        // 외부 인물은 고용 판정에서 제외한다(설계 87 §5.2).
        private bool IsSpeakerAvailable(string speakerId) => sources.Content.TryGetPerson(speakerId, out _) || sources.IsHired(speakerId);
    }
}

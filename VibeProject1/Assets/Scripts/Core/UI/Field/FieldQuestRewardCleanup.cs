using System;

namespace Game.Core
{
    /// <summary>
    /// 퀘스트 보상이 임시 보관으로 가면 그 자리에서 정리 화면(회수 적재)을 연다(기획 106 §4.3, Docs/설계/107번 §8.3, 2026-10-27 사용자 결정).
    /// - 상행 중: 전투·이벤트·이벤트 대기 정지나 모달이 걸려 있으면 그 흐름이 끝난 뒤(정지 해제·모달 닫힘·이벤트 종료) 연다. 여는 동안은 상행을 멈춘다.
    /// - 최종 도착: 도착 처리 중에 완료가 일어나므로 그 사이 자동으로 열지 않고(BeginArrival), 도착 팝업 직전에 열어 닫힌 뒤 팝업을 띄운다(TryRunBeforeArrival).
    ///   상행이 이미 끝났으니 정지를 걸지 않는다.
    /// 화면이 없으면(인스톨러 미실행) 표시를 지우고 넘어간다 - 보상은 임시 보관에 남고 출발 경고가 다룬다. Field 로드마다 새로 만들고 교체 시 Dispose한다.
    /// </summary>
    public sealed class FieldQuestRewardCleanup : IDisposable
    {
        private readonly IQuestRewardCleanupSource source;
        private readonly ITripHaltControl halt;
        private readonly IEventEncounterStatus events;
        private readonly IUIVisibilitySignal visibility;
        private readonly ITripMilestoneSource milestones;
        private readonly Func<Action, bool> tryShowRecovery;

        private bool showing;
        private bool arrivalHold;
        private Action afterShowing;

        public FieldQuestRewardCleanup(IQuestRewardCleanupSource source, ITripHaltControl halt, IEventEncounterStatus events, IUIVisibilitySignal visibility,
            ITripMilestoneSource milestones, Func<Action, bool> tryShowRecovery)
        {
            this.source = source;
            this.halt = halt;
            this.events = events;
            this.visibility = visibility;
            this.milestones = milestones;
            this.tryShowRecovery = tryShowRecovery;

            if (source != null) source.RewardStaged += TryRun;
            if (halt != null) halt.HaltEnded += TryRun;
            if (visibility != null) visibility.ModalPopupOpenChanged += HandleModalChanged;
            if (milestones != null) milestones.Raised += HandleMilestone;
        }

        /// <summary>최종 도착 처리 직전에 부른다 - 도착 처리 안에서 난 보상 알림으로 화면이 먼저 열리지 않게 한다.</summary>
        public void BeginArrival() => arrivalHold = true;

        /// <summary>정리 대기가 있으면 화면을 열고 닫힌 뒤 onDone, 없으면 바로 onDone. 반환 = 화면을 열었는지(열려 있던 것 포함).</summary>
        public bool TryRunBeforeArrival(Action onDone)
        {
            arrivalHold = false;
            if (showing)
            {
                afterShowing += onDone;
                return true;
            }
            if (source == null || !source.HasPendingCleanup)
            {
                onDone?.Invoke();
                return false;
            }
            return Open(withHalt: false, onDone);
        }

        public void Dispose()
        {
            if (source != null) source.RewardStaged -= TryRun;
            if (halt != null) halt.HaltEnded -= TryRun;
            if (visibility != null) visibility.ModalPopupOpenChanged -= HandleModalChanged;
            if (milestones != null) milestones.Raised -= HandleMilestone;
        }

        private void HandleModalChanged(bool open)
        {
            if (!open) TryRun();
        }

        private void HandleMilestone(TripMilestone milestone)
        {
            if (milestone == TripMilestone.EventEnded || milestone == TripMilestone.BattleEnded) TryRun();
        }

        private void TryRun()
        {
            if (arrivalHold || showing || source == null || !source.HasPendingCleanup || IsBusy()) return;
            Open(withHalt: true, null);
        }

        private bool IsBusy()
        {
            if (events != null && events.IsRunning) return true;
            if (visibility != null && visibility.IsModalPopupOpen) return true;
            return halt != null && (halt.IsHeld(TripHaltReason.Battle) || halt.IsHeld(TripHaltReason.Event) || halt.IsHeld(TripHaltReason.EventPending));
        }

        private bool Open(bool withHalt, Action onDone)
        {
            showing = true;
            afterShowing = onDone;
            if (withHalt) halt?.Acquire(TripHaltReason.QuestReward);
            if (tryShowRecovery != null && tryShowRecovery(() => Finish(withHalt))) return true;

            Finish(withHalt); // 화면 없음 - 보상은 임시 보관에 남는다
            return false;
        }

        private void Finish(bool withHalt)
        {
            showing = false;
            source?.ClearPendingCleanup();
            if (withHalt) halt?.Release(TripHaltReason.QuestReward);
            var callback = afterShowing;
            afterShowing = null;
            callback?.Invoke();
        }
    }
}

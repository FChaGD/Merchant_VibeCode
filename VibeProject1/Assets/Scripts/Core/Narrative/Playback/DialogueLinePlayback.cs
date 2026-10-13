using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum DialogueLinePhase { Idle, Typing, Waiting, Finished }

    /// <summary>
    /// 대사 한 줄의 진행 상태기(설계 87 §7). 타자기(초당 12자) → 대기(길이 비례) → 완료. 클릭은 2단계(출력 중 = 전체 표시, 출력 후 = 완료).
    /// 자동 넘김은 대화만 켠다(이벤트는 클릭으로만). finishOnTypingComplete = 선택지 앞 질문 줄(#ask) - 출력이 끝나면 대기 없이 완료해 선택지가 곧바로 뜬다.
    /// 일시정지 원인이 하나라도 있으면 출력·대기·클릭이 모두 멈춘다(모달 화면이 열린 동안).
    /// </summary>
    public sealed class DialogueLinePlayback
    {
        private readonly bool autoAdvance;
        private readonly HashSet<object> pauseReasons = new();
        private int typedTotal;
        private float typingElapsed;
        private float waitElapsed;
        private float waitDuration;
        private bool finishOnTypingComplete;

        public DialogueLinePlayback(bool autoAdvance) => this.autoAdvance = autoAdvance;

        public DialogueLinePhase Phase { get; private set; } = DialogueLinePhase.Idle;
        public string Text { get; private set; } = string.Empty;
        public int VisibleLength { get; private set; }
        public bool IsPaused => pauseReasons.Count > 0;

        public event Action TypingCompleted;
        public event Action Finished;

        public void Begin(string text, bool finishOnTypingComplete)
        {
            Text = text ?? string.Empty;
            this.finishOnTypingComplete = finishOnTypingComplete;
            typedTotal = DialogueTextMetrics.CountTyped(Text);
            typingElapsed = 0f;
            waitElapsed = 0f;
            waitDuration = DialoguePacing.WaitSecondsFor(typedTotal);
            Phase = DialogueLinePhase.Typing;
            VisibleLength = DialogueTextMetrics.VisibleLengthFor(Text, 0);
            if (typedTotal == 0) CompleteTyping();
        }

        public void Tick(float delta)
        {
            if (IsPaused || delta <= 0f) return;

            if (Phase == DialogueLinePhase.Typing)
            {
                typingElapsed += delta;
                var typed = Math.Min(typedTotal, (int)(typingElapsed * DialoguePacing.CharactersPerSecond + 1e-4f));
                VisibleLength = DialogueTextMetrics.VisibleLengthFor(Text, typed);
                if (typed >= typedTotal) CompleteTyping();
                return;
            }

            if (Phase == DialogueLinePhase.Waiting && autoAdvance)
            {
                waitElapsed += delta;
                if (waitElapsed >= waitDuration) Finish();
            }
        }

        public void Click()
        {
            if (IsPaused) return;
            if (Phase == DialogueLinePhase.Typing) CompleteTyping();
            else if (Phase == DialogueLinePhase.Waiting) Finish();
        }

        public void SetPaused(object reason, bool paused)
        {
            if (reason == null) return;
            if (paused) pauseReasons.Add(reason);
            else pauseReasons.Remove(reason);
        }

        /// <summary>중단 - 완료 이벤트 없이 멈춘다(설계 87 §7.2 즉시 종료).</summary>
        public void Stop()
        {
            Phase = DialogueLinePhase.Idle;
        }

        private void CompleteTyping()
        {
            VisibleLength = Text.Length;
            Phase = DialogueLinePhase.Waiting;
            waitElapsed = 0f;
            TypingCompleted?.Invoke();
            if (finishOnTypingComplete && Phase == DialogueLinePhase.Waiting) Finish();
        }

        private void Finish()
        {
            Phase = DialogueLinePhase.Finished;
            Finished?.Invoke();
        }
    }
}

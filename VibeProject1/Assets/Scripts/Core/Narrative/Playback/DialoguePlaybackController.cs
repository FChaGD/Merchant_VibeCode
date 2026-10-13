using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>재생 옵션 - 대화(자동 넘김, 선택지 10초)와 이벤트(클릭만, 제한 없음)의 차이만 담는다(설계 87 §7.2).</summary>
    public readonly struct DialoguePlaybackOptions
    {
        public bool AutoAdvance { get; }
        /// <summary>0 이하 = 제한 없음.</summary>
        public float OptionTimeoutSeconds { get; }

        public DialoguePlaybackOptions(bool autoAdvance, float optionTimeoutSeconds)
        {
            AutoAdvance = autoAdvance;
            OptionTimeoutSeconds = optionTimeoutSeconds;
        }

        public static DialoguePlaybackOptions Trip => new(true, DialoguePacing.DialogueOptionTimeoutSeconds);
        public static DialoguePlaybackOptions Event => new(false, 0f);
    }

    /// <summary>
    /// 줄 상태기 + 화면 + 선택지 제한시간을 묶는다(설계 87 §7.1). Yarn 연결 표시기가 PresentLine/PresentOptions를 부르고, 소유자(88·89 진행자)가 매 프레임
    /// Tick·클릭·일시정지를 전달한다. 무응답(#timeout) 선택지는 화면에 내보내지 않고 시간이 다 됐을 때만 고른다. 보이는 선택지가 모두 잠겼고 무응답도
    /// 없으면 선택 없음(-1)으로 넘긴다 - 대본이 멈추지 않게.
    /// </summary>
    public sealed class DialoguePlaybackController : IDialoguePresentationTarget
    {
        private readonly IDialogueView view;
        private readonly DialoguePlaybackOptions options;
        private readonly DialogueLinePlayback line;
        private readonly HashSet<object> pauseReasons = new();

        private Action onAdvance;
        private Action<int> onChosen;
        private int timeoutIndex = -1;
        private float optionElapsed;

        public DialoguePlaybackController(IDialogueView view, DialoguePlaybackOptions options)
        {
            this.view = view;
            this.options = options;
            line = new DialogueLinePlayback(options.AutoAdvance);
            line.TypingCompleted += () => view.SetTypingComplete(true);
            line.Finished += HandleLineFinished;
        }

        public bool IsShowingOptions => onChosen != null;

        public void PresentLine(DialogueLineData data, Action onAdvance)
        {
            this.onAdvance = onAdvance;
            view.HideOptions();
            view.ShowLine(data);
            view.SetTypingComplete(false);
            // 선택지 앞 질문 줄은 출력이 끝나면 대기 없이 넘긴다 - Yarn이 그 줄에 lastline을 자동으로 붙이고, 작가가 #ask로 직접 표시할 수도 있다.
            line.Begin(data.Text, finishOnTypingComplete: data.HasTag(DialogueTags.Ask) || data.HasTag(DialogueTags.LastLine));
            view.SetVisibleLength(line.VisibleLength);
        }

        public void PresentOptions(IReadOnlyList<DialogueOptionData> all, Action<int> onChosen)
        {
            var visible = new List<DialogueOptionData>();
            timeoutIndex = -1;
            var anyAvailable = false;
            foreach (var option in all)
            {
                if (option.IsTimeout)
                {
                    // 제한시간이 없는 실행(이벤트)에선 무응답이 골라질 수 없어 없는 것으로 본다 - 전부 잠김이면 아래에서 선택 없음으로 넘긴다.
                    if (options.OptionTimeoutSeconds > 0f) timeoutIndex = option.Index;
                    continue;
                }
                visible.Add(option);
                anyAvailable |= option.IsAvailable;
            }

            if (!anyAvailable && timeoutIndex < 0)
            {
                onChosen(-1);
                return;
            }

            this.onChosen = onChosen;
            optionElapsed = 0f;
            view.ShowOptions(visible, index => Choose(index, requireAvailable: true, visible));
            if (HasTimeout) view.SetOptionTimeRemaining(1f);
        }

        public void EndPresentation() => view.Hide();

        public void Tick(float delta)
        {
            line.Tick(delta);
            if (IsShowingOptions && HasTimeout && pauseReasons.Count == 0 && delta > 0f)
            {
                optionElapsed += delta;
                view.SetOptionTimeRemaining(Math.Max(0f, 1f - optionElapsed / options.OptionTimeoutSeconds));
                if (optionElapsed >= options.OptionTimeoutSeconds) Choose(timeoutIndex, requireAvailable: false, null);
            }
            if (!IsShowingOptions) view.SetVisibleLength(line.VisibleLength);
        }

        public void Click()
        {
            if (IsShowingOptions) return;
            line.Click();
            view.SetVisibleLength(line.VisibleLength);
        }

        public void SetPaused(object reason, bool paused)
        {
            line.SetPaused(reason, paused);
            if (reason == null) return;
            if (paused) pauseReasons.Add(reason);
            else pauseReasons.Remove(reason);
        }

        /// <summary>즉시 종료 - 콜백 없이 화면을 닫는다(설계 87 §7.2).</summary>
        public void Stop()
        {
            line.Stop();
            onAdvance = null;
            onChosen = null;
            view.HideOptions();
            view.Hide();
        }

        private bool HasTimeout => options.OptionTimeoutSeconds > 0f && timeoutIndex >= 0;

        private void HandleLineFinished()
        {
            var callback = onAdvance;
            onAdvance = null;
            callback?.Invoke();
        }

        private void Choose(int index, bool requireAvailable, List<DialogueOptionData> visible)
        {
            if (onChosen == null) return;
            if (requireAvailable)
            {
                var ok = false;
                foreach (var o in visible) if (o.Index == index && o.IsAvailable) ok = true;
                if (!ok) return;
            }
            var callback = onChosen;
            onChosen = null;
            view.HideOptions();
            callback(index);
        }
    }
}

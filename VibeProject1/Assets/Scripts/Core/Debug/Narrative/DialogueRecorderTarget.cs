#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Game.Core.DebugTools
{
    /// <summary>
    /// 디버깅 전용 - 화면 없이 대본을 끝까지 흘려 보내며 줄·선택지를 기록하는 표시 대상. 줄은 즉시 넘기고 선택지는 지정 순번을 고른다.
    /// Yarn 연결(설계 87 §7)이 화면 없이도 동작하는지 확인하는 데 쓴다(이력 디버그의 "대본 스모크" 버튼, CLI 검증). 걷어낼 때 EncounterDebugView와 함께 지운다.
    /// </summary>
    public sealed class DialogueRecorderTarget : IDialoguePresentationTarget
    {
        private readonly List<string> log;
        private readonly int optionToChoose;
        private readonly bool advanceLines;

        // advanceLines = false면 첫 줄에서 멈춘다 - 중단(Stop) 경로 확인용.
        public DialogueRecorderTarget(List<string> log, int optionToChoose = 0, bool advanceLines = true)
        {
            this.log = log;
            this.optionToChoose = optionToChoose;
            this.advanceLines = advanceLines;
        }

        public void PresentLine(DialogueLineData line, Action onAdvance)
        {
            log.Add($"line:{line.SpeakerId}:{line.Text}|tags={string.Join(",", line.Tags)}");
            if (advanceLines) onAdvance();
        }

        public void PresentOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen)
        {
            foreach (var o in options) log.Add($"opt{o.Index}:{o.Text}|avail={o.IsAvailable}|lock={o.LockText}|timeout={o.IsTimeout}");
            onChosen(optionToChoose);
        }

        public void EndPresentation() => log.Add("end");
    }
}
#endif

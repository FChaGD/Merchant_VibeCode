using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 대사 실행기의 화면 계약(IDialogueView)을 말풍선·좌우 스탠딩 화면(IDialogueStageView)으로 바꾼다(설계 88 §5). 말하는 쪽 밝게·듣는 쪽 어둡게·서술은 양쪽 어둡게,
    /// 대화에 나오지 않은 자리는 숨김. 대화가 끝나 화면을 닫으면(Hide) 자리 배정을 비운다 - 다음 대화는 새로 배정한다.
    /// 선택지 화면은 선택적이다(Docs/Refactor/2026-10-26_씬전용.md G-1) - 없는 화면(이벤트 대화)에 선택지가 오면 대본이 그 자리에서 멈추므로 경고로 드러낸다.
    /// 이벤트는 선택지를 선택 화면으로 보내 이 경로로 오지 않는다(EventPresentationTarget).
    /// </summary>
    public sealed class StagedDialogueView : IDialogueView
    {
        private readonly IDialogueStageView stage;
        private readonly IDialogueOptionsView options;
        private readonly SpeakerPresentation speakers;
        private readonly DialogueStageLayout layout = new();

        public StagedDialogueView(IDialogueStageView stage, SpeakerPresentation speakers, IDialogueOptionsView options = null)
        {
            this.stage = stage;
            this.options = options;
            this.speakers = speakers;
        }

        public void ShowLine(DialogueLineData line)
        {
            stage.SetVisible(true);
            var active = layout.Apply(line);
            ApplyStanding(DialogueSide.Left, layout.LeftSpeaker, active);
            ApplyStanding(DialogueSide.Right, layout.RightSpeaker, active);
            stage.SetBubble(speakers.NameOf(line.SpeakerId), line.Text);
        }

        public void SetVisibleLength(int length) => stage.SetVisibleCharacters(length);
        public void SetTypingComplete(bool complete) => stage.SetContinueMarker(complete);
        public void ShowOptions(IReadOnlyList<DialogueOptionData> visibleOptions, Action<int> onChosen)
        {
            if (options == null)
            {
                UnityEngine.Debug.LogWarning($"{nameof(StagedDialogueView)}: 선택지를 그릴 화면이 없는 대화에 선택지 {visibleOptions.Count}개가 왔다 - 대본이 이 자리에서 멈춘다.");
                return;
            }
            options.ShowOptions(visibleOptions, onChosen);
        }

        public void SetOptionTimeRemaining(float fraction01) => options?.SetOptionTimeRemaining(fraction01);
        public void HideOptions() => options?.HideOptions();

        public void Hide()
        {
            layout.Reset();
            options?.HideOptions();
            stage.SetStanding(DialogueSide.Left, StandingState.Hidden);
            stage.SetStanding(DialogueSide.Right, StandingState.Hidden);
            stage.SetVisible(false);
        }

        private void ApplyStanding(DialogueSide side, string speakerId, DialogueSide? active)
        {
            if (string.IsNullOrEmpty(speakerId))
            {
                stage.SetStanding(side, StandingState.Hidden);
                return;
            }
            stage.SetStanding(side, new StandingState(true, speakers.NameOf(speakerId), SpeakerPresentation.ColorOf(speakerId), active == side));
        }
    }
}

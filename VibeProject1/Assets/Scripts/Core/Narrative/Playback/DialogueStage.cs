using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    public enum DialogueSide { Left, Right }

    /// <summary>스탠딩 한 자리의 표시 상태. Visible=false면 그 자리를 숨긴다(대화에 나오지 않는 자리, 설계 88 §5.2).</summary>
    public readonly struct StandingState
    {
        public bool Visible { get; }
        public string Name { get; }
        public Color Color { get; }
        public bool Highlighted { get; }

        public StandingState(bool visible, string name, Color color, bool highlighted)
        {
            Visible = visible;
            Name = name ?? string.Empty;
            Color = color;
            Highlighted = highlighted;
        }

        public static StandingState Hidden => new(false, string.Empty, Color.clear, false);
    }

    /// <summary>
    /// 말풍선·좌우 스탠딩 화면 계약(설계 88 §5, 89 이벤트 대화 화면도 같은 계약). 자리·이름·색·밝기 결정은 StagedDialogueView가 하고,
    /// 구현은 받은 값을 그리기만 한다 - 자리 규칙을 Unity 없이 검증하기 위해서다. 선택지는 IDialogueOptionsView로 나눴다 - 이벤트 대화 화면은
    /// 선택지를 별도 선택 화면이 맡아 빈 구현으로 채우고 있었다(Docs/Refactor/2026-10-26_씬전용.md SS-12).
    /// </summary>
    public interface IDialogueStageView
    {
        void SetVisible(bool visible);
        /// <summary>새 줄. 보이는 글자 수는 0부터 다시 시작한다.</summary>
        void SetBubble(string speakerName, string text);
        void SetVisibleCharacters(int count);
        void SetContinueMarker(bool shown);
        void SetStanding(DialogueSide side, StandingState state);
    }

    /// <summary>말풍선 아래 선택지(설계 88 §5) - 상행 대화 화면만 구현한다. 이벤트 선택지는 선택 화면(EventPresentationTarget)이 맡는다.</summary>
    public interface IDialogueOptionsView
    {
        void ShowOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen);
        void SetOptionTimeRemaining(float fraction01);
        void HideOptions();
    }

    /// <summary>
    /// 화자 자리(설계 88 §5.3) - 화자마다 첫 대사의 `#left`/`#right`로 자리가 정해지고 대화 끝까지 유지된다. 태그가 없거나 같은 자리를 두 화자가 쓰는 대본은
    /// 콘텐츠 검증이 막지만, 실행 중엔 멈추지 않도록 빈 자리(왼쪽 먼저)에 두거나 최근 화자로 덮어쓴다.
    /// </summary>
    public sealed class DialogueStageLayout
    {
        public string LeftSpeaker { get; private set; }
        public string RightSpeaker { get; private set; }

        /// <summary>말하는 자리. 서술 줄(화자 없음)은 null.</summary>
        public DialogueSide? Apply(DialogueLineData line)
        {
            var id = line.SpeakerId;
            if (string.IsNullOrEmpty(id)) return null;
            if (LeftSpeaker == id) return DialogueSide.Left;
            if (RightSpeaker == id) return DialogueSide.Right;

            DialogueSide side;
            if (line.HasTag(DialogueTags.Left)) side = DialogueSide.Left;
            else if (line.HasTag(DialogueTags.Right)) side = DialogueSide.Right;
            else side = LeftSpeaker == null ? DialogueSide.Left : DialogueSide.Right;

            if (side == DialogueSide.Left) LeftSpeaker = id;
            else RightSpeaker = id;
            return side;
        }

        public void Reset()
        {
            LeftSpeaker = null;
            RightSpeaker = null;
        }
    }
}

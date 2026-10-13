using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>Yarn 대본 태그(설계 87 §3.2, 88 §5.3, 89 §5.4). `#`는 Yarn이 떼고 넘긴다. 태그에는 공백을 넣을 수 없다(Yarn 문법).</summary>
    public static class DialogueTags
    {
        public const string Lock = "lock:";
        public const string Timeout = "timeout";
        public const string Ask = "ask";
        /// <summary>Yarn이 선택지 바로 앞 줄에 자동으로 붙이는 태그 - 작가가 쓰지 않는다. #ask와 같게 취급한다.</summary>
        public const string LastLine = "lastline";
        public const string Choice = "choice";
        public const string People = "people:";
        public const string Left = "left";
        public const string Right = "right";
    }

    /// <summary>표시할 줄 1개 - Yarn 타입을 숨긴 값. 화자 Id가 비면 서술 줄이다.</summary>
    public readonly struct DialogueLineData
    {
        public string SpeakerId { get; }
        public string Text { get; }
        public IReadOnlyList<string> Tags { get; }

        public DialogueLineData(string speakerId, string text, IReadOnlyList<string> tags)
        {
            SpeakerId = speakerId ?? string.Empty;
            Text = text ?? string.Empty;
            Tags = tags ?? Array.Empty<string>();
        }

        public bool HasTag(string tag)
        {
            foreach (var t in Tags) if (t == tag) return true;
            return false;
        }

        public bool TryGetTagValue(string prefix, out string value) => TagValue.TryGet(Tags, prefix, out value);
    }

    /// <summary>선택지 1개. Index는 Yarn이 준 순서(무응답 포함) - 화면에는 무응답을 빼고 보여 주지만 선택 결과는 이 Index로 돌려준다.</summary>
    public readonly struct DialogueOptionData
    {
        public int Index { get; }
        public string Text { get; }
        public bool IsAvailable { get; }
        public IReadOnlyList<string> Tags { get; }

        public DialogueOptionData(int index, string text, bool isAvailable, IReadOnlyList<string> tags)
        {
            Index = index;
            Text = text ?? string.Empty;
            IsAvailable = isAvailable;
            Tags = tags ?? Array.Empty<string>();
        }

        /// <summary>잠김 문구. Yarn 태그에 공백을 넣을 수 없어 대본은 `#lock:골드_300_필요`처럼 _로 쓰고, 표시할 때 공백으로 바꾼다.</summary>
        public string LockText => TagValue.TryGet(Tags, DialogueTags.Lock, out var v) ? v.Replace('_', ' ') : string.Empty;

        public bool IsTimeout
        {
            get
            {
                foreach (var t in Tags) if (t == DialogueTags.Timeout) return true;
                return false;
            }
        }
    }

    internal static class TagValue
    {
        public static bool TryGet(IReadOnlyList<string> tags, string prefix, out string value)
        {
            foreach (var t in tags)
            {
                if (t != null && t.StartsWith(prefix, StringComparison.Ordinal))
                {
                    value = t.Substring(prefix.Length).Trim();
                    return true;
                }
            }
            value = null;
            return false;
        }
    }

    /// <summary>대사 화면 계약(88 말풍선, 89 이벤트 대화 화면이 구현).</summary>
    public interface IDialogueView
    {
        void ShowLine(DialogueLineData line);
        void SetVisibleLength(int length);
        /// <summary>출력 완료 표시(▼ 등) - 출력이 끝난 순간 true, 새 줄에서 false.</summary>
        void SetTypingComplete(bool complete);
        void ShowOptions(IReadOnlyList<DialogueOptionData> visibleOptions, Action<int> onChosen);
        /// <summary>제한시간 남은 비율 1 → 0. 제한시간이 없으면 호출되지 않는다.</summary>
        void SetOptionTimeRemaining(float fraction01);
        void HideOptions();
        void Hide();
    }

    /// <summary>Yarn 연결 표시기가 부르는 쪽 - onChosen(-1) = 선택 없음(Yarn이 선택지 묶음을 건너뜀).</summary>
    public interface IDialoguePresentationTarget
    {
        void PresentLine(DialogueLineData line, Action onAdvance);
        void PresentOptions(IReadOnlyList<DialogueOptionData> options, Action<int> onChosen);
        void EndPresentation();
    }

    /// <summary>
    /// 대본 실행(설계 87 §7.1). Yarn 의존을 숨긴다 - 구현은 Game.Core.Yarn 어셈블리. onFinished(true) = 끝까지 진행(발생 기록 대상), false = 중단.
    /// </summary>
    public interface IDialogueScriptRunner
    {
        bool IsRunning { get; }
        bool HasNode(string node);
        bool TryRun(string node, IDialoguePresentationTarget target, Action<bool> onFinished);
        void Stop();
    }

    /// <summary>대본 명령·함수 등록(설계 87 §7.3) - 효과 명령·잠김 조건 함수는 89에서 등록한다.</summary>
    public interface IDialogueCommandRegistry
    {
        void AddCommand(string name, Delegate handler);
        void AddFunction(string name, Delegate implementation);
    }
}

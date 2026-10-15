using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>수동 걸기 목록의 원천 - 고용 상단원(상단주 제외, 설계 88 §4.1).</summary>
    public readonly struct CrewMember
    {
        public string CharacterId { get; }
        public string Name { get; }
        public CrewMember(string characterId, string name) { CharacterId = characterId; Name = name ?? characterId; }
    }

    /// <summary>수동 걸기 목록 한 줄. HasTalk=false면 흐림 + "할 말 없음", 선택 불가.</summary>
    public readonly struct ManualTalkMember
    {
        public string CharacterId { get; }
        public string Name { get; }
        public bool HasTalk { get; }
        public ManualTalkMember(string characterId, string name, bool hasTalk) { CharacterId = characterId; Name = name; HasTalk = hasTalk; }
    }

    /// <summary>Field 말풍선 화면(설계 88 §5) - 화면 아무 데나 클릭(빈 곳)을 진행자에게 알린다.</summary>
    public interface ITripDialogueView : IDialogueStageView
    {
        event Action Clicked;
    }

    /// <summary>
    /// 수동 걸기 화면(설계 88 §6). 버튼 활성은 두 조건의 곱이다 - 진행자 조건(대화 중·상행 정지 아님)은 SetTalkAllowed, 화면 전환·인카운터 조건은
    /// FieldUIController가 구현체에 직접 준다. 목록을 다시 누르면 닫는 동작은 화면이 스스로 처리한다.
    /// </summary>
    public interface IManualTalkView
    {
        event Action OpenRequested;
        event Action<string> MemberChosen;
        void SetTalkAllowed(bool allowed);
        void ShowMembers(IReadOnlyList<ManualTalkMember> members);
        void HideMembers();
    }

    public interface ITripDialogueStatus
    {
        bool IsRunning { get; }
        string CurrentDialogueId { get; }
        IReadOnlyList<DialogueTrigger> PendingTriggers { get; }
        /// <summary>전투 후 대화까지 남은 상행 시간. 음수 = 대기 없음.</summary>
        float AfterBattleSecondsRemaining { get; }
    }

    /// <summary>Field 로드마다 이번 씬의 화면을 진행자(Bootstrap 상주)에 연결한다 - FieldUIWiring만 쓴다.</summary>
    public interface ITripDialogueViewBinder
    {
        void BindViews(ITripDialogueView dialogueView, IManualTalkView manualTalkView);
    }

    /// <summary>디버그 전용 - 조건·횟수·화자 고용을 무시하고 즉시 시작한다(설계 88 §8).</summary>
    public interface ITripDialogueDebug : ITripDialogueStatus
    {
        bool DebugStart(string dialogueId);
    }
}

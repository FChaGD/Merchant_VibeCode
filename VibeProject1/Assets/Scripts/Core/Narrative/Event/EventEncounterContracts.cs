using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>선택 화면 표시 인물 1명(설계 89 §6.2) - Placeholder 색 사각형 + 이름, 반전 없음.</summary>
    public readonly struct ChoicePerson
    {
        public string Name { get; }
        public Color Color { get; }

        public ChoicePerson(string name, Color color)
        {
            Name = name;
            Color = color;
        }
    }

    /// <summary>선택 화면 한 번의 내용 - `#choice` 줄(설명문·`#people`)과 그 뒤 선택지(무응답 제외).</summary>
    public readonly struct EventChoiceContent
    {
        public string Description { get; }
        public IReadOnlyList<ChoicePerson> People { get; }
        public IReadOnlyList<DialogueOptionData> Options { get; }

        public EventChoiceContent(string description, IReadOnlyList<ChoicePerson> people, IReadOnlyList<DialogueOptionData> options)
        {
            Description = description ?? string.Empty;
            People = people ?? Array.Empty<ChoicePerson>();
            Options = options ?? Array.Empty<DialogueOptionData>();
        }
    }

    /// <summary>
    /// 이벤트 화면 묶음(설계 89 §6). 진행자(Bootstrap 상주)가 Field 씬 화면을 이 계약으로만 다룬다 - 패널 열기·닫기는 구현(FieldEventScreens)이 UIManager로
    /// 하고(패널 규칙), 진행자는 화면이 무엇인지 모른다. 이벤트 중 상단 버튼 비활성과 정비창 열람 모드도 함께 묶는다(시작·종료에 항상 같이 바뀐다).
    /// </summary>
    public interface IEventEncounterScreens
    {
        /// <summary>Field 화면이 아직 살아 있는지(씬 언로드 후 false).</summary>
        bool IsAvailable { get; }
        IDialogueStageView DialogueStage { get; }
        void ShowDialogue();
        void HideDialogue();
        void ShowChoice(EventChoiceContent content, Action<int> onChosen);
        void HideChoice();
        /// <summary>임시 보관 정리 화면(회수 적재 재사용). false = 화면 없음(완료 콜백 없음).</summary>
        bool TryShowCargoRecovery(Action onDone);
        void OpenFormationViewer();
        /// <summary>true = 상단 버튼(정비창·방향성·대화) 비활성 + 정비창 열람 모드. false = 복구.</summary>
        void SetEventControls(bool eventActive);
        event Action Clicked;
        event Action FormationViewRequested;
        /// <summary>이벤트 화면이 다시 열렸다(정비창을 닫아 모달 묶음이 되돌림 등).</summary>
        event Action Reshown;
    }

    /// <summary>이벤트 전투 결과(설계 89 §4.1 ⑤). 궤주·사망·포로는 전부 Defeat.</summary>
    public enum EventBattleOutcome { Victory, Flee, Defeat }

    /// <summary>이벤트가 전투를 요청하는 입구(설계 89 §4.2) - 랜덤 전투와 같은 인카운터 흐름으로 들어간다. 적 구성 Id 인자는 향후(§12).</summary>
    public interface IEventBattleRequester
    {
        void RequestEventBattle();
    }

    /// <summary>전투 마무리가 "이벤트" 정지가 남아 있을 때 결과를 넘기는 곳(설계 89 §4.2) - 전투 종료 신호 대신 이걸 부른다.</summary>
    public interface IEventBattleOutcomeSink
    {
        void OnEventBattleFinished(EventBattleOutcome outcome);
    }

    public interface IEventEncounterStatus
    {
        bool IsRunning { get; }
        string CurrentEventId { get; }
        /// <summary>
        /// 이벤트 실행이 시작됐다(이벤트 Id) - 퀘스트 확정 사건 발생 기록용(설계 107번 §9.3). 이벤트는 중단되지 않으므로 시작 = 발생으로 본다(설계 89 §4).
        /// </summary>
        event Action<string> EventStarted;
        /// <summary>정비창·방향성 지시가 닫히기를 기다리는 이벤트(없으면 null).</summary>
        string PendingEventId { get; }
        IReadOnlyList<EventEntry> QueuedEvents { get; }
    }

    /// <summary>디버그 전용 - 조건·횟수를 무시하고 시작한다. 정비창이 열려 있으면 평소처럼 대기(설계 89 §7).</summary>
    public interface IEventEncounterDebug : IEventEncounterStatus
    {
        bool DebugStart(string eventId);
    }

    /// <summary>Field 로드마다 이번 씬의 이벤트 화면을 진행자에 연결한다 - FieldUIWiring만 쓴다.</summary>
    public interface IEventEncounterViewBinder
    {
        void BindScreens(IEventEncounterScreens screens);
    }
}

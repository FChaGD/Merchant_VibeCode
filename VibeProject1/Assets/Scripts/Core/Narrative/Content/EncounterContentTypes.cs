using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum EventOccurrence { Random, Fixed }

    /// <summary>발생 장소(설계 109 §3.1) - 상행 이벤트와 숙소 휴식 이벤트는 후보 목록을 섞지 않는다.</summary>
    public enum EventPlace { Trip, Rest }

    /// <summary>장소 오류 문구용 이름 - 임포터(표 검사)와 검증기(대본 검사)가 같은 말로 알린다.</summary>
    public static class EventPlaceNames
    {
        public static string Of(EventPlace place) => place == EventPlace.Rest ? "휴식" : "상행";
    }

    /// <summary>확정 이벤트 대기열 우선순위(설계 87 §5.4) - 선언 순서가 곧 우선순위다(작을수록 먼저). 값 순서를 바꾸지 말 것.</summary>
    public enum EventImportance { Scenario, Core, Side, Normal }

    /// <summary>Once = 게임 전체 1회, PerTrip = 상행마다 1회까지(기획 86 §3.1).</summary>
    public enum EventRepeat { Once, PerTrip }

    public enum DialogueOccurrence { Random, Trigger, Manual, ManualRepeat }

    public enum DialogueTrigger { None, Departure, AfterBattle, AfterEvent, MidCityArrival }

    /// <summary>Encounter.xlsx Event 행 1개(설계 87 §3.1). 대사·분기·효과는 YarnNode가 가리키는 Yarn 노드에 있다.</summary>
    [Serializable]
    public sealed class EventEntry
    {
        public string Id;
        public string YarnNode;
        public EventOccurrence Occurrence;
        public EventPlace Place = EventPlace.Trip;
        /// <summary>EventStrings 시트의 제목 - 휴식 진행 화면 이벤트 기록 목록에 쓴다(설계 109 §3.1). 휴식 이벤트는 필수.</summary>
        public string Title = string.Empty;
        public EventImportance Importance = EventImportance.Normal;
        public EventRepeat Repeat = EventRepeat.PerTrip;
        public int Weight = 100;
        public List<EncounterCondition> Conditions = new();
    }

    /// <summary>Encounter.xlsx Dialogue 행 1개. 화자 열의 캐릭터가 전원 고용 상태여야 후보가 된다(외부 인물 제외, 설계 87 §5.2).</summary>
    [Serializable]
    public sealed class DialogueEntry
    {
        public string Id;
        public string YarnNode;
        public DialogueOccurrence Occurrence;
        public DialogueTrigger Trigger;
        public List<string> Speakers = new();
        public int Weight = 100;
        public List<EncounterCondition> Conditions = new();
    }

    /// <summary>외부 인물(기획 86 §4.1). 이름은 PersonStrings 시트에서 온다.</summary>
    [Serializable]
    public sealed class PersonEntry
    {
        public string Id;
        public string Name;
        public string IllustrationKey;
    }
}

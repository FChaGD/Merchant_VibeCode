using System;
using System.Collections.Generic;

namespace Game.Core
{
    public enum EventOccurrence { Random, Fixed }

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

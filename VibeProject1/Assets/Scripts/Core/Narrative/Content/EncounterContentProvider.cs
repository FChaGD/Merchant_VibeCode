using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>이벤트·대화 발생 정보 조회(설계 87 §3.1).</summary>
    public interface IEncounterContentReader
    {
        IReadOnlyList<EventEntry> Events { get; }
        IReadOnlyList<DialogueEntry> Dialogues { get; }
        IReadOnlyList<PersonEntry> People { get; }
        bool TryGetEvent(string id, out EventEntry entry);
        bool TryGetDialogue(string id, out DialogueEntry entry);
        bool TryGetPerson(string id, out PersonEntry entry);
    }

    /// <summary>
    /// Encounter 테이블 자산의 Bootstrap 상주 조회자. Id 조회는 등록 시 한 번 사전으로 만든다(매 판정마다 목록을 훑지 않음).
    /// 자산이 없으면(인스톨러 미실행) 빈 목록 - 이벤트·대화가 발생하지 않을 뿐 상행은 정상 진행한다.
    /// </summary>
    public class EncounterContentProvider : MonoBehaviour, IEncounterContentReader, IManagedComponent
    {
        [SerializeField] private EncounterTableAsset table;

        private readonly Dictionary<string, EventEntry> eventsById = new();
        private readonly Dictionary<string, DialogueEntry> dialoguesById = new();
        private readonly Dictionary<string, PersonEntry> peopleById = new();

        public IReadOnlyList<EventEntry> Events => table != null ? table.Events : Array.Empty<EventEntry>();
        public IReadOnlyList<DialogueEntry> Dialogues => table != null ? table.Dialogues : Array.Empty<DialogueEntry>();
        public IReadOnlyList<PersonEntry> People => table != null ? table.People : Array.Empty<PersonEntry>();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<IEncounterContentReader>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            eventsById.Clear();
            dialoguesById.Clear();
            peopleById.Clear();
            if (table == null)
            {
                Debug.LogWarning($"{nameof(EncounterContentProvider)}: Encounter 테이블 자산이 연결되지 않아 이벤트·대화가 발생하지 않는다(Play로 임포트 → Tools > Game > Build Bootstrap Scene).");
                return;
            }
            foreach (var e in table.Events) if (!string.IsNullOrEmpty(e.Id)) eventsById[e.Id] = e;
            foreach (var d in table.Dialogues) if (!string.IsNullOrEmpty(d.Id)) dialoguesById[d.Id] = d;
            foreach (var p in table.People) if (!string.IsNullOrEmpty(p.Id)) peopleById[p.Id] = p;
        }

        public bool TryGetEvent(string id, out EventEntry entry) => eventsById.TryGetValue(id ?? string.Empty, out entry);
        public bool TryGetDialogue(string id, out DialogueEntry entry) => dialoguesById.TryGetValue(id ?? string.Empty, out entry);
        public bool TryGetPerson(string id, out PersonEntry entry) => peopleById.TryGetValue(id ?? string.Empty, out entry);
    }
}

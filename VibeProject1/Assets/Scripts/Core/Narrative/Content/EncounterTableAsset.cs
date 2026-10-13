using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 이벤트·대화 발생 정보(설계 87 §3.1). EncounterTableImporter가 Assets/Table/Encounter/Encounter.xlsx에서 채운다 - 엑셀이 단일 진실 소스라
    /// 인스펙터 수정은 다음 임포트에 덮어써진다. 행 순서 = 테이블 순서(확정 이벤트 동시 성립·계기 대화 선택에 쓰인다).
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterTable", menuName = "Game/Table/Encounter Table")]
    public class EncounterTableAsset : ScriptableObject
    {
        [SerializeField] private List<EventEntry> events = new();
        [SerializeField] private List<DialogueEntry> dialogues = new();
        [SerializeField] private List<PersonEntry> people = new();

        public IReadOnlyList<EventEntry> Events => events;
        public IReadOnlyList<DialogueEntry> Dialogues => dialogues;
        public IReadOnlyList<PersonEntry> People => people;
    }
}

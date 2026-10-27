using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 퀘스트 정의(Docs/설계/107번 §4). QuestTableImporter가 Assets/Table/Quest/Quest.xlsx에서 채운다 - 엑셀이 단일 진실 소스라 인스펙터 수정은
    /// 다음 임포트에 덮어써진다. 행 순서 = 퀘스트 시트 순서(게시판 목록 순서).
    /// </summary>
    [CreateAssetMenu(fileName = "QuestTable", menuName = "Game/Table/Quest Table")]
    public class QuestTableAsset : ScriptableObject
    {
        [SerializeField] private List<QuestDefinition> quests = new();

        private Dictionary<string, QuestDefinition> byId;

        public IReadOnlyList<QuestDefinition> Quests => quests;

        public bool TryGet(string id, out QuestDefinition quest)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, QuestDefinition>(quests.Count);
                foreach (var q in quests)
                {
                    if (q?.Id != null) byId[q.Id] = q;
                }
            }

            quest = null;
            return id != null && byId.TryGetValue(id, out quest);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 임포터 전용. 정의가 목록 안에 목록을 품고 있어 다른 테이블처럼 SerializedObject로 필드를 하나씩 쓰면 정의 필드 목록이 임포터에 한 번 더
        /// 복제된다 - 직접 바꾸고 호출자가 SetDirty한다.
        /// </summary>
        public void ReplaceAll(List<QuestDefinition> definitions)
        {
            quests = definitions ?? new List<QuestDefinition>();
            byId = null;
        }
#endif
    }
}

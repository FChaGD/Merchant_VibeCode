using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 배틀 테스트 씬 전용 적 세팅 데이터 - BattleTestAllyRoster와 같은 자리(Entry가 클래스인 이유도
    /// 동일 - Id 기반 배치 취소/스탯 조절). 사전 세팅(적 구성 편집 패널, 타입별 개수 입력)과 전투 중
    /// 드래그 추가가 공유하는 단일 출처다.
    /// </summary>
    public class BattleTestEnemyRoster
    {
        public class Entry
        {
            public int Id { get; }
            public EnemyType Type { get; }
            public Vector2 Position { get; }
            public BattleUnitStats? StatsOverride { get; set; }

            public Entry(int id, EnemyType type, Vector2 position)
            {
                Id = id;
                Type = type;
                Position = position;
            }
        }

        private readonly List<Entry> entries = new();
        private int nextId;

        public IReadOnlyList<Entry> Entries => entries;

        public Entry Add(EnemyType type, Vector2 position)
        {
            var entry = new Entry(nextId++, type, position);
            entries.Add(entry);
            return entry;
        }

        public bool TryGet(int id, out Entry entry)
        {
            entry = entries.Find(e => e.Id == id);
            return entry != null;
        }

        public bool Remove(int id) => entries.RemoveAll(e => e.Id == id) > 0;

        public void Clear() => entries.Clear();
    }
}

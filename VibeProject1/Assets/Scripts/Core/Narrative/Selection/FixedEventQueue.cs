using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 확정 이벤트 대기열(설계 87 §5.4). 정렬 = ① 중요도(Scenario > Core > Side > Normal) ② 같은 중요도 안에서 먼저 성립한 것(판정 회차 순서)
    /// ③ 같은 회차 → 테이블 순서. 꺼낼 때 조건을 다시 검사해 깨진 것은 버린다 - 버려진 이벤트가 다시 성립하면 새 회차로 들어가 그 중요도의 맨 뒤에 선다.
    /// 판정은 5초 간격이라 같은 회차 안의 성립 순서는 구분할 수 없어 테이블 순서를 보조 기준으로 쓴다.
    /// </summary>
    public sealed class FixedEventQueue
    {
        private sealed class Item
        {
            public EventEntry Entry;
            public int TableIndex;
            public long Round;
        }

        private readonly List<Item> items = new();
        private long round;

        public int Count => items.Count;

        public IReadOnlyList<EventEntry> Snapshot()
        {
            var list = new List<EventEntry>(items.Count);
            foreach (var item in items) list.Add(item.Entry);
            return list;
        }

        public bool Contains(string id)
        {
            foreach (var item in items)
            {
                if (item.Entry.Id == id) return true;
            }
            return false;
        }

        /// <summary>이번 판정 회차에 성립한 확정 이벤트(테이블 순서)를 넣는다. 이미 대기 중인 것은 건너뛴다.</summary>
        public void EnqueueSatisfied(IReadOnlyList<(EventEntry entry, int tableIndex)> satisfiedInTableOrder)
        {
            round++;
            var added = false;
            foreach (var (entry, tableIndex) in satisfiedInTableOrder)
            {
                if (entry == null || Contains(entry.Id)) continue;
                items.Add(new Item { Entry = entry, TableIndex = tableIndex, Round = round });
                added = true;
            }
            if (added) items.Sort(Compare);
        }

        public bool TryDequeue(Func<EventEntry, bool> stillValid, out EventEntry entry)
        {
            while (items.Count > 0)
            {
                var head = items[0];
                items.RemoveAt(0);
                if (stillValid(head.Entry))
                {
                    entry = head.Entry;
                    return true;
                }
            }
            entry = null;
            return false;
        }

        public void Clear() => items.Clear();

        // 세 키가 항목마다 유일한 순서를 만들므로 List.Sort의 불안정성은 결과에 영향이 없다.
        private static int Compare(Item a, Item b)
        {
            var byImportance = a.Entry.Importance.CompareTo(b.Entry.Importance);
            if (byImportance != 0) return byImportance;
            var byRound = a.Round.CompareTo(b.Round);
            return byRound != 0 ? byRound : a.TableIndex.CompareTo(b.TableIndex);
        }
    }
}

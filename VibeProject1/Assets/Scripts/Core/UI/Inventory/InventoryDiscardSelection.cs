using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 선택 버리기에서 고른 아이템 목록(Docs/설계/102번 §4.5). 마차(섹션)를 바꿔도 선택이 유지되므로 화면이 아니라 Id로 기억하고,
    /// 경고 문구가 고른 순서를 따르도록 순서를 보존한다. 저장소가 바뀌면 사라진 아이템을 Prune으로 걸러 낸다.
    /// </summary>
    public sealed class InventoryDiscardSelection
    {
        private readonly List<string> ids = new();

        public int Count => ids.Count;
        public IReadOnlyList<string> Ids => ids;

        public bool Contains(string instanceId) => instanceId != null && ids.Contains(instanceId);

        /// <summary>선택을 뒤집는다. 선택된 상태가 되면 true.</summary>
        public bool Toggle(string instanceId)
        {
            if (instanceId == null) return false;
            if (ids.Remove(instanceId)) return false;
            ids.Add(instanceId);
            return true;
        }

        public void Clear() => ids.Clear();

        /// <summary>exists가 false인 Id를 뺀다. 빠진 것이 있으면 true.</summary>
        public bool Prune(Predicate<string> exists) => ids.RemoveAll(id => !exists(id)) > 0;
    }
}

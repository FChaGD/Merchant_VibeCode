using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 개체 Id → 현재 내구도(Docs/설계/91번 §3.2). 보유 목록 변경을 구독해 새 개체는 최대치로 등록하고 사라진 개체는 지운다 - 구매·시작 보유·
    /// 파괴 제거가 저마다 내구도를 챙기지 않아도 되게. 최대치는 저장하지 않고 조회 때마다 종류 정보에서 읽는다 - 테이블 값이 바뀌어도
    /// 새 최대치로 잘려 보이게(기획 90번 §4-5). MonoBehaviour와 분리한 이유는 Unity 밖에서 규칙을 검증하기 위해서다.
    /// </summary>
    public sealed class CaravanAssetDurabilityLedger : ICaravanAssetDurabilityReader, ICaravanAssetDurabilityWriter, IDisposable
    {
        private static readonly FormationUnitKind[] Kinds = { FormationUnitKind.Wagon, FormationUnitKind.Facility };

        private readonly IOwnedCaravanAssetReader owned;
        private readonly Dictionary<string, float> currentById = new();
        private readonly HashSet<string> ownedIds = new();
        private readonly List<string> staleIds = new();

        public event Action Changed;

        public CaravanAssetDurabilityLedger(IOwnedCaravanAssetReader owned)
        {
            this.owned = owned;
            owned.OnOwnedChanged += Sync;
            Sync();
        }

        public void Dispose() => owned.OnOwnedChanged -= Sync;

        public bool TryGet(string instanceId, out float current, out int max)
        {
            current = 0f;
            max = 0;
            if (instanceId == null || !currentById.TryGetValue(instanceId, out var stored) || !owned.TryGetOwned(instanceId, out var asset)) return false;

            max = asset.Profile.MaxDurability;
            current = Math.Min(stored, max);
            return true;
        }

        public bool TrySet(string instanceId, float value)
        {
            if (!TryGet(instanceId, out var current, out var max)) return false;

            var clamped = Math.Max(0f, Math.Min(value, max));
            // 저장값이 최대치보다 큰 채(최대치 축소) 같은 값을 쓰면 저장값만 맞추고 알리지 않는다 - 보이는 값은 그대로다.
            var visibleChange = clamped != current;
            currentById[instanceId] = clamped;
            if (visibleChange) Changed?.Invoke();
            return true;
        }

        public bool TryRestore(string instanceId) => TryGet(instanceId, out _, out var max) && TrySet(instanceId, max);

        private void Sync()
        {
            var changed = false;
            ownedIds.Clear();
            foreach (var kind in Kinds)
            {
                foreach (var id in owned.GetOwnedIds(kind))
                {
                    ownedIds.Add(id);
                    if (currentById.ContainsKey(id) || !owned.TryGetOwned(id, out var asset)) continue;
                    currentById[id] = asset.Profile.MaxDurability;
                    changed = true;
                }
            }

            staleIds.Clear();
            foreach (var id in currentById.Keys)
            {
                if (!ownedIds.Contains(id)) staleIds.Add(id);
            }
            foreach (var id in staleIds) currentById.Remove(id);

            if (changed || staleIds.Count > 0) Changed?.Invoke();
        }
    }
}

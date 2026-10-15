using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;

namespace Game.Core.Tests
{
    // 내구도 테스트(설계 91 §8) 공용 가짜 보유 목록 - 종류 정보는 프로필로 직접 만든다(카탈로그·MonoBehaviour 없이).
    internal sealed class FakeOwnedCaravanAssets : IOwnedCaravanAssetReader
    {
        private readonly List<(string id, FormationUnitKind kind)> order = new();
        private readonly Dictionary<string, CaravanAssetProfile> profiles = new();
        public event Action OnOwnedChanged;

        public void Add(string id, FormationUnitKind kind, int price = 1000, int max = 200)
        {
            order.Add((id, kind));
            profiles[id] = Profile(id, kind, price, max);
            OnOwnedChanged?.Invoke();
        }

        public void Remove(string id)
        {
            order.RemoveAll(e => e.id == id);
            profiles.Remove(id);
            OnOwnedChanged?.Invoke();
        }

        // 테이블 최대치 변경(데이터 수정) 흉내 - 보유 변경 이벤트는 내지 않는다.
        public void SetMax(string id, int max)
        {
            var p = profiles[id];
            profiles[id] = Profile(id, p.Kind, p.Price, max);
        }

        public IReadOnlyList<string> GetOwnedIds(FormationUnitKind kind) => order.Where(e => e.kind == kind).Select(e => e.id).ToList();

        public bool TryGetOwned(string instanceId, out OwnedCaravanAsset asset)
        {
            asset = default;
            if (instanceId == null || !profiles.TryGetValue(instanceId, out var profile)) return false;
            asset = new OwnedCaravanAsset(instanceId, profile, 1);
            return true;
        }

        private static CaravanAssetProfile Profile(string id, FormationUnitKind kind, int price, int max)
            => new(id, id + "이름", kind, kind == FormationUnitKind.Wagon ? "마차" : "시설", price, maxDurability: max);
    }

    internal sealed class FakeGoldSpender : IGoldSpender
    {
        public int Gold { get; private set; }
        public bool FailNextSpend { get; set; }
        public int OwnedGold => Gold;
        public event Action Changed;

        public FakeGoldSpender(int gold) => Gold = gold;

        public bool CanAfford(int amount) => amount <= Gold;
        public IReadOnlyList<string> PreviewWithdrawal(int amount) => Array.Empty<string>();

        public bool TrySpend(int amount)
        {
            if (FailNextSpend || amount > Gold) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void Refund(int amount)
        {
            Gold += amount;
            Changed?.Invoke();
        }
    }
}

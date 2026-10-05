using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 전투 한 번의 마차 화물 기록(Docs/기획/77번, 설계 79번 §3.2). 전투마다 새로 만들고, 전투 중에는 물품별 상태만 바꿀 뿐
    /// 저장소에 반영하지 않는다 - 반영은 결과 판정 뒤 정산이 한 번에 한다(§15-2). 난수를 Func로 받는 이유는 확률 분기를
    /// 고정값으로 검증하기 위해서다(실제는 UnityEngine.Random.value).
    /// 물품이 없는 마차·마차 없는 전투(배틀 테스트 씬 포함)에서는 아무 일도 하지 않는다(기획 77번 §4-23).
    /// </summary>
    public sealed class BattleCargoLedger
    {
        private readonly Func<float> random;
        private readonly List<Entry> entries = new();
        private readonly Dictionary<string, List<Entry>> byWagon = new();
        // 도난품을 가진 적의 사망/도주를 한 번만 구독하기 위한 기록 - 같은 적이 여러 번 훔쳐도 구독은 1회.
        private readonly HashSet<IBattleCombatant> subscribedThieves = new();
        // 루프가 매 틱 적 표적 후보에 더하는 살아 있는 목록(참조 공유) - 잔해가 생길 때 여기에 추가만 한다.
        private readonly List<IDamageable> wreckTargets = new();
        private readonly List<string> destroyedWagonIds = new();

        private sealed class Entry
        {
            public InventoryItemInstance Item;
            public CargoItemFate Fate;
            public IBattleCombatant Thief;
        }

        public IReadOnlyList<IDamageable> WreckTargets => wreckTargets;

        // items는 전투 시작 시점의 스냅샷으로 복사해 둔다 - 전투 중 저장소가 바뀌어도 이 전투의 기록은 시작 상태 기준이다.
        // 임시보관 물품은 마차에 실려 있지 않아 손실 대상이 아니다.
        public BattleCargoLedger(IEnumerable<InventoryItemInstance> items, Func<float> random)
        {
            this.random = random ?? throw new ArgumentNullException(nameof(random));
            if (items == null) return;

            foreach (var item in items)
            {
                if (item.IsStaged || string.IsNullOrEmpty(item.SectionId)) continue;

                var entry = new Entry { Item = item, Fate = CargoItemFate.Intact };
                entries.Add(entry);
                if (!byWagon.TryGetValue(item.SectionId, out var list))
                {
                    list = new List<Entry>();
                    byWagon.Add(item.SectionId, list);
                }
                list.Add(entry);
            }
        }

        // 시설·Id 없는 보호 대상은 화물이 없으므로 구독하지 않는다.
        public void RegisterWagon(BattleProtectedUnit wagon)
        {
            if (wagon == null || wagon.Kind != ProtectedUnitKind.Wagon || string.IsNullOrEmpty(wagon.UnitId)) return;

            // OnHitBy는 체력 감소 직후·파괴 판정 전에 발생한다 - 마지막 일격도 피격 판정을 먼저 받는다(기획 77번 §4-1).
            wagon.OnHitBy += HandleHit;
            wagon.OnDied += () => HandleDestroyed(wagon);
        }

        // 전투 종료 시점에 아직 Stolen인 물품(소유 적이 살아 있음)은 그대로 Stolen - 해석은 정산이 결과에 따라 한다.
        public BattleCargoReport BuildReport()
        {
            var records = new List<CargoItemRecord>(entries.Count);
            foreach (var entry in entries)
            {
                records.Add(new CargoItemRecord(entry.Item.InstanceId, entry.Item.SectionId, entry.Item.Definition, entry.Fate));
            }
            return new BattleCargoReport(records, new List<string>(destroyedWagonIds));
        }

        // 적재 물품이 있을 때만 1회 굴림 - 물품이 없으면 굴림 자체를 소비하지 않는다.
        private void HandleHit(BattleProtectedUnit wagon, IBattleCombatant attacker)
        {
            var intact = ItemsOf(wagon.UnitId, CargoItemFate.Intact);
            if (intact.Count == 0) return;

            var roll = random();
            if (roll < CargoLossTuning.HitBreakChance)
            {
                Pick(intact).Fate = CargoItemFate.Broken;
            }
            else if (roll < CargoLossTuning.HitBreakChance + CargoLossTuning.HitStealChance)
            {
                Steal(Pick(intact), attacker);
            }
        }

        // 파괴 순간엔 도난이 없다 - 남은 물품마다 파손 아니면 무방비. 무방비가 하나라도 있으면 잔해 표적을 만든다.
        private void HandleDestroyed(BattleProtectedUnit wagon)
        {
            destroyedWagonIds.Add(wagon.UnitId);
            foreach (var entry in ItemsOf(wagon.UnitId, CargoItemFate.Intact))
            {
                entry.Fate = random() < CargoLossTuning.DestroyBreakChance ? CargoItemFate.Broken : CargoItemFate.Unprotected;
            }
            if (HasUnprotected(wagon.UnitId))
            {
                wreckTargets.Add(new BattleWagonWreck(wagon, this));
            }
        }

        internal bool HasUnprotected(string wagonId)
        {
            if (wagonId == null || !byWagon.TryGetValue(wagonId, out var list)) return false;
            foreach (var entry in list)
            {
                if (entry.Fate == CargoItemFate.Unprotected) return true;
            }
            return false;
        }

        internal bool TryStealFromWreck(string wagonId, IBattleCombatant attacker)
        {
            var unprotected = ItemsOf(wagonId, CargoItemFate.Unprotected);
            if (unprotected.Count == 0 || random() >= CargoLossTuning.WreckStealChance) return false;

            Steal(Pick(unprotected), attacker);
            return true;
        }

        private void Steal(Entry entry, IBattleCombatant thief)
        {
            entry.Fate = CargoItemFate.Stolen;
            entry.Thief = thief;
            if (thief == null || !subscribedThieves.Add(thief)) return;

            // 소유 적이 사망하면 그 적의 도난품은 전부 회수 대상, 도주하면 되찾을 수 없다.
            thief.OnDied += () => Resolve(thief, CargoItemFate.Recoverable);
            thief.OnFled += () => Resolve(thief, CargoItemFate.StolenLost);
        }

        private void Resolve(IBattleCombatant thief, CargoItemFate fate)
        {
            foreach (var entry in entries)
            {
                if (entry.Thief == thief && entry.Fate == CargoItemFate.Stolen) entry.Fate = fate;
            }
        }

        private Entry Pick(List<Entry> candidates)
        {
            var count = candidates.Count;
            var index = Mathf.Clamp((int)(random() * count), 0, count - 1);
            return candidates[index];
        }

        private List<Entry> ItemsOf(string wagonId, CargoItemFate fate)
        {
            var result = new List<Entry>();
            if (wagonId == null || !byWagon.TryGetValue(wagonId, out var list)) return result;

            foreach (var entry in list)
            {
                if (entry.Fate == fate) result.Add(entry);
            }
            return result;
        }
    }
}

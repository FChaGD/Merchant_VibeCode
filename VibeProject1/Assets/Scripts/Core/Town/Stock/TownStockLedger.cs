using System;
using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마을별 남은 수량(Docs/설계/81번 §3.4)의 순수 로직. 마을을 처음 조회할 때 테이블 행을 복사해 초기화한다 - 앱 실행 중 줄어든 값을
    /// 유지하고(기획 80번 §3-9), 방문하지 않은 마을은 만들 필요가 없다. isSellable은 마을 규모상 그 Category의 판매 시설이 있는지를
    /// 묻는다 - 규모별 시설 배정이 엑셀이 아니라 Bootstrap 인스펙터에 있어 임포터가 걸러낼 수 없기 때문이다(§3.4). null이면 전부 판매.
    /// </summary>
    public sealed class TownStockLedger
    {
        private static readonly IReadOnlyList<TownStockLine> Empty = Array.Empty<TownStockLine>();

        private readonly IReadOnlyList<TownStockEntry> source;
        private readonly Func<int, TownStockCategory, bool> isSellable;
        private readonly Dictionary<(int cityId, TownStockCategory category), List<TownStockLine>> lines = new();

        public TownStockLedger(IReadOnlyList<TownStockEntry> source, Func<int, TownStockCategory, bool> isSellable)
        {
            this.source = source ?? Array.Empty<TownStockEntry>();
            this.isSellable = isSellable;
        }

        public IReadOnlyList<TownStockLine> GetLines(int cityId, TownStockCategory category) => GetOrCreate(cityId, category) ?? Empty;

        public bool TryConsume(int cityId, TownStockCategory category, string itemId)
        {
            var list = GetOrCreate(cityId, category);
            if (list == null) return false;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].ItemId != itemId || list[i].Remaining <= 0) continue;
                list[i] = new TownStockLine(itemId, list[i].Remaining - 1);
                return true;
            }
            return false;
        }

        // 판매 불가로 판정된 조합은 빈 목록을 저장해 판정을 다시 하지 않는다(경고 1회).
        private List<TownStockLine> GetOrCreate(int cityId, TownStockCategory category)
        {
            var key = (cityId, category);
            if (lines.TryGetValue(key, out var existing)) return existing;

            var created = new List<TownStockLine>();
            var sellable = isSellable == null || isSellable(cityId, category);
            if (sellable)
            {
                foreach (var entry in source)
                {
                    if (entry.CityId == cityId && entry.Category == category) created.Add(new TownStockLine(entry.ItemId, entry.Quantity));
                }
            }
            lines[key] = created;
            return created;
        }
    }
}

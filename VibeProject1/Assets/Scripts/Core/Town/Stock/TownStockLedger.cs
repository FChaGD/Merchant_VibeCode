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
        // 빈 목록만으로는 "판매 불가"와 "행이 없음"을 구분할 수 없어 반환(매각) 판정용으로 따로 기억한다.
        private readonly HashSet<(int cityId, TownStockCategory category)> unsellable = new();

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

        // 매각 반환(기획 80번 §3-6 예외) - 같은 종류 행이 있으면 +1, 없으면 끝에 수량 1로 추가한다.
        public bool TryReturn(int cityId, TownStockCategory category, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return false;
            var list = GetOrCreate(cityId, category);
            if (unsellable.Contains((cityId, category))) return false;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].ItemId != itemId) continue;
                list[i] = new TownStockLine(itemId, list[i].Remaining + 1);
                return true;
            }
            list.Add(new TownStockLine(itemId, 1));
            return true;
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
            else
            {
                unsellable.Add(key);
            }
            lines[key] = created;
            return created;
        }
    }
}

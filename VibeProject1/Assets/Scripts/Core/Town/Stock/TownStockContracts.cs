using System;
using System.Collections.Generic;

namespace Game.Core
{
    public readonly struct TownStockLine
    {
        public string ItemId { get; }
        public int Remaining { get; }

        public TownStockLine(string itemId, int remaining)
        {
            ItemId = itemId;
            Remaining = remaining;
        }
    }

    /// <summary>마을 재고 읽기(Docs/설계/81번 §3.4). 남은 수량 0인 행도 돌려준다 - 무역품 화면이 품절 행을 남겨 둔다(기획 80번 §3-5).</summary>
    public interface ITownStockReader
    {
        IReadOnlyList<TownStockLine> GetLines(int cityId, TownStockCategory category);
        event Action OnStockChanged;
    }

    /// <summary>
    /// 재고 차감 전용(ISP). 구매 서비스만 쓴다. 늘리는 연산이 없는 이유: 파손·파괴로 보유가 줄어도 마을 재고는 늘지 않고(기획 80번 §3-6),
    /// 재입고는 범위 밖이다(§3-7).
    /// </summary>
    public interface ITownStockConsumer
    {
        bool TryConsume(int cityId, TownStockCategory category, string itemId);
    }
}

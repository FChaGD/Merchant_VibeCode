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
    /// 재고 차감 전용(ISP). 구매 서비스만 쓴다. 늘리는 연산은 매각 전용 계약(ITownStockReturner)으로 따로 뒀다 - 파손·파괴로는
    /// 마을 재고가 늘지 않고 매각만 예외다(기획 80번 §3-6 개정).
    /// </summary>
    public interface ITownStockConsumer
    {
        bool TryConsume(int cityId, TownStockCategory category, string itemId);
    }

    /// <summary>
    /// 재고 반환 전용(Docs/설계/104번 §4) - 마구간 매각만 쓴다. 판 마을에 그 종류 행이 없으면 새로 만든다(기획 80번 §3-6 매각 예외).
    /// </summary>
    public interface ITownStockReturner
    {
        /// <summary>판매 불가 마을(그 분류의 시설이 없음)이거나 Id가 비었으면 false.</summary>
        bool TryReturn(int cityId, TownStockCategory category, string itemId);
    }
}

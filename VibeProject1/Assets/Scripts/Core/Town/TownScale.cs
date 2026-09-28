using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 마을 규모 한 단계(Docs/기획/57번, 설계 58번 §5.1). 규모를 enum으로 두지 않고 "순서가 곧 등급인 목록"의 한 항목으로 둔다 -
    /// 규모 단계 추가(예: 소도시)가 목록에 항목 하나를 끼우는 것으로 끝나게 하기 위해서다. Rank는 목록 위치(0 = 가장 작은 규모).
    /// </summary>
    public readonly struct TownScale
    {
        public string Id { get; }
        public string Label { get; }
        public int Rank { get; }

        public TownScale(string id, string label, int rank)
        {
            Id = id;
            Label = label;
            Rank = rank;
        }
    }

    /// <summary>
    /// 마을 Id → 규모 조회(설계 58번 §5.4). 시설 제공 여부 외에 규모를 쓰는 소비자(향후 판매 품목·용병 후보 수 등)를 위해 연다.
    /// </summary>
    public interface ITownScaleReader
    {
        IReadOnlyList<TownScale> Scales { get; }
        TownScale GetScale(int cityId);
    }
}

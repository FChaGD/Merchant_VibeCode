using System.Collections.Generic;

namespace Game.Core
{
    /// <summary>
    /// 규모 목록과 시설별 최소 규모의 기본값(Docs/기획/57번 §4.1 확정값). 실제 값은 Bootstrap의
    /// TownScaleFacilityAvailabilityProvider 인스펙터가 가지며, 인스톨러가 이 기본값으로 빈 항목만 채운다 - 사용자가 바꾼 값은 유지된다.
    /// 규모를 추가할 때는 Scales에 크기 순서대로 끼우고, 새 규모에서 처음 열리는 시설의 최소 규모만 바꾸면 된다.
    /// </summary>
    public static class TownScaleDefaults
    {
        public const string Village = "Village";
        public const string City = "City";
        public const string Metropolis = "Metropolis";

        // 작은 규모 → 큰 규모 순서(포함 관계: 촌락 ⊂ 도시 ⊂ 대도시, 기획 57번 §3).
        public static readonly (string Id, string Label)[] Scales =
        {
            (Village, "촌락"),
            (City, "도시"),
            (Metropolis, "대도시"),
        };

        public static readonly IReadOnlyDictionary<string, string> FacilityMinScale = new Dictionary<string, string>
        {
            [TownFacilityIds.TradeGoodsMarket] = Village,
            [TownFacilityIds.SpecialtyMarket] = Metropolis, // "토산품" 근거로 낮은 규모에 여는 것은 확장으로만 염두(기획 57번 §4.1)
            [TownFacilityIds.Pawnshop] = City,
            [TownFacilityIds.Inn] = Village,
            [TownFacilityIds.MercenaryContact] = City,
            [TownFacilityIds.Tavern] = City,
            [TownFacilityIds.GeneralStore] = Village,
            [TownFacilityIds.Stable] = City,
            [TownFacilityIds.Blacksmith] = City,
            [TownFacilityIds.CityHall] = Metropolis,
            [TownFacilityIds.MerchantGuild] = Metropolis,
        };
    }
}

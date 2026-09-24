using System.Collections.Generic;
using System.Linq;

namespace Game.Core
{
    /// <summary>
    /// 카테고리→시설 소속, 표시 순서(위→아래), 한글 라벨의 단일 출처(Docs/기획/24번 §3.1·§3.2 확정값).
    /// 시설별 기획이 진행돼 라벨 외 데이터가 생기면 엑셀 테이블로 옮기는 대상이다 - 지금은 라벨뿐이라
    /// 테이블/임포터를 만들지 않았다(Docs/설계/37번 §2).
    /// </summary>
    public static class TownFacilityCatalog
    {
        private sealed class Category
        {
            public readonly string Id;
            public readonly string Label;
            public readonly (string Id, string Label)[] Facilities;

            public Category(string id, string label, params (string Id, string Label)[] facilities)
            {
                Id = id;
                Label = label;
                Facilities = facilities;
            }
        }

        private static readonly Category[] Categories =
        {
            new(TownFacilityCategoryIds.MarketDistrict, "시장 지구",
                (TownFacilityIds.TradeGoodsMarket, "무역품 구매"),
                (TownFacilityIds.SpecialtyMarket, "특산물 구매"),
                (TownFacilityIds.Pawnshop, "전당포")),
            new(TownFacilityCategoryIds.TavernDistrict, "주점 지구",
                (TownFacilityIds.Inn, "숙소"),
                (TownFacilityIds.MercenaryContact, "용병단 접촉"),
                (TownFacilityIds.Tavern, "주점")),
            new(TownFacilityCategoryIds.IndustrialDistrict, "공업 지구",
                (TownFacilityIds.Blacksmith, "대장간"),
                (TownFacilityIds.Stable, "마구간"),
                (TownFacilityIds.GeneralStore, "잡화 상점")),
            new(TownFacilityCategoryIds.CityHallQuarter, "시청 방면",
                (TownFacilityIds.CityHall, "시청"),
                (TownFacilityIds.MerchantGuild, "상인조합")),
        };

        private static readonly Dictionary<string, Category> CategoriesById = Categories.ToDictionary(c => c.Id);
        private static readonly Dictionary<string, string> FacilityLabelsById =
            Categories.SelectMany(c => c.Facilities).ToDictionary(f => f.Id, f => f.Label);

        public static readonly IReadOnlyList<string> CategoryIds = Categories.Select(c => c.Id).ToArray();
        public static readonly IReadOnlyList<string> AllFacilityIds = Categories.SelectMany(c => c.Facilities).Select(f => f.Id).ToArray();

        public static IReadOnlyList<string> GetFacilityIds(string categoryId) =>
            CategoriesById.TryGetValue(categoryId, out var category)
                ? category.Facilities.Select(f => f.Id).ToArray()
                : System.Array.Empty<string>();

        public static string GetCategoryLabel(string categoryId) =>
            CategoriesById.TryGetValue(categoryId, out var category) ? category.Label : categoryId;

        public static string GetFacilityLabel(string facilityId) =>
            FacilityLabelsById.TryGetValue(facilityId, out var label) ? label : facilityId;
    }
}

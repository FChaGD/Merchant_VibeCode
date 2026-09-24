namespace Game.Core
{
    /// <summary>
    /// 마을 시설 카테고리 식별자(Docs/기획/24번 §3.1). enum이 아니라 문자열인 이유는 기존 데이터 테이블
    /// Id 체계 전환 방침(Docs/설계/36번)과 맞추기 위해서다 - 마을별 시설 데이터가 테이블로 옮겨질 때
    /// 코드 변경 없이 그대로 Id로 쓰인다.
    /// </summary>
    public static class TownFacilityCategoryIds
    {
        public const string MarketDistrict = "MarketDistrict";
        public const string TavernDistrict = "TavernDistrict";
        public const string IndustrialDistrict = "IndustrialDistrict";
        public const string CityHallQuarter = "CityHallQuarter";
    }
}

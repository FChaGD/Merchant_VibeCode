namespace Game.Core
{
    /// <summary>
    /// 마을 카테고리 depth에서 UIElementMarker.Id로 사용하는 문자열. 시설 버튼 Id는 시설 수만큼 상수를
    /// 복제하지 않고 시설 Id로 조립한다 - 매직스트링 조립 규칙이 이 한 곳에만 있다.
    /// </summary>
    public static class TownUIElementIds
    {
        public const string CategoryDepthRoot = "Town.CategoryDepth";
        public const string BackButton = "Town.BackButton";

        public static string FacilityButton(string facilityId) => $"Town.Facility.{facilityId}";
    }
}

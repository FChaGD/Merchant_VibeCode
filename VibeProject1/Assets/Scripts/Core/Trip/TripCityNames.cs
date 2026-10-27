namespace Game.Core
{
    /// <summary>
    /// 도시 표시 이름(Docs/Refactor/2026-10-26_씬전용.md I-1, SE-4) - 이름이 없는 도시(갓 배치한 디버그 도시 등)·지도에 없는 도시·지도 자체가 없을 때의
    /// 대체 규칙을 한 곳에 둔다. 상행 준비 지도와 Field 구간 도착 알림이 같은 규칙을 각자 갖고 있었다.
    /// </summary>
    public static class TripCityNames
    {
        public static string DisplayName(IWorldMapReader map, int cityId)
            => map != null && map.TryGetCity(cityId, out var city) && !string.IsNullOrEmpty(city.Name) ? city.Name : Fallback(cityId);

        public static string Fallback(int cityId) => $"디버그 도시 {cityId}";
    }
}

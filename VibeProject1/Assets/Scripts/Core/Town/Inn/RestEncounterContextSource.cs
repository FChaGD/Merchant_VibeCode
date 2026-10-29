namespace Game.Core
{
    /// <summary>
    /// 휴식 이벤트 조건 판정 문맥(설계 109 §3.2) - 체류 도시·그 지역·오늘 날짜·이력을 판정 순간 값으로 만든다. 현재 위치는 쓰기 타입으로만 등록되어 있어
    /// ITripCurrentLocationRepository로 조회한다. 의존성은 전부 선택적 - 없으면 체류 도시 없음(`town`·`region` 거짓).
    /// </summary>
    public sealed class RestEncounterContextSource
    {
        private readonly IDateReader date;
        private readonly ITripCurrentLocationRepository location;
        private readonly IWorldMapReader worldMap;
        private readonly IEncounterHistoryReader history;

        public RestEncounterContextSource(IDependencyResolver registrar)
        {
            registrar.TryResolve(out date);
            registrar.TryResolve(out location);
            registrar.TryResolve(out worldMap);
            registrar.TryResolve(out history);
        }

        public EncounterConditionContext Build()
            => EncounterConditionContextFactory.CreateForTown(date != null ? date.Today : default, location != null ? location.CurrentCityId : 0, RegionOf, history);

        private int RegionOf(int cityId) => worldMap != null && worldMap.TryGetCity(cityId, out var city) ? city.RegionId : -1;
    }
}

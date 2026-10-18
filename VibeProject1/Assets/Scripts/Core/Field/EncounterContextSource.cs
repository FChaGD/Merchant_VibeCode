namespace Game.Core
{
    /// <summary>
    /// 이벤트·대화 조건 판정 문맥(설계 87 §5.1)을 판정 순간 값으로 만든다. 대화 진행자(88)와 이벤트 진행자(89)가 같은 문맥을 써야 같은 조건이 같게
    /// 판정되므로 한 곳에 둔다. 의존성은 전부 선택적 - 없으면 기본값.
    /// </summary>
    public sealed class EncounterContextSource
    {
        private readonly IDateReader date;
        private readonly ITripItinerary itinerary;
        private readonly IWorldMapReader worldMap;
        private readonly ISessionState session;
        private readonly IEncounterHistoryReader history;

        public EncounterContextSource(IDependencyResolver registrar)
        {
            registrar.TryResolve(out date);
            registrar.TryResolve(out itinerary);
            registrar.TryResolve(out worldMap);
            registrar.TryResolve(out session);
            registrar.TryResolve(out history);
        }

        public EncounterConditionContext Build()
        {
            var hasLeg = itinerary != null && itinerary.HasItinerary;
            var today = date != null ? date.Today : default;
            return EncounterConditionContextFactory.Create(today, hasLeg, hasLeg ? itinerary.CurrentLeg : default, RegionOf, session != null ? session.Progress : 0f, history);
        }

        private int RegionOf(int cityId) => worldMap != null && worldMap.TryGetCity(cityId, out var city) ? city.RegionId : -1;
    }
}

using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 여정 상태(Docs/설계/76번 §5). "상행 시작" 때 계획을 세워 저장하고, Field의 구간 진행이 현재 구간을 읽어 간다. Bootstrap 상주라
    /// Hub → Field 씬 전환을 넘어 여정이 유지된다. 이동 수치(TripTravelSettings)도 여기 인스펙터로 둔다 - 계획(ITripPlanner)·인카운터 빈도
    /// (IEncounterRateSource)가 모두 이 수치를 쓰므로 한곳에 둔다.
    /// 여정이 없을 때(Field에서 바로 Play 등)는 보통 등급 빈도를 내놓는다.
    /// </summary>
    public class TripItineraryState : MonoBehaviour, ITripItinerary, ITripDeparture, ITripPlanner, IEncounterRateSource, IManagedComponent
    {
        [SerializeField] private TripTravelSettings settings = TripTravelSettings.Default();

        private TripRoutePlanner planner;
        private TripPlan plan;
        private int currentLegIndex;

        public TripTravelSettings Settings => settings != null && settings.IsValid ? settings : TripTravelSettings.Default();

        public bool HasItinerary => plan != null && plan.Legs.Count > 0;
        public int LegCount => plan?.Legs.Count ?? 0;
        public int CurrentLegIndex => currentLegIndex;
        public TripLeg CurrentLeg => HasItinerary ? plan.Legs[currentLegIndex] : default;
        public bool HasNextLeg => HasItinerary && currentLegIndex < plan.Legs.Count - 1;

        public float EncountersPerFiveMinutes => HasItinerary
            ? CurrentLeg.EncountersPerFiveMinutes
            : TripTravelRules.ResolveGrade(Settings, TripTravelSettings.DefaultRoadDifficulty).EncountersPerFiveMinutes;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITripItinerary>(this);
            registrar.Register<ITripDeparture>(this);
            registrar.Register<IEncounterRateSource>(this);
            // 계획기는 지도 조회 뒤(ResolveDependencies)에 만들어지므로 이 컴포넌트가 대신 등록하고 위임한다 - 정보 패널과 실제 상행이
            // 같은 이동 수치로 계산해야 표시 시간과 실제 시간이 같다.
            registrar.Register<ITripPlanner>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            if (!registrar.TryResolve<IWorldMapReader>(out var worldMap))
            {
                Debug.LogWarning($"{nameof(TripItineraryState)}: 월드 지도가 없어 상행 계획을 세울 수 없다(Tools > Game > Build Bootstrap Scene).");
                return;
            }
            registrar.TryResolve<IRoadDifficultyReader>(out var difficulties);
            planner = new TripRoutePlanner(worldMap, difficulties ?? new BaseDifficultyReader(worldMap), () => Settings);
        }

        public bool TryPlan(int fromCityId, int toCityId, out TripPlan newPlan)
        {
            newPlan = null;
            return planner != null && planner.TryPlan(fromCityId, toCityId, out newPlan);
        }

        public bool TryDepart(int fromCityId, int toCityId)
        {
            Clear();
            if (!TryPlan(fromCityId, toCityId, out var newPlan)) return false;
            plan = newPlan;
            return true;
        }

        public void AdvanceLeg()
        {
            if (HasNextLeg) currentLegIndex++;
        }

        public void Clear()
        {
            plan = null;
            currentLegIndex = 0;
        }

        // 도로 난이도 상태가 등록되지 않은 경우(인스톨러 미실행) 지도 기본값만 쓴다.
        private sealed class BaseDifficultyReader : IRoadDifficultyReader
        {
            private readonly IWorldMapReader map;
            public BaseDifficultyReader(IWorldMapReader map) => this.map = map;
            public int GetDifficulty(RoadKey road) => map.GetRoadBaseDifficulty(road);
        }
    }
}

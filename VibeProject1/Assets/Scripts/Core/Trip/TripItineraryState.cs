using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행 여정 상태(Docs/설계/76번 §5). "상행 시작" 때 계획을 세워 저장하고, Field의 구간 진행이 현재 구간을 읽어 간다. Bootstrap 상주라
    /// Hub → Field 씬 전환을 넘어 여정이 유지된다. 이동 수치(TripTravelSettings)도 여기 인스펙터로 둔다 - 계획(ITripPlanner)·인카운터 빈도
    /// (IEncounterRateSource)가 모두 이 수치를 쓰므로 한곳에 둔다.
    /// 현재 구간 실행 값(CurrentLegRuntime)은 출발 계획과 따로 둔다 - 이벤트로 도로 난이도가 바뀌면 빈도·등급·남은 시간만 다시 계산하고 계획(예상 일수
    /// 포함)은 그대로 둔다(설계 89 §4.3).
    /// 여정이 없을 때(Field에서 바로 Play 등)는 보통 등급 빈도를 내놓는다.
    /// </summary>
    public class TripItineraryState : MonoBehaviour, ITripItinerary, ITripDeparture, ITripPlanner, IEncounterRateSource, ITripTravelSettingsReader, ICurrentLegDifficulty, IManagedComponent
    {
        [SerializeField] private TripTravelSettings settings = TripTravelSettings.Default();

        private TripRoutePlanner planner;
        private TripPlan plan;
        private int currentLegIndex;
        private CurrentLegRuntime runtime;
        private ICityPairDifficultyReader pairs;
        private ITripClockAdjuster clock; // 선택적 - 없으면 빈도만 바뀌고 시간은 그대로

        public event Action Changed;

        public TripTravelSettings Settings => settings != null && settings.IsValid ? settings : TripTravelSettings.Default();

        public bool HasItinerary => plan != null && plan.Legs.Count > 0;
        public int LegCount => plan?.Legs.Count ?? 0;
        public int CurrentLegIndex => currentLegIndex;
        public TripLeg CurrentLeg => HasItinerary ? plan.Legs[currentLegIndex] : default;
        public bool HasNextLeg => HasItinerary && currentLegIndex < plan.Legs.Count - 1;

        public float EncountersPerFiveMinutes => HasLeg
            ? runtime.EncountersPerFiveMinutes
            : TripTravelRules.ResolveGrade(Settings, TripTravelSettings.DefaultRoadDifficulty).EncountersPerFiveMinutes;

        public bool HasLeg => runtime != null && runtime.HasLeg;
        public CityPairKey CurrentPair => HasLeg ? runtime.Pair : default;
        public int CurrentBaseDifficulty => HasLeg ? runtime.Leg.BaseDifficulty : TripTravelSettings.DefaultRoadDifficulty;
        public int CurrentDifficulty => HasLeg ? runtime.Difficulty : TripTravelSettings.DefaultRoadDifficulty;
        public int LegOnlyChange => HasLeg ? runtime.LegOnlyChange : 0;
        public string CurrentGradeName => HasLeg ? runtime.GradeName : string.Empty;
        public float CurrentBaseDurationSeconds => HasLeg ? runtime.BaseDurationSeconds : CurrentLeg.DurationSeconds;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITripItinerary>(this);
            registrar.Register<ITripDeparture>(this);
            registrar.Register<IEncounterRateSource>(this);
            // 계획기는 지도 조회 뒤(ResolveDependencies)에 만들어지므로 이 컴포넌트가 대신 등록하고 위임한다 - 정보 패널과 실제 상행이
            // 같은 이동 수치로 계산해야 표시 시간과 실제 시간이 같다.
            registrar.Register<ITripPlanner>(this);
            // 상행 날짜 시계(설계 93 §4.3)가 같은 이동 수치로 초→일 환산을 하도록 조회 계약을 따로 등록한다.
            registrar.Register<ITripTravelSettingsReader>(this);
            registrar.Register<ICurrentLegDifficulty>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            // 실행 값은 지도가 없어도 만든다 - 구간이 없으면 아무것도 바꾸지 않는다.
            if (pairs != null) pairs.Changed -= HandlePairChanged;
            registrar.TryResolve(out pairs);
            registrar.TryResolve(out clock);
            runtime = new CurrentLegRuntime(() => Settings, pairs);
            if (pairs != null) pairs.Changed += HandlePairChanged;

            if (!registrar.TryResolve<IWorldMapReader>(out var worldMap))
            {
                Debug.LogWarning($"{nameof(TripItineraryState)}: 월드 지도가 없어 상행 계획을 세울 수 없다(Tools > Game > Build Bootstrap Scene).");
                return;
            }
            registrar.TryResolve<IRoadDifficultyReader>(out var difficulties);
            planner = new TripRoutePlanner(worldMap, difficulties ?? new BaseDifficultyReader(worldMap), () => Settings, pairs);
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
            ResetRuntime();
            return true;
        }

        public void AdvanceLeg()
        {
            if (!HasNextLeg) return;
            currentLegIndex++;
            ResetRuntime();
        }

        public void Clear()
        {
            plan = null;
            currentLegIndex = 0;
            runtime?.Clear();
        }

        public void AdjustCurrentLeg(int change)
        {
            if (runtime != null && runtime.AdjustLegOnly(change)) ApplyRuntimeChange();
        }

        private void ResetRuntime()
        {
            if (runtime != null && HasItinerary) runtime.Reset(CurrentLeg);
        }

        private void HandlePairChanged(CityPairKey pair)
        {
            if (!HasLeg || !runtime.Pair.Equals(pair)) return;
            if (runtime.Recompute()) ApplyRuntimeChange();
        }

        // 진행 비율 유지·추가 시간 보존은 시계가 맡는다(TripLegClock.Rebase).
        private void ApplyRuntimeChange()
        {
            clock?.RebaseDuration(runtime.BaseDurationSeconds);
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            if (pairs != null) pairs.Changed -= HandlePairChanged;
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

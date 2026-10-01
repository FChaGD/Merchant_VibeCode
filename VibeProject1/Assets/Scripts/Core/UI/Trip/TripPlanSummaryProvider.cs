using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 상행정보 패널 문구(Docs/설계/76번 §7.1) - 현재 위치 → 도착지 계획을 구간별로 보여 준다. PlaceholderTripInfoProvider를 대체한다.
    /// 위험도는 구간별 등급 이름만 보여 준다 - 수치·예상 전투 횟수는 숨긴다(기획 75번 §4-12). 보상은 아직 없어 "값 없음"(기획 75번 §6).
    /// 계획은 표시할 때마다 새로 세운다 - 도착지를 고를 때만 불리고 노드가 수십 개라 비용이 작다.
    /// </summary>
    public class TripPlanSummaryProvider : MonoBehaviour, ITripInfoProvider, IManagedComponent
    {
        private const string MissingText = "값 없음";
        private const string LegSeparator = " → ";

        private ITripPlanner planner;
        private ITripCurrentLocationReader currentLocation;
        private ITripDestinationReader destination;

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITripInfoProvider>(this);
        }

        public void ResolveDependencies(IDependencyResolver registrar)
        {
            registrar.TryResolve(out planner);
            // 읽기 전용 타입은 DI에 등록되지 않는다 - 저장소는 쓰기 타입으로만 등록되므로 그 타입으로 조회해 읽기 전용 필드에 담는다
            // (HubUIWiring과 같은 규칙). 읽기 타입으로 조회하면 조용히 실패해 패널이 늘 "값 없음"이 됐다(2026-10-01 검증).
            if (registrar.TryResolve<ITripCurrentLocationRepository>(out var currentLocationRepository)) currentLocation = currentLocationRepository;
            if (registrar.TryResolve<ITripDestinationAssigner>(out var destinationAssigner)) destination = destinationAssigner;
        }

        public TripSummary GetTripSummary()
        {
            if (planner == null || currentLocation == null || destination?.DestinationCityId is not { } destinationCityId
                || !planner.TryPlan(currentLocation.CurrentCityId, destinationCityId, out var plan))
            {
                return new TripSummary(MissingText, MissingText, MissingText);
            }

            var durations = new List<string>(plan.Legs.Count);
            var grades = new List<string>(plan.Legs.Count);
            foreach (var leg in plan.Legs)
            {
                durations.Add($"{FormatDuration(leg.DurationSeconds)}({leg.BandName})");
                grades.Add(leg.GradeName);
            }
            var durationText = string.Join(LegSeparator, durations);
            if (plan.Legs.Count > 1) durationText += $" · 합계 {FormatDuration(plan.TotalDurationSeconds)}";
            return new TripSummary(durationText, string.Join(LegSeparator, grades), MissingText);
        }

        public static string FormatDuration(float seconds)
        {
            var total = Mathf.RoundToInt(seconds);
            return $"{total / 60}:{total % 60:00}";
        }
    }
}

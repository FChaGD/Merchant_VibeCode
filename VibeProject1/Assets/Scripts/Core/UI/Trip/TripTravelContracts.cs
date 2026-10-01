namespace Game.Core
{
    /// <summary>도로의 현재 난이도 = 이벤트로 바뀐 값이 있으면 그 값, 없으면 지도 기본값(Docs/설계/76번 §4.4).</summary>
    public interface IRoadDifficultyReader
    {
        int GetDifficulty(RoadKey road);
    }

    /// <summary>도로 난이도를 바꾸는 쪽(향후 이벤트, 기획 75번 §6) - 지금 호출하는 곳은 없다.</summary>
    public interface IRoadDifficultyRepository : IRoadDifficultyReader
    {
        void SetDifficulty(RoadKey road, int difficulty);
        void ResetDifficulty(RoadKey road);
    }

    /// <summary>두 도시 사이 상행 계획(구간 목록). 경로가 없으면 false.</summary>
    public interface ITripPlanner
    {
        bool TryPlan(int fromCityId, int toCityId, out TripPlan plan);
    }

    /// <summary>"상행 시작" 버튼이 쓰는 출발 조작 - 계획을 세워 여정으로 확정한다. 경로가 없으면 false.</summary>
    public interface ITripDeparture
    {
        bool TryDepart(int fromCityId, int toCityId);
    }

    /// <summary>진행 중 여정(Field의 구간 진행이 쓴다, 설계 76번 §5). 여정이 없으면 HasItinerary = false.</summary>
    public interface ITripItinerary
    {
        bool HasItinerary { get; }
        int LegCount { get; }
        int CurrentLegIndex { get; }
        TripLeg CurrentLeg { get; }
        bool HasNextLeg { get; }
        void AdvanceLeg();
        void Clear();
    }

    /// <summary>현재 구간의 인카운터 빈도(5분 평균 횟수) - EncounterManager가 판정 확률을 계산할 때 쓴다(설계 76번 §6.3).</summary>
    public interface IEncounterRateSource
    {
        float EncountersPerFiveMinutes { get; }
    }
}

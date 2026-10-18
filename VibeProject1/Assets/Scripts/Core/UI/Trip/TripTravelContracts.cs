namespace Game.Core
{
    /// <summary>
    /// 도로 조각의 기본 난이도(지도 데이터, Docs/설계/76번 §4.4). 게임 중 변경은 도로 조각이 아니라 도시 쌍 단위다(ICityPairDifficultyReader, 설계 89 §5.2) -
    /// 예전 도로 조각별 변경 계약(IRoadDifficultyRepository)은 호출처 없이 비어 있다가 도시 쌍 저장으로 교체됐다.
    /// </summary>
    public interface IRoadDifficultyReader
    {
        int GetDifficulty(RoadKey road);
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

    /// <summary>
    /// 현재 구간 실행 난이도(설계 89 §4.3·§5.2) - 이벤트 효과(이번 구간 증감, 도시 쌍 기준값)·구간 라벨·디버그가 쓴다. ITripItinerary에 넣지 않은 이유:
    /// 구간 진행만 하는 소비자에게 난이도 조작을 노출하지 않기 위해서다(ISP).
    /// </summary>
    public interface ICurrentLegDifficulty
    {
        bool HasLeg { get; }
        CityPairKey CurrentPair { get; }
        int CurrentBaseDifficulty { get; }
        int CurrentDifficulty { get; }
        int LegOnlyChange { get; }
        string CurrentGradeName { get; }
        /// <summary>실행 난이도 기준 구간 기본 소요시간 - 구간 시작 시 시계를 이 값으로 시작한다(계획 값이 낡았을 수 있다).</summary>
        float CurrentBaseDurationSeconds { get; }
        void AdjustCurrentLeg(int change);
        /// <summary>실행 난이도가 바뀌었다(시계 기본 교체까지 끝난 뒤).</summary>
        event System.Action Changed;
    }

    /// <summary>현재 구간의 인카운터 빈도(5분 평균 횟수) - EncounterManager가 판정 확률을 계산할 때 쓴다(설계 76번 §6.3).</summary>
    public interface IEncounterRateSource
    {
        float EncountersPerFiveMinutes { get; }
    }

    /// <summary>
    /// 상행 이동 수치 조회(Docs/설계/85번 §3.3) - Field 날짜 정산이 초→일 환산 값을 읽는다. ITripItinerary에 넣지 않은 이유: 구간 진행만
    /// 필요한 소비자에게 이동 수치까지 노출하지 않기 위해서다(ISP).
    /// </summary>
    public interface ITripTravelSettingsReader
    {
        TripTravelSettings Settings { get; }
    }
}

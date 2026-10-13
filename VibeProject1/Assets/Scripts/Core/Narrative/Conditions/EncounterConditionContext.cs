namespace Game.Core
{
    /// <summary>
    /// 조건 판정 순간의 값 묶음(설계 87 §5.1). 판정기는 저장소를 직접 보지 않고 이 값만 읽는다 - 판정 규칙을 Unity 없이 테스트하기 위해서다.
    /// 구간이 없으면(여정 없이 Field 진입 등) 장소 조건은 전부 거짓이다.
    /// </summary>
    public readonly struct EncounterConditionContext
    {
        public GameDate Today { get; }
        public bool HasLeg { get; }
        public int FromCityId { get; }
        public int ToCityId { get; }
        public int FromRegionId { get; }
        public int ToRegionId { get; }
        public float Progress { get; }
        public IEncounterHistoryReader History { get; }

        public EncounterConditionContext(GameDate today, bool hasLeg, int fromCityId, int toCityId, int fromRegionId, int toRegionId, float progress, IEncounterHistoryReader history)
        {
            Today = today;
            HasLeg = hasLeg;
            FromCityId = fromCityId;
            ToCityId = toCityId;
            FromRegionId = fromRegionId;
            ToRegionId = toRegionId;
            Progress = progress;
            History = history;
        }
    }
}

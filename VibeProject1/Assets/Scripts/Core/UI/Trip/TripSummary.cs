namespace Game.Core
{
    /// <summary>
    /// 상행정보 패널에 표시할 텍스트 값 모음. 실제 계산 로직이 없어 전부 표시용 문자열이다 -
    /// 지역/보상 시스템이 설계되면 구조화된 값으로 교체한다. 편성 요약은 포함하지 않는다
    /// (TripPanel이 IFormationReader에서 직접 계산한다).
    /// </summary>
    public readonly struct TripSummary
    {
        public string EstimatedDurationDistanceText { get; }
        public string DangerText { get; }
        public string RewardText { get; }

        public TripSummary(string estimatedDurationDistanceText, string dangerText, string rewardText)
        {
            EstimatedDurationDistanceText = estimatedDurationDistanceText;
            DangerText = dangerText;
            RewardText = rewardText;
        }
    }
}

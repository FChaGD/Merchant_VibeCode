namespace Game.Core
{
    /// <summary>
    /// 결과 정리 플로우(설계 79번 §6) 단계들이 공유하는 읽기 전용 입력. 정산 반영은 플로우 시작 전에 끝나 있으므로 단계는
    /// 저장소를 다시 뒤지지 않고 요약(Summary)만 보고 적용 여부를 판단한다. 승리면 Consequence = null.
    /// </summary>
    internal sealed class BattleAftermathContext
    {
        public BattleResult Result { get; }
        public DefeatConsequence? Consequence { get; }
        public BattleAftermathSummary Summary { get; }

        public BattleAftermathContext(BattleResult result, DefeatConsequence? consequence, BattleAftermathSummary summary)
        {
            Result = result;
            Consequence = consequence;
            Summary = summary;
        }

        public bool IsVictory => Result.Outcome == BattleOutcome.Victory;

        /// <summary>승리 또는 패배-도주 - 상행을 이어 가는 결과(대열 정리·상행 재개 대상).</summary>
        public bool ContinuesTrip => IsVictory || Consequence == DefeatConsequence.Flee;
    }
}

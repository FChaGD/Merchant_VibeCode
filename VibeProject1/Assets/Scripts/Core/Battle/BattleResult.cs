namespace Game.Core
{
    public enum BattleOutcome
    {
        Victory,
        Defeat
    }

    /// <summary>
    /// 전투 결과. 패배 원인·화물 보고서는 전투 뒤 정산이 쓴다(설계 79번 §4.2). 1인자 생성자는 화물·마차가 없는 규칙
    /// (배틀 테스트 씬·Placeholder)용으로 남긴다 - 원인 None, 보고서 Empty.
    /// </summary>
    public readonly struct BattleResult
    {
        private readonly BattleCargoReport cargoReport;

        public BattleOutcome Outcome { get; }
        // 승리면 None.
        public BattleDefeatCause DefeatCause { get; }
        // default(BattleResult)처럼 보고서가 비어 있어도 소비자가 null을 다루지 않게 Empty를 돌려준다.
        public BattleCargoReport CargoReport => cargoReport ?? BattleCargoReport.Empty;

        public BattleResult(BattleOutcome outcome)
            : this(outcome, BattleDefeatCause.None, BattleCargoReport.Empty)
        {
        }

        public BattleResult(BattleOutcome outcome, BattleDefeatCause defeatCause, BattleCargoReport cargoReport)
        {
            Outcome = outcome;
            DefeatCause = defeatCause;
            this.cargoReport = cargoReport;
        }
    }
}

namespace Game.Core
{
    public enum BattleOutcome
    {
        Victory,
        Defeat
    }

    public readonly struct BattleResult
    {
        public BattleOutcome Outcome { get; }

        public BattleResult(BattleOutcome outcome)
        {
            Outcome = outcome;
        }
    }
}

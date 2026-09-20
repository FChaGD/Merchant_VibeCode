namespace Game.Core
{
    /// <summary>적 진영 정의 - 타입 구성 공급자로 기본 스탯을 조회하고, 전술 행동 없이 타입별 플레이스홀더 아이콘을 쓴다.</summary>
    public class BattleTestEnemySide : IBattleTestSide
    {
        private readonly TableEnemyTypeCompositionProvider enemyProvider;
        private readonly IDamageFormula damageFormula;
        private readonly IUnitSpatialQuery spatialQuery;

        public bool IsAlly => false;
        public BattleTestRoster Roster { get; } = new();
        public PartyMorale Morale { get; private set; }
        public MoraleWaveCoordinator WaveCoordinator { get; private set; }

        public BattleTestEnemySide(TableEnemyTypeCompositionProvider enemyProvider, IDamageFormula damageFormula, IUnitSpatialQuery spatialQuery)
        {
            this.enemyProvider = enemyProvider;
            this.damageFormula = damageFormula;
            this.spatialQuery = spatialQuery;
        }

        public void BeginBattle(float fieldRadius)
        {
            Morale = new PartyMorale();
            WaveCoordinator = new MoraleWaveCoordinator(fieldRadius);
        }

        public BattleUnitStats GetDefaultStats(BattleTestUnitKind kind) => enemyProvider.GetStatsForType(kind.ToEnemyType());

        public BattleCharacterUnit CreatePreviewUnit(BattleTestRoster.Entry entry)
        {
            var stats = entry.StatsOverride ?? GetDefaultStats(entry.Kind);
            return new BattleCharacterUnit(entry.Position, isAlly: false, stats, damageFormula, new PartyMorale(), new MoraleWaveCoordinator(0f), spatialQuery, 0f, icon: BattlePlaceholderSprite.ForEnemyType(entry.Kind.ToEnemyType()));
        }

        public BattleCharacterUnit CreateBattleUnit(BattleTestRoster.Entry entry, BattleTestBattleContext context)
        {
            var stats = entry.StatsOverride ?? GetDefaultStats(entry.Kind);
            return new BattleCharacterUnit(entry.Position, isAlly: false, stats, damageFormula, Morale, WaveCoordinator, spatialQuery, context.FleeTravelDistance, icon: BattlePlaceholderSprite.ForEnemyType(entry.Kind.ToEnemyType()));
        }
    }
}

namespace Game.Core
{
    /// <summary>아군 진영 정의 - 직업 스탯 공급자로 기본 스탯을 조회하고, 전투 유닛에는 방향성 지시 전술 행동을 부여한다.</summary>
    public class BattleTestAllySide : IBattleTestSide
    {
        private readonly IBattleUnitStatProvider statProvider;
        private readonly IDamageFormula damageFormula;
        private readonly IUnitSpatialQuery spatialQuery;

        public bool IsAlly => true;
        public BattleTestRoster Roster { get; } = new();
        public PartyMorale Morale { get; private set; }
        public MoraleWaveCoordinator WaveCoordinator { get; private set; }

        public BattleTestAllySide(IBattleUnitStatProvider statProvider, IDamageFormula damageFormula, IUnitSpatialQuery spatialQuery)
        {
            this.statProvider = statProvider;
            this.damageFormula = damageFormula;
            this.spatialQuery = spatialQuery;
        }

        public void BeginBattle(float fieldRadius)
        {
            Morale = new PartyMorale();
            WaveCoordinator = new MoraleWaveCoordinator(fieldRadius);
        }

        public BattleUnitStats GetDefaultStats(BattleTestUnitKind kind) => statProvider.GetStats(kind.ToMercenaryClass());

        public BattleCharacterUnit CreatePreviewUnit(BattleTestRoster.Entry entry)
        {
            var stats = entry.StatsOverride ?? GetDefaultStats(entry.Kind);
            return new BattleCharacterUnit(entry.Position, isAlly: true, stats, damageFormula, new PartyMorale(), new MoraleWaveCoordinator(0f), spatialQuery, 0f);
        }

        public BattleCharacterUnit CreateBattleUnit(BattleTestRoster.Entry entry, BattleTestBattleContext context)
        {
            var stats = entry.StatsOverride ?? GetDefaultStats(entry.Kind);
            var tacticsBehaviors = BuildTacticsBehaviors(entry, context);
            return new BattleCharacterUnit(entry.Position, isAlly: true, stats, damageFormula, Morale, WaveCoordinator, spatialQuery, context.FleeTravelDistance, tacticsBehaviors);
        }

        // 배치 슬롯 좌표 대신 드롭 좌표를 그대로 HomePosition으로 쓴다 - 자유 배치라 "슬롯"이라는
        // 개념이 없을 뿐, 방향성 지시가 참조하는 의미(그 유닛이 원래 있어야 할 자리)는 동일하다.
        private UnitTacticsBehaviors BuildTacticsBehaviors(BattleTestRoster.Entry entry, BattleTestBattleContext context)
        {
            if (context.TacticsProfileResolver == null) return null;

            var profile = context.TacticsProfileResolver.Resolve(entry.Kind.ToMercenaryClass(), entry.Position);
            return UnitTacticsBehaviorsFactory.Build(
                profile, context.StandardActivityRadius, context.FieldRadius, spatialQuery,
                context.FrontlineCoordinator, context.RangedSurroundCoordinator);
        }
    }
}

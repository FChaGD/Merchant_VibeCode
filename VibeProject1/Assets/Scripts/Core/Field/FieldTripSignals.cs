namespace Game.Core
{
    /// <summary>
    /// Field 조율자들(FieldUIController·인카운터 흐름·구간 진행·전투 마무리)에 넘기는 정지·계기·이벤트 전투 결과 전달 묶음. 이미 인자가 많은 등록 메서드에 계약을
    /// 하나씩 더하지 않기 위해 묶었다. 전부 null일 수 있다(Bootstrap 미재빌드) - 소비자는 예전 동작으로 물러난다. 이벤트 전투 결과 전달은 설계 89 §4.2.
    /// </summary>
    public sealed class FieldTripSignals
    {
        public ITripHaltControl Halt { get; }
        public ITripMilestoneSink Milestones { get; }
        public IEventBattleOutcomeSink EventBattle { get; }

        public FieldTripSignals(ITripHaltControl halt, ITripMilestoneSink milestones, IEventBattleOutcomeSink eventBattle = null)
        {
            Halt = halt;
            Milestones = milestones;
            EventBattle = eventBattle;
        }

        public static FieldTripSignals Resolve(IDependencyResolver registrar)
        {
            registrar.TryResolve<ITripHaltControl>(out var halt);
            registrar.TryResolve<ITripMilestoneSink>(out var milestones);
            registrar.TryResolve<IEventBattleOutcomeSink>(out var eventBattle);
            return new FieldTripSignals(halt, milestones, eventBattle);
        }
    }
}

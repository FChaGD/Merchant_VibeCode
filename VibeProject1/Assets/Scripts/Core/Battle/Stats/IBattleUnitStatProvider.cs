namespace Game.Core
{
    public interface IBattleUnitStatProvider
    {
        BattleUnitStats GetStats(MercenaryClass mercenaryClass);
    }
}

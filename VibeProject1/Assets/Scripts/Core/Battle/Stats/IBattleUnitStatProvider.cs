namespace Game.Core
{
    /// <summary>
    /// 아군 캐릭터 스탯 조회. 키는 직업이 아니라 캐릭터 Id다 - 같은 직업이라도 캐릭터마다 스탯 행이 따로 있다(Docs/설계/54번 §5).
    /// </summary>
    public interface IBattleUnitStatProvider
    {
        BattleUnitStats GetStats(string characterId);
    }
}

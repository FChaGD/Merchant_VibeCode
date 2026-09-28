namespace Game.Core
{
    /// <summary>
    /// 직업 단위로만 유닛을 만드는 소비자(배틀 테스트 씬)를 위한 조회 - 해당 직업의 대표 캐릭터(테이블 첫 행) 스탯을 준다
    /// (Docs/설계/54번 §6.3). 실제 로스터 전투는 캐릭터 Id로 조회하므로(IBattleUnitStatProvider) 이 계약을 쓰지 않는다.
    /// </summary>
    public interface IClassRepresentativeStatProvider
    {
        BattleUnitStats GetRepresentativeStats(string mercenaryClass);
    }
}

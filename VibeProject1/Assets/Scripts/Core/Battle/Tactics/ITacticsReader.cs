namespace Game.Core
{
    /// <summary>
    /// 방향성 지시 조회만 필요한 소비자를 위한 읽기 전용 계약(IFormationReader와 같은 판단 기준) -
    /// 전투 시뮬레이션(IUnitTacticsProfileResolver)처럼 값을 바꿀 필요가 없는 곳은 이 인터페이스만
    /// 의존해 ITacticsRepository의 Set 계열 메서드에 대한 접근 권한을 아예 갖지 않도록 한다.
    /// </summary>
    public interface ITacticsReader
    {
        PartyTacticsSettings GetPartySettings();
        RoleGroupTacticsOverride GetRoleGroupOverride(RoleGroup roleGroup);
    }
}

namespace Game.Core
{
    /// <summary>
    /// TacticsPanel이 값을 반영할 대상. "저장(게임 세이브)"이 아니라 "현재 상행에 적용"하는
    /// 개념이다(IFormationRepository와 동일 판단).
    /// </summary>
    public interface ITacticsRepository : ITacticsReader
    {
        void SetPartySettings(PartyTacticsSettings settings);
        void SetRoleGroupOverride(RoleGroup roleGroup, RoleGroupTacticsOverride value);
    }
}

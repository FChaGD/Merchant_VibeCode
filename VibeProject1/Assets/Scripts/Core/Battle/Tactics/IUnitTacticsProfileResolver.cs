using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// "직업→역할군→프로필" 해석을 전담한다. LiveBattleSimulationRule.BuildAllies에 이 로직을
    /// 인라인하지 않은 이유는 그 클래스의 조립 책임이 계속 커지는 걸 막고, 향후 개체별 방향성이
    /// 이 해석 체인에 끼어들 정확한 확장 지점을 마련하기 위함이다(Docs/설계/12번 §5.3, §6-1의
    /// "개체별이 상단 지침+역할군별 방향성을 override할 예정"과 직결).
    /// </summary>
    public interface IUnitTacticsProfileResolver
    {
        UnitTacticsProfile Resolve(MercenaryClass mercenaryClass, Vector2 homePosition);
    }
}

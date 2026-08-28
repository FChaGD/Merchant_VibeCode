using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// ITacticsReader(읽기 전용)만 의존한다 - 프로필 해석은 값을 바꿀 필요가 없다(ISP, Docs/기획/12번
    /// CLAUDE.md 컨벤션의 IFormationReader 사례와 동일 판단). 역할군 override의 카탈로그 기본값
    /// 폴백은 ITacticsRepository.GetRoleGroupOverride가 이미 처리하므로 이 클래스는 카탈로그를
    /// 직접 참조하지 않는다. 단, 직업이 역할군 맵에 아예 없는 경우(설정 누락)는 그 위임 경로를 탈 수
    /// 없어(어떤 역할군을 조회해야 할지 모름) RoleGroup.Frontline 카탈로그 기본값으로 안전하게
    /// 대체한다 - default(RoleGroupTacticsOverride)를 그대로 반환하면 카탈로그를 거치지 않은 raw
    /// enum-zero 값이 새어나간다.
    /// </summary>
    public class UnitTacticsProfileResolver : IUnitTacticsProfileResolver
    {
        private readonly ITacticsReader tacticsReader;
        private readonly MercenaryRoleGroupMapAsset roleGroupMap;

        public UnitTacticsProfileResolver(ITacticsReader tacticsReader, MercenaryRoleGroupMapAsset roleGroupMap)
        {
            this.tacticsReader = tacticsReader;
            this.roleGroupMap = roleGroupMap;
        }

        public UnitTacticsProfile Resolve(MercenaryClass mercenaryClass, Vector2 homePosition)
        {
            var party = tacticsReader.GetPartySettings();
            var roleGroupOverride = ResolveRoleGroupOverride(mercenaryClass);

            return new UnitTacticsProfile(
                party.RecognitionType, party.RadiusPreset, party.Pursuit,
                roleGroupOverride.TargetPriority, roleGroupOverride.Positioning, roleGroupOverride.SelfPreservation,
                homePosition);
        }

        private RoleGroupTacticsOverride ResolveRoleGroupOverride(MercenaryClass mercenaryClass)
        {
            if (roleGroupMap != null && roleGroupMap.TryGetRoleGroup(mercenaryClass, out var roleGroup))
            {
                return tacticsReader.GetRoleGroupOverride(roleGroup);
            }

            Debug.LogWarning($"{nameof(UnitTacticsProfileResolver)}: 직업 '{mercenaryClass}'에 매핑된 역할군을 찾을 수 없어 '{RoleGroup.Frontline}' 카탈로그 기본값으로 대체한다.");
            return tacticsReader.GetRoleGroupOverride(RoleGroup.Frontline);
        }
    }
}

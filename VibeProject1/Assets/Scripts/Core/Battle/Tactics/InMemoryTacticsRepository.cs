using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// TacticsPanel에서 적용한 방향성 지시를 현재 플레이 세션 동안 보관한다 - InMemoryFormationRepository와
    /// 같은 성격("게임 세이브"가 아니라 "현재 상행에 적용"). 역할군 값이 한 번도 지정된 적 없으면
    /// RoleGroupTacticsCatalogAsset의 후보 목록 첫 항목을 그 자리에서 계산해 돌려준다(Docs/설계/11번
    /// §2.1의 "override 초기값 = 카탈로그 첫 항목" 규칙) - 별도로 "초기화"를 거치지 않아도 항상
    /// 유효한 값을 돌려주기 위함이다.
    /// </summary>
    public class InMemoryTacticsRepository : MonoBehaviour, ITacticsRepository, IManagedComponent
    {
        [SerializeField] private RoleGroupTacticsCatalogAsset catalog;

        private static readonly PartyTacticsSettings DefaultPartySettings = new(
            EnemyRecognitionType.OneSecondDelay, ActivityRadiusPreset.Standard, PursuitPreset.OffensiveJudgment);

        private PartyTacticsSettings? partySettings;
        private readonly Dictionary<RoleGroup, RoleGroupTacticsOverride> roleGroupOverrides = new();

        public void RegisterSelf(IDependencyRegistrar registrar)
        {
            registrar.Register<ITacticsRepository>(this);
        }

        public void ResolveDependencies(IDependencyRegistrar registrar)
        {
            // 다른 매니저에 대한 의존성이 없다.
        }

        public PartyTacticsSettings GetPartySettings() => partySettings ?? DefaultPartySettings;

        public void SetPartySettings(PartyTacticsSettings settings) => partySettings = settings;

        public RoleGroupTacticsOverride GetRoleGroupOverride(RoleGroup roleGroup)
        {
            if (roleGroupOverrides.TryGetValue(roleGroup, out var stored))
            {
                return stored;
            }

            return ResolveCatalogDefault(roleGroup);
        }

        public void SetRoleGroupOverride(RoleGroup roleGroup, RoleGroupTacticsOverride value)
        {
            roleGroupOverrides[roleGroup] = value;
        }

        private RoleGroupTacticsOverride ResolveCatalogDefault(RoleGroup roleGroup)
        {
            if (catalog == null)
            {
                Debug.LogWarning($"{nameof(InMemoryTacticsRepository)}: {nameof(RoleGroupTacticsCatalogAsset)}가 연결되지 않아 '{roleGroup}' 기본값을 계산할 수 없다.");
                return default;
            }

            if (!catalog.TryGetEntry(roleGroup, out var entry)
                || entry.TargetPriorityOptions is not { Count: > 0 }
                || entry.PositioningOptions is not { Count: > 0 }
                || entry.SelfPreservationOptions is not { Count: > 0 })
            {
                Debug.LogWarning($"{nameof(InMemoryTacticsRepository)}: '{roleGroup}' 역할군의 후보 목록이 카탈로그에 비어 있어 기본값을 계산할 수 없다.");
                return default;
            }

            return new RoleGroupTacticsOverride(
                isOverridden: false,
                targetPriority: entry.TargetPriorityOptions[0].Value,
                positioning: entry.PositioningOptions[0].Value,
                selfPreservation: entry.SelfPreservationOptions[0].Value);
        }
    }
}

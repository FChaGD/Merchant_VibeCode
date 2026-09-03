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
        // 파티 3축 기본값 출처(엑셀 임포트 결과, Docs/설계/17번 §10) - 예전엔 DefaultPartySettings
        // 코드 상수였다.
        [SerializeField] private PartyTacticsPolicyCatalogAsset partyPolicyCatalog;

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

        public PartyTacticsSettings GetPartySettings() => partySettings ?? ResolveDefaultPartySettings();

        // 축마다 IsDefault==true인 항목을 찾는다(§10.4 임포트 시점에 축당 정확히 1개임을 검증).
        // 카탈로그가 비어있거나 IsDefault 항목이 없으면 ResolveCatalogDefault와 같은 fail-soft
        // 원칙으로 경고 후 그 축은 enum 0번째 값(default)으로 폴백한다.
        private PartyTacticsSettings ResolveDefaultPartySettings()
        {
            if (partyPolicyCatalog == null)
            {
                Debug.LogWarning($"{nameof(InMemoryTacticsRepository)}: {nameof(PartyTacticsPolicyCatalogAsset)}가 연결되지 않아 파티 3축 기본값을 계산할 수 없다.");
                return default;
            }

            return new PartyTacticsSettings(
                ResolveRecognitionDefault(partyPolicyCatalog.RecognitionOptions),
                ResolveRadiusDefault(partyPolicyCatalog.RadiusOptions),
                ResolvePursuitDefault(partyPolicyCatalog.PursuitOptions));
        }

        // 3개 축 구조체(EnemyRecognitionOption/ActivityRadiusOption/PursuitOption)가 서로 다른 타입이라
        // 제네릭 하나로 묶을 수 없다 - TacticsPanel.IndexOfOption과 같은 이유로 타입별 반복을 택했다.
        private EnemyRecognitionType ResolveRecognitionDefault(IReadOnlyList<EnemyRecognitionOption> options)
        {
            foreach (var option in options)
            {
                if (option.IsDefault) return option.Value;
            }
            Debug.LogWarning($"{nameof(InMemoryTacticsRepository)}: '{nameof(EnemyRecognitionType)}' 축에 IsDefault 항목이 없어 기본값을 계산할 수 없다.");
            return default;
        }

        private ActivityRadiusPreset ResolveRadiusDefault(IReadOnlyList<ActivityRadiusOption> options)
        {
            foreach (var option in options)
            {
                if (option.IsDefault) return option.Value;
            }
            Debug.LogWarning($"{nameof(InMemoryTacticsRepository)}: '{nameof(ActivityRadiusPreset)}' 축에 IsDefault 항목이 없어 기본값을 계산할 수 없다.");
            return default;
        }

        private PursuitPreset ResolvePursuitDefault(IReadOnlyList<PursuitOption> options)
        {
            foreach (var option in options)
            {
                if (option.IsDefault) return option.Value;
            }
            Debug.LogWarning($"{nameof(InMemoryTacticsRepository)}: '{nameof(PursuitPreset)}' 축에 IsDefault 항목이 없어 기본값을 계산할 수 없다.");
            return default;
        }

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
